using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;
using Myra.MML;

namespace Myra.Mcp;

/// <summary>
/// Reflects over Myra's widget assemblies to report the MML vocabulary a layout may use: the valid
/// tags, each widget's settable properties (with enum options and defaults), the attached properties
/// a child can carry (e.g. <c>Grid.Row</c>), and the style names in the active stylesheet.
/// Enumeration mirrors what <see cref="Project"/> resolves, so the catalog never advertises a tag or
/// property the loader would reject (verified by a parity test).
/// </summary>
public sealed class WidgetCatalog
{
    private static readonly string[] MyraNamespaces = { "Myra.Graphics2D.UI", "Myra.Graphics2D.UI.Properties" };

    private static readonly Dictionary<string, string> LegacyAliases = new(StringComparer.Ordinal)
    {
        ["TextBlock"] = "Label",
        ["TextField"] = "TextBox",
        ["VerticalBox"] = "VerticalStackPanel",
        ["HorizontalBox"] = "HorizontalStackPanel",
        ["ScrollPane"] = "ScrollViewer",
    };

    // Value types that serialize as a single MML attribute (a superset kept intentionally small; the
    // parity test guards that what is reported as an attribute actually loads as one).
    private static readonly HashSet<string> AttributeValueTypeNames = new(StringComparer.Ordinal)
    {
        "Thickness", "Color", "FSColor", "Vector2", "Point", "Rectangle",
        "SpriteFontBase", "IBrush", "SolidBrush", "IImage", "TextureRegionAtlas",
    };

    private readonly Dictionary<string, Type> _typesByTag;
    private AttachedProperty[]? _attachedProperties;

    public WidgetCatalog()
    {
        _typesByTag = BuildTypeMap();
    }

    public IReadOnlyList<WidgetTypeInfo> ListWidgetTypes()
    {
        return _typesByTag
            .Where(kv => string.Equals(kv.Key, kv.Value.Name, StringComparison.Ordinal)) // canonical names, not legacy aliases
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new WidgetTypeInfo(kv.Key, kv.Value.BaseType?.Name, DescribeRole(kv.Value)))
            .ToArray();
    }

    /// <summary>Describes a widget by tag name (legacy aliases accepted). Returns null for an unknown tag.</summary>
    public WidgetDescription? DescribeWidget(string name)
    {
        var canonical = LegacyAliases.TryGetValue(name, out var alias) ? alias : name;
        if (!_typesByTag.TryGetValue(canonical, out var type))
        {
            return null;
        }

        lock (MyraEngine.Gate)
        {
            MyraEngine.Initialize();
            var properties = SettableProperties(type).Select(ToWidgetProperty).ToArray();
            var styleNames = StyleNames(type);
            return new WidgetDescription(type.Name, properties, GetAttachedProperties(), styleNames);
        }
    }

    private static Dictionary<string, Type> BuildTypeMap()
    {
        var sources = new List<(Assembly Assembly, string[] Namespaces)>
        {
            (typeof(Widget).Assembly, MyraNamespaces),
        };
        foreach (var pair in Project.ExtraWidgetAssembliesAndNamespaces)
        {
            sources.Add((pair.Key, pair.Value));
        }

        var map = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var (assembly, namespaces) in sources)
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (!type.IsPublic || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (type.Namespace == null || !namespaces.Contains(type.Namespace))
                {
                    continue;
                }

                if (!typeof(Widget).IsAssignableFrom(type))
                {
                    continue;
                }

                map[type.Name] = type;
            }
        }

        foreach (var (aliasName, target) in LegacyAliases)
        {
            if (map.ContainsKey(target))
            {
                map[aliasName] = map[target];
            }
        }

        return map;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    private AttachedProperty[] GetAttachedProperties()
    {
        if (_attachedProperties != null)
        {
            return _attachedProperties;
        }

        var list = new List<AttachedProperty>();
        foreach (var type in _typesByTag.Values.Distinct())
        {
            // Attached properties are registered in the owner type's static initializer.
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            foreach (var ap in AttachedPropertiesRegistry.GetPropertiesOfType(type))
            {
                list.Add(new AttachedProperty(ap.OwnerType.Name, ap.Name, $"{ap.OwnerType.Name}.{ap.Name}", ap.PropertyType.Name));
            }
        }

        _attachedProperties = list
            .GroupBy(a => a.Syntax, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(a => a.Syntax, StringComparer.Ordinal)
            .ToArray();
        return _attachedProperties;
    }

    private static IEnumerable<PropertyInfo> SettableProperties(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetGetMethod() is { IsPublic: true, IsStatic: false }
                        && p.GetSetMethod() is { IsPublic: true }
                        && p.GetCustomAttribute<XmlIgnoreAttribute>() == null
                        && p.GetCustomAttribute<BrowsableAttribute>() is not { Browsable: false })
            .OrderBy(p => p.Name, StringComparer.Ordinal);
    }

    private static WidgetProperty ToWidgetProperty(PropertyInfo property)
    {
        var underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var enumValues = underlying.IsEnum ? Enum.GetNames(underlying) : null;
        var defaultValue = property.GetCustomAttribute<DefaultValueAttribute>()?.Value?.ToString();
        return new WidgetProperty(property.Name, FriendlyTypeName(property.PropertyType), IsAttributeType(property.PropertyType), defaultValue, enumValues);
    }

    private static bool IsAttributeType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal))
        {
            return true;
        }

        if (AttributeValueTypeNames.Contains(t.Name))
        {
            return true;
        }

        return t.GetInterfaces().Any(i => AttributeValueTypeNames.Contains(i.Name));
    }

    private static string FriendlyTypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        return underlying != null ? underlying.Name + "?" : type.Name;
    }

    private static string DescribeRole(Type type)
    {
        var content = ContentProperty(type);
        if (content == null)
        {
            return "widget";
        }

        var isList = typeof(IEnumerable).IsAssignableFrom(content.PropertyType) && content.PropertyType != typeof(string);
        return isList ? "container" : "single-child container";
    }

    private static PropertyInfo? ContentProperty(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.GetCustomAttribute<Myra.Attributes.ContentAttribute>() != null);
    }

    private static string[] StyleNames(Type type)
    {
        var sheet = Stylesheet.Current;
        var property = sheet.GetType().GetProperty(type.Name + "Styles");
        if (property?.GetValue(sheet) is IDictionary dictionary)
        {
            return dictionary.Keys
                .Cast<object?>()
                .Select(k => k?.ToString())
                .Where(s => !string.IsNullOrEmpty(s))
                .Cast<string>()
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();
        }

        return Array.Empty<string>();
    }
}
