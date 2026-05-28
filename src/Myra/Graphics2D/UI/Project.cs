using System.Collections;
using System.Reflection;
using System.ComponentModel;
using Myra.Graphics2D.UI.Styles;
using System.Xml.Linq;
using System.Xml.Serialization;
using System;
using Myra.MML;
using System.Collections.Generic;
using Myra.Attributes;
using System.Linq;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI.Properties;
using FontStashSharp;
using Myra.Utility;
using Myra.Graphics2D.UI.File;
using AssetManagementBase;

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// Options for exporting a UI project.
	/// </summary>
	public class ExportOptions
	{
		/// <summary>
		/// Gets or sets the namespace for the exported code.
		/// </summary>
		public string Namespace { get; set; }

		/// <summary>
		/// Gets or sets the class name for the exported code.
		/// </summary>
		public string Class { get; set; }

		/// <summary>
		/// Gets or sets the output path for the exported files.
		/// </summary>
		public string OutputPath { get; set; }

		/// <summary>
		/// Gets or sets the template for the designer file.
		/// </summary>
		public string TemplateDesigner { get; set; }

		/// <summary>
		/// Gets or sets the template for the main file.
		/// </summary>
		public string TemplateMain { get; set; }
	}

	/// <summary>
	/// Represents the position of an object in a document.
	/// </summary>
	public class ObjectPosition
	{
		/// <summary>
		/// Gets the object.
		/// </summary>
		public object Object { get; private set; }

		/// <summary>
		/// Gets the starting position.
		/// </summary>
		public int Start { get; private set; }

		/// <summary>
		/// Gets the ending position.
		/// </summary>
		public int End { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ObjectPosition"/> class with the specified object and positions.
		/// </summary>
		/// <param name="obj">The object.</param>
		/// <param name="start">The starting position.</param>
		/// <param name="end">The ending position.</param>
		public ObjectPosition(object obj, int start, int end)
		{
			Object = obj;
			Start = start;
			End = end;
		}
	}

	/// <summary>
	/// Represents a UI project that can be saved, loaded, and exported to code.
	/// </summary>
	public class Project
	{
		// RAII pattern utility: temporarily changes stylesheet and restores on Dispose
		// Used to apply a specific stylesheet during loading/saving without permanently changing global stylesheet
		private struct StylesheetChanger: IDisposable
		{
			private readonly Stylesheet _oldStylesheet;

			public StylesheetChanger(Stylesheet newStylesheet)
			{
				_oldStylesheet = Stylesheet.Current;
				Stylesheet.Current = newStylesheet;
			}

			public void Dispose()
			{
				Stylesheet.Current = _oldStylesheet;
			}
		}

		/// <summary>Constant name for proportion values.</summary>
		public const string ProportionName = "Proportion";
		/// <summary>Constant name for default proportion values.</summary>
		public const string DefaultProportionName = "DefaultProportion";
		/// <summary>Constant name for default column proportion values.</summary>
		public const string DefaultColumnProportionName = "DefaultColumnProportion";
		/// <summary>Constant name for default row proportion values.</summary>
		public const string DefaultRowProportionName = "DefaultRowProportion";

		// Maps old deprecated class names to their modern replacements for backward compatibility
		private static readonly Dictionary<string, string> LegacyClassNames = new Dictionary<string, string>();

		private readonly ExportOptions _exportOptions = new ExportOptions();  // Code export settings

		/// <summary>
		/// Gets the export options for this project.
		/// </summary>
		[Browsable(false)]
		public ExportOptions ExportOptions
		{
			get { return _exportOptions; }
		}

		/// <summary>
		/// Gets or sets the root widget of the project.
		/// </summary>
		[Browsable(false)]
		[Content]
		public Widget Root { get; set; }

		/// <summary>
		/// Gets or sets the path to the stylesheet file.
		/// </summary>
		[Browsable(false)]
		public string StylesheetPath { get; set; }

		/// <summary>
		/// Gets or sets the stylesheet for this project.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Stylesheet Stylesheet { get; set; }

		/// <summary>
		/// Gets or sets the designer runtime assets folder path.
		/// </summary>
		[FilePath(FileDialogMode.ChooseFolder)]
		public string DesignerRtfAssetsPath { get; set; }

		/// <summary>
		/// Gets the mapping of loaded objects to their respective XML nodes.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public List<Tuple<object, XElement>> ObjectsNodes { get; internal set; }

		// Initializes legacy class name mappings for loading old project files
		static Project()
		{
			LegacyClassNames["VerticalBox"] = "VerticalStackPanel";
			LegacyClassNames["HorizontalBox"] = "HorizontalStackPanel";
			LegacyClassNames["TextField"] = "TextBox";
			LegacyClassNames["TextBlock"] = "Label";
			LegacyClassNames["ScrollPane"] = "ScrollViewer";
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Project"/> class.
		/// </summary>
		public Project()
		{
			Stylesheet = Stylesheet.Current;
		}

		/// <summary>
		/// Determines whether the specified name is a proportion property name.
		/// </summary>
		/// <param name="s">The name to check.</param>
		/// <returns>true if the name is a proportion name; otherwise, false.</returns>
		public static bool IsProportionName(string s)
		{
			return s.EndsWith(ProportionName) ||
				s.EndsWith(DefaultProportionName) ||
				s.EndsWith(DefaultColumnProportionName) ||
				s.EndsWith(DefaultRowProportionName);
		}

		/// <summary>
		/// Determines whether a property should be serialized for the specified object.
		/// Omits properties that have default values, match stylesheet, or are auto-managed layout properties.
		/// </summary>
		/// <param name="stylesheet">The stylesheet to use for comparison.</param>
		/// <param name="o">The object containing the property.</param>
		/// <param name="p">The property information.</param>
		/// <returns>true if the property should be serialized; otherwise, false.</returns>
		public static bool ShouldSerializeProperty(Stylesheet stylesheet, object o, PropertyInfo p)
		{
			// Skip auto-assigned GridRow/GridColumn when widget is in a SplitPane or StackPanel container
			var asWidget = o as Widget;
			if (asWidget != null && asWidget.Parent != null && asWidget.Parent is Grid)
			{
				var container = asWidget.Parent.Parent;
				if (container != null &&
				   (container is StackPanel || container is SplitPane) &&
				   (p.Name == "GridRow" || p.Name == "GridColumn"))
				{
					return false;
				}
			}

			// Skip default proportion values for Grid
			var asGrid = o as Grid;
			if (asGrid != null)
			{
				var value = p.GetValue(o);
				if ((p.Name == DefaultColumnProportionName || p.Name == DefaultRowProportionName) &&
					value == Proportion.GridDefault)
				{
					return false;
				}
			}

			// Skip default proportion values for StackPanel
			var asBox = o as StackPanel;
			if (asBox != null)
			{
				var value = p.GetValue(o);
				if (p.Name == DefaultProportionName && value == Proportion.StackPanelDefault)
				{
					return false;
				}
			}

			// Skip properties that have default values (not modified)
			if (SaveContext.HasDefaultValue(o, p))
			{
				return false;
			}

			// Skip properties that match stylesheet values (inherited from style)
			if (asWidget != null && HasStylesheetValue(asWidget, p, stylesheet))
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Determines whether a property should be serialized for the specified object using this project's stylesheet.
		/// </summary>
		/// <param name="o">The object containing the property.</param>
		/// <param name="p">The property information.</param>
		/// <returns>true if the property should be serialized; otherwise, false.</returns>
		public bool ShouldSerializeProperty(object o, PropertyInfo p)
		{
			return ShouldSerializeProperty(Stylesheet, o, p);
		}

		internal static SaveContext CreateSaveContext(Stylesheet stylesheet)
		{
			return new SaveContext
			{
				ShouldSerializeProperty = (o, p) => ShouldSerializeProperty(stylesheet, o, p)
			};
		}

		// Creates save context using this project's stylesheet
		internal SaveContext CreateSaveContext()
		{
			return CreateSaveContext(Stylesheet);
		}

		/// <summary>
		/// Gets or sets the extra widget assemblies and namespaces to include during project loading and saving.
		/// </summary>
		public static Dictionary<Assembly, string[]> ExtraWidgetAssembliesAndNamespaces = new Dictionary<Assembly, string[]>();
		
		// Creates a load context for deserializing UI projects from XML.
		// Sets up asset loading, widget type resolution, and legacy name mapping.
		internal static LoadContext CreateLoadContext(AssetManager assetManager)
		{
			// Creates resource instances (brushes, textures, fonts) by name using asset manager
			Func<Type, string, object> resourceGetter = (t, name) =>
			{
				if (t == typeof(IBrush))
				{
					return new SolidBrush(name);
				}
				else if (t == typeof(IImage))
				{
					return assetManager.LoadTextureRegion(name);
				}
				else if (t == typeof(SpriteFontBase))
				{
					return assetManager.LoadFont(name);
				}

				throw new Exception(string.Format("Type {0} isn't supported", t.Name));
			};

			// Collect widget assemblies: both Myra core types and user-supplied custom widgets
			Dictionary<Assembly, string[]> assemblies = new Dictionary<Assembly, string[]>(ExtraWidgetAssembliesAndNamespaces);
			assemblies.Add(typeof(Widget).Assembly, new string[] { typeof(Widget).Namespace, typeof(PropertyGrid).Namespace });

			return new LoadContext
			{
				Assemblies = assemblies,
				LegacyClassNames = LegacyClassNames,
				ObjectCreator = (t, el) => CreateItem(t, el),
				ResourceGetter = resourceGetter
			};
		}

		/// <summary>
		/// Saves the project to an XML string.
		/// </summary>
		/// <returns>An XML string representation of the project.</returns>
		public string Save()
		{
			var saveContext = CreateSaveContext();
			var root = saveContext.Save(this);

			var xDoc = new XDocument(root);

			return xDoc.ToString();
		}

		/// <summary>
		/// Loads a project from an XDocument with an optional handler.
		/// If project has external stylesheet, temporarily switches to it during loading.
		/// </summary>
		/// <typeparam name="T">The type of the handler.</typeparam>
		/// <param name="xDoc">The XDocument to load from.</param>
		/// <param name="assetManager">The asset manager for loading resources. Required if the project has an external stylesheet.</param>
		/// <param name="handler">Optional handler for loading events.</param>
		/// <returns>The loaded project.</returns>
		public static Project LoadFromXml<T>(XDocument xDoc, AssetManager assetManager = null, T handler = null) where T : class
		{
			// Check if project specifies external stylesheet
			var stylesheet = Stylesheet.Current;
			var stylesheetPathAttr = xDoc.Root.Attribute("StylesheetPath");
			if (stylesheetPathAttr != null)
			{
				stylesheet = assetManager.LoadStylesheet(stylesheetPathAttr.Value);
			}

			var result = new Project();

			// If external stylesheet, temporarily switch to it for loading
			if (stylesheetPathAttr != null)
			{
				if (assetManager == null)
				{
					throw new Exception($"assetManager couldn't be null if the project has external stylesheet");
				}

				result.Stylesheet = stylesheet;
				using(var stylesheetChanger = new StylesheetChanger(stylesheet))
				{
					var loadContext = CreateLoadContext(assetManager);
					loadContext.Load(result, xDoc.Root, handler);
					result.ObjectsNodes = loadContext.ObjectsNodes;
				}
			}
			else
			{
				// Use current stylesheet
				var loadContext = CreateLoadContext(assetManager);
				loadContext.Load(result, xDoc.Root, handler);
				result.ObjectsNodes = loadContext.ObjectsNodes;
			}

			return result;
		}

		/// <summary>
		/// Loads a project from XML string data with an optional handler.
		/// </summary>
		/// <typeparam name="T">The type of the handler.</typeparam>
		/// <param name="data">The XML data as a string.</param>
		/// <param name="assetManager">The asset manager for loading resources.</param>
		/// <param name="handler">Optional handler for loading events.</param>
		/// <returns>The loaded project.</returns>
		public static Project LoadFromXml<T>(string data, AssetManager assetManager = null, T handler = null) where T : class
		{
			return LoadFromXml(XDocument.Parse(data, LoadOptions.SetLineInfo), assetManager, handler);
		}

		/// <summary>
		/// Loads a project from an XDocument.
		/// </summary>
		/// <param name="xDoc">The XDocument to load from.</param>
		/// <param name="assetManager">The asset manager for loading resources.</param>
		/// <returns>The loaded project.</returns>
		public static Project LoadFromXml(XDocument xDoc, AssetManager assetManager = null)
		{
			return LoadFromXml<object>(xDoc, assetManager, null);
		}

		/// <summary>
		/// Loads a project from XML string data.
		/// </summary>
		/// <param name="data">The XML data as a string.</param>
		/// <param name="assetManager">The asset manager for loading resources.</param>
		/// <returns>The loaded project.</returns>
		public static Project LoadFromXml(string data, AssetManager assetManager = null)
		{
			return LoadFromXml<object>(XDocument.Parse(data, LoadOptions.SetLineInfo), assetManager, null);
		}

		/// <summary>
		/// Loads a single object from XML string data.
		/// Determines object type from XML tag name, resolving legacy names and special types.
		/// </summary>
		/// <typeparam name="T">The type of the handler.</typeparam>
		/// <param name="data">The XML data as a string.</param>
		/// <param name="assetManager">The asset manager for loading resources.</param>
		/// <param name="stylesheet">The stylesheet to apply to loaded objects.</param>
		/// <param name="handler">Optional handler for loading events.</param>
		/// <param name="parentType">The parent type context for loading.</param>
		/// <returns>The loaded object.</returns>
		public static object LoadObjectFromXml<T>(string data, AssetManager assetManager = null, Stylesheet stylesheet = null, T handler = null, Type parentType = null) where T : class
		{
			XDocument xDoc = XDocument.Parse(data, LoadOptions.SetLineInfo);

			var name = xDoc.Root.Name.ToString();
			Type itemType;

			// Determine type from XML tag name
			if (name == "PropertyGrid")
			{
				itemType = typeof(PropertyGrid);
			}
			else if (!IsProportionName(name))
			{
				// Check if it's a legacy name and get modern name
				string newName;
				if (LegacyClassNames.TryGetValue(name, out newName))
				{
					name = newName;
				}

				// Look up widget type by name in Myra assemblies
				itemType = GetWidgetTypeByName(name);
			}
			else
			{
				// It's a Proportion (layout configuration)
				itemType = typeof(Proportion);
			}

			if (itemType == null)
			{
				return null;
			}

			// Create and load object, applying stylesheet context if provided
			object item = null;
			if (stylesheet != null)
			{
				using (var stylesheetChanger = new StylesheetChanger(stylesheet))
				{
					item = CreateItem(itemType, xDoc.Root);
					var loadContext = CreateLoadContext(assetManager);
					loadContext.Load(item, xDoc.Root, handler);
				}
			}
			else
			{
				item = CreateItem(itemType, xDoc.Root);
				var loadContext = CreateLoadContext(assetManager);
				loadContext.Load(item, xDoc.Root, handler);
			}

			return item;
		}

		/// <summary>
		/// Loads a single object from XML string data using the specified stylesheet.
		/// </summary>
		/// <param name="data">The XML data as a string.</param>
		/// <param name="assetManager">The asset manager for loading resources.</param>
		/// <param name="stylesheet">The stylesheet to apply to loaded objects.</param>
		/// <returns>The loaded object.</returns>
		public static object LoadObjectFromXml(string data, AssetManager assetManager, Stylesheet stylesheet)
		{
			return LoadObjectFromXml<object>(data, assetManager, stylesheet, null);
		}

		/// <summary>
		/// Loads a single object from XML string data using this project's stylesheet.
		/// </summary>
		/// <param name="data">The XML data as a string.</param>
		/// <param name="assetManager">The asset manager for loading resources.</param>
		/// <returns>The loaded object.</returns>
		public object LoadObjectFromXml(string data, AssetManager assetManager)
		{
			return LoadObjectFromXml(data, assetManager, Stylesheet);
		}

		/// <summary>
		/// Saves an object to an XML string using this project's stylesheet.
		/// Serializes only properties that differ from stylesheet defaults.
		/// </summary>
		/// <param name="obj">The object to save.</param>
		/// <param name="tagName">The XML tag name for the object.</param>
		/// <param name="parentType">The parent type context for saving.</param>
		/// <returns>An XML string representation of the object.</returns>
		public string SaveObjectToXml(object obj, string tagName, Type parentType)
		{
			var saveContext = CreateSaveContext(Stylesheet);
			return saveContext.Save(obj, true, tagName, parentType).ToString();
		}

		// Instantiates an object of the given type, handling special case of Widget constructors that accept StyleName parameter
		private static object CreateItem(Type type, XElement element)
		{
			if (typeof(Widget).IsAssignableFrom(type))
			{
				// Check if widget constructor accepts a style name parameter (string)
				var acceptsStyleName = false;
				foreach (var c in type.GetConstructors())
				{
					var p = c.GetParameters();
					if (p != null && p.Length == 1)
					{
						if (p[0].ParameterType == typeof(string))
						{
							acceptsStyleName = true;
							break;
						}
					}
				}

				if (acceptsStyleName)
				{
					// Extract StyleName from XML attribute, defaulting if not found or invalid
					var styleName = Stylesheet.DefaultStyleName;
					var styleNameAttr = element.Attribute("StyleName");
					if (styleNameAttr != null)
					{
						var stylesNames = Stylesheet.Current.GetStylesByWidgetName(type.Name);
						if (stylesNames != null && stylesNames.Contains(styleNameAttr.Value))
						{
							styleName = styleNameAttr.Value;
						}
						else
						{
							// Remove invalid style name attribute
							styleNameAttr.Remove();
						}
					}

					// Create widget with style name parameter
					return (Widget)Activator.CreateInstance(type, styleName);
				}
			}

			// Create non-widget object or widget without style parameter
			return Activator.CreateInstance(type);
		}

		// Checks if widget property value matches the value defined in the stylesheet.
		// Used to skip serializing properties that are already defined by the applied style.
		private static bool HasStylesheetValue(Widget w, PropertyInfo property, Stylesheet stylesheet)
		{
			if (stylesheet == null)
			{
				return false;
			}

			// Get style name: use widget's style or default
			var styleName = w.StyleName;
			if (string.IsNullOrEmpty(styleName))
			{
				styleName = Stylesheet.DefaultStyleName;
			}

			// Determine the styles dictionary property name for this widget type
			var typeName = w.GetType().Name;
			var styleTypeNameAttribute = w.GetType().FindAttribute<StyleTypeNameAttribute>();
			if (styleTypeNameAttribute != null)
			{
				typeName = styleTypeNameAttribute.Name;
			}

			// Get the stylesheet's Styles collection for this widget type
			var stylesDictPropertyName = typeName + "Styles";
			var stylesDictProperty = stylesheet.GetType().GetRuntimeProperty(stylesDictPropertyName);
			if (stylesDictProperty == null)
			{
				return false;
			}

			var stylesDict = (IDictionary)stylesDictProperty.GetValue(stylesheet);
			if (stylesDict == null)
			{
				return false;
			}

			// Get the style object, fallback to default if style name not found
			if (!stylesDict.Contains(styleName))
			{
				styleName = Stylesheet.DefaultStyleName;
			}

			object obj = stylesDict[styleName];

			// Navigate to the property in stylesheet using reflection (supports nested paths)
			PropertyInfo styleProperty = null;

			var stylePropertyPathAttribute = property.FindAttribute<StylePropertyPathAttribute>();
			if (stylePropertyPathAttribute != null)
			{
				// Custom path specified (e.g., "/SomeProperty/NestedProperty")
				var path = stylePropertyPathAttribute.Name;
				if (path.StartsWith("/"))
				{
					obj = stylesheet;
					path = path.Substring(1);
				}

				// Traverse path segments separated by '/'
				var parts = path.Split('/');
				for (var i = 0; i < parts.Length; ++i)
				{
					styleProperty = obj.GetType().GetRuntimeProperty(parts[i]);

					if (i < parts.Length - 1)
					{
						obj = styleProperty.GetValue(obj);
					}
				}
			}
			else
			{
				// Use property name directly
				styleProperty = obj.GetType().GetRuntimeProperty(property.Name);
			}

			if (styleProperty == null)
			{
				return false;
			}

			// Compare values: if they match, property is inherited from stylesheet
			var styleValue = styleProperty.GetValue(obj);
			var value = property.GetValue(w);
			if (!Equals(styleValue, value))
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Gets the widget type by its name.
		/// Resolves by looking up the type in the Myra.Graphics2D.UI namespace.
		/// </summary>
		/// <param name="name">The name of the widget type.</param>
		/// <returns>The widget type, or null if not found.</returns>
		public static Type GetWidgetTypeByName(string name)
		{
			// Look up type in Widget's namespace and assembly
			var itemNamespace = typeof(Widget).Namespace;
			return typeof(Widget).Assembly.GetType(itemNamespace + "." + name);
		}
	}
}