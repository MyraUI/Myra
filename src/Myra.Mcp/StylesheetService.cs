using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AssetManagementBase;
using Myra.Graphics2D.UI.Styles;

namespace Myra.Mcp;

/// <summary>
/// Read-only inspection of a stylesheet and its atlas, so an agent can see which named styles
/// (referenceable via a widget's <c>StyleName</c>), fonts, and atlas region (drawable) names a layout
/// may use. Loads through the same engine path as validation, under <see cref="MyraEngine.Gate"/>; the
/// built-in default skin is inspected when no path is given. Paths are confined via <see cref="MyraWorkspace"/>.
/// </summary>
public sealed class StylesheetService
{
    private readonly MyraWorkspace _workspace;

    public StylesheetService(MyraWorkspace workspace)
    {
        _workspace = workspace;
    }

    /// <summary>Inspects the stylesheet at <paramref name="stylesheetPath"/> (confined), or the built-in default skin when null.</summary>
    public StylesheetInfo Inspect(string? stylesheetPath = null)
    {
        var full = stylesheetPath != null ? _workspace.Resolve(stylesheetPath, mustBeXmmp: false) : null;

        lock (MyraEngine.Gate)
        {
            MyraEngine.Initialize();
            var sheet = Load(full);
            return new StylesheetInfo(StyleGroups(sheet), FontIds(sheet), AtlasRegions(sheet));
        }
    }

    private static Stylesheet Load(string? stylesheetFullPath)
    {
        if (stylesheetFullPath == null)
        {
            return Stylesheet.Current;
        }

        var manager = AssetManager.CreateFileAssetManager(Path.GetDirectoryName(stylesheetFullPath)!);
        return manager.LoadStylesheet(Path.GetFileName(stylesheetFullPath));
    }

    // A widget's named styles live in its "<Widget>Styles" dictionary (e.g. ButtonStyles), keyed by
    // StyleName. The empty-string default style is not referenceable by name, so it is omitted; a
    // widget with only a default style contributes no group. Mirrors WidgetCatalog's style-name logic.
    private static StyleGroup[] StyleGroups(Stylesheet sheet)
    {
        var groups = new List<StyleGroup>();
        foreach (var property in sheet.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.Name.EndsWith("Styles", StringComparison.Ordinal) || property.GetValue(sheet) is not IDictionary dictionary)
            {
                continue;
            }

            var names = dictionary.Keys
                .Cast<object?>()
                .Select(k => k?.ToString())
                .Where(s => !string.IsNullOrEmpty(s))
                .Cast<string>()
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();
            if (names.Length == 0)
            {
                continue;
            }

            var widget = property.Name[..^"Styles".Length];
            groups.Add(new StyleGroup(widget, names));
        }

        return groups.OrderBy(g => g.Widget, StringComparer.Ordinal).ToArray();
    }

    private static string[] FontIds(Stylesheet sheet)
        => sheet.Fonts
            .Select(f => f.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    private static string[] AtlasRegions(Stylesheet sheet)
        => sheet.Atlas?.Regions.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
}
