using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using FontStashSharp;
using FontStashSharp.RichText;
using Microsoft.Xna.Framework;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Myra.MML;
using Myra.Utility;

namespace MyraPad
{
	/// <summary>
	/// Exports a Myra UI project to C# code. Generates two files: a main file (with initialization code placeholder)
	/// and a designer file (with generated widget hierarchy and property initialization). Supports both full C# export
	/// (with proper indentation) and lightweight export (minimal formatting for copy-paste). Recursively processes
	/// the widget hierarchy, generates unique variable names, handles attached properties, and converts property values
	/// to C# literals using CodeDom.
	/// </summary>
	public class ExporterCS : IDisposable
	{
		// The project being exported
		private readonly Project _project;
		// Dictionary to track generated variable names and their count for auto-generated IDs
		private readonly Dictionary<string, int> ids = new Dictionary<string, int>();
		// Accumulates generated C# field declarations
		private readonly StringBuilder sbFields = new StringBuilder();
		// Accumulates generated C# initialization code for the Build method
		private readonly StringBuilder sbBuild = new StringBuilder();
		// Helper to convert primitive values to C# code literals
		private readonly PrimitiveConverter converter = new PrimitiveConverter();
		// Tracks whether the current item is the first (for formatting)
		private bool isFirst = true;

		/// <summary>
		/// Initializes a new instance of the ExporterCS class with the project to export
		/// </summary>
		public ExporterCS(Project project)
		{
			if (project == null)
			{
				throw new ArgumentNullException("project");
			}

			_project = project;
		}

		/// <summary>
		/// The fully qualified export output path with environment variables expanded
		/// </summary>
		private string ExportPath
		{
			get
			{
				return Environment.ExpandEnvironmentVariables(_project.ExportOptions.OutputPath);
			}
		}

		/// <summary>
		/// Exports the main C# class file with a placeholder for user code initialization.
		/// Returns empty string if the file already exists to avoid overwriting user modifications.
		/// </summary>
		public string ExportMain()
		{
			var path = Path.Combine(ExportPath, _project.ExportOptions.Class + ".cs");
			// Don't overwrite the main file if it already exists (user may have modified it)
			if (File.Exists(path))
			{
				return string.Empty;
			}

			// Use custom template if provided, otherwise use default
			var template = string.IsNullOrWhiteSpace(_project.ExportOptions.TemplateMain) ?
				Resources.ExportCSMain :
				File.ReadAllText(_project.ExportOptions.TemplateMain);

			// Replace template placeholders with actual export values
			template = template.Replace("$namespace$", _project.ExportOptions.Namespace);
			template = template.Replace("$class$", _project.ExportOptions.Class);
			template = template.Replace("$generationDate$", DateTime.Now.ToString());

			File.WriteAllText(path, template);

			return path;
		}

		// Converts the first character of a string to lowercase (e.g., "Button" -> "button")
		private static string LowercaseFirstLetter(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return s;
			}

			return char.ToLowerInvariant(s[0]) + s.Substring(1);
		}

		/// <summary>
		/// Recursively exports a widget and all its children to C# code, generating variable declarations,
		/// instantiations, and property assignments. Returns the variable name/ID of the created widget.
		/// </summary>
		public string ExportDesignerRecursive(IItemWithId w, IItemWithId parent, bool light)
		{
			var properties = BuildProperties(w.GetType());
			var simpleProperties = new List<PropertyInfo>();

			// Process widget properties into child widgets and simple properties
			var subItems = new List<string>();
			string styleName = string.Empty;
			foreach (var property in properties)
			{
				var value = property.GetValue(w);

				// Handle StyleName specially (it's passed as constructor argument)
				if (property.Name == "StyleName")
				{
					styleName = (string)value;
					continue;
				}

				if (value == null)
				{
					simpleProperties.Add(property);
					continue;
				}

				// Check if this property holds a single child widget
				if (value is IItemWithId)
				{
					var subItemId = ExportDesignerRecursive((IItemWithId)value, w, light);
					var subItemCode = string.Format("{0} = {1}", property.Name, subItemId);
					subItems.Add(subItemCode);
				}
				else
				{
					// Check if this property is a list of child widgets
					var type = value.GetType();
					var listInterface = (from i in type.GetInterfaces()
										 where i.IsGenericType &&
										 i.GetGenericTypeDefinition() == typeof(IList<>)
										 select i).FirstOrDefault();

					if (listInterface != null &&
						typeof(IItemWithId).IsAssignableFrom(listInterface.GetGenericArguments()[0]))
					{
						// Export each child widget in the collection
						var asEnumerable = value as IEnumerable;
						foreach (var comp in asEnumerable)
						{
							var subItemId = ExportDesignerRecursive((IItemWithId)comp, w, light);
							var subItemCode = string.Format("{0}.Add({1})", property.Name, subItemId);
							subItems.Add(subItemCode);
						}
					}
					else
					{
						// Regular property
						simpleProperties.Add(property);
					}
				}
			}

			// Add formatting between statements (skip for first item)
			if (!isFirst)
			{
				if (!light)
				{
					sbBuild.Append("\n\n\t\t\t");
				}
				else
				{
					sbBuild.Append("\n\n");
				}
			}

			isFirst = false;

			// Determine the variable ID/name for this widget
			string id;
			var typeName = w.GetType().GetFriendlyName();
			if (_project.Root == w && !light)
			{
				// Root widget in full export doesn't need a variable (used directly)
				id = string.Empty;
			}
			else
			{
				id = w.Id;
				if (string.IsNullOrEmpty(id))
				{
					// Auto-generate unique variable name based on widget type
					var onlyTypeName = LowercaseFirstLetter(w.GetType().GetOnlyTypeName());
					int count;
					if (!ids.TryGetValue(onlyTypeName, out count))
					{
						count = 1;
					}
					else
					{
						++count;
					}
					ids[onlyTypeName] = count;

					id = onlyTypeName + count;
				}

				// Generate variable declaration with appropriate keyword
				if (string.IsNullOrEmpty(w.Id) || light)
				{
					sbBuild.Append("var " + id);
				}
				else
				{
					sbBuild.Append(w.Id);
				}
			}

			// Generate the object instantiation statement
			var idPrefix = string.IsNullOrEmpty(id) ? string.Empty : id + ".";
			if (!string.IsNullOrEmpty(id))
			{
				sbBuild.Append(" = new " + typeName + "(" +
					(string.IsNullOrEmpty(styleName) ? string.Empty : ("\"" + styleName + "\"")) + ");");
			}

			// Generate public field declarations for named widgets
			if (!string.IsNullOrEmpty(w.Id) && _project.Root != w)
			{
				if (!isFirst)
				{
					sbFields.Append("\n\t\t");
				}
				sbFields.Append("public " + w.GetType().Name + " " + w.Id + ";");
			}

			// Generate property assignments for simple properties
			foreach (var property in simpleProperties)
			{
				if (!_project.ShouldSerializeProperty(w, property))
				{
					continue;
				}

				var propertyCode = BuildPropertyCode(property, w, idPrefix, light);
				if (!string.IsNullOrEmpty(propertyCode))
				{
					sbBuild.Append(propertyCode);
				}
			}

			// Generate attached property assignments from parent
			var asBaseObject = w as BaseObject;
			if (parent != null && asBaseObject != null)
			{
				var attachedProperties = AttachedPropertiesRegistry.GetPropertiesOfType(parent.GetType());
				foreach (var property in attachedProperties)
				{
					var value = property.GetValueObject(asBaseObject);
					if (value != null && !value.Equals(property.DefaultValueObject))
					{
						value = BuildValue(value, converter);

						sbBuild.Append("\n");
						if (!light)
						{
							sbBuild.Append("\t\t\t");
						}

						sbBuild.Append($"{property.OwnerType.Name}.Set{property.Name}({id}, {value});");
					}
				}
			}

			// Generate child widget assignments and additions
			foreach (var subItem in subItems)
			{
				sbBuild.Append("\n");
				if (!light)
				{
					sbBuild.Append("\t\t\t");
				}

				sbBuild.Append(idPrefix);
				sbBuild.Append(subItem);
				sbBuild.Append(";");
			}

			return id;
		}

		/// <summary>
		/// Processes the designer template by replacing placeholders with generated code and export options
		/// </summary>
		public string ExportDesignerCode(string template, bool light)
		{
			// Replace metadata placeholders in the template
			template = template.Replace("$namespace$", _project.ExportOptions.Namespace);
			template = template.Replace("$class$", _project.ExportOptions.Class);
			template = template.Replace("$parentClass$", _project.Root.GetType().Name);
			template = template.Replace("$generationDate$", DateTime.Now.ToString());

			// Clear state from previous exports
			ids.Clear();
			sbFields.Clear();
			sbBuild.Clear();

			isFirst = true;
			// Recursively export the widget hierarchy
			ExportDesignerRecursive(_project.Root, null, light);

			// Replace code placeholders with the generated code
			template = template.Replace("$fields$", sbFields.ToString());
			template = template.Replace("$build$", sbBuild.ToString());

			return template;
		}

		/// <summary>
		/// Exports the designer file containing generated widget hierarchy and property initialization code
		/// </summary>
		public string ExportDesigner()
		{
			// Load designer template (custom or default)
			var template = string.IsNullOrWhiteSpace(_project.ExportOptions.TemplateDesigner) ?
				Resources.ExportCSDesigner :
				File.ReadAllText(_project.ExportOptions.TemplateDesigner);

			// Generate the designer code
			template = ExportDesignerCode(template, false);

			// Write the generated designer file
			var path = Path.Combine(ExportPath, _project.ExportOptions.Class + ".Generated.cs");
			File.WriteAllText(path, template);

			return path;
		}

		// Filters and collects public properties from a type that should be serialized (excluding ignored/obsolete properties)
		private static List<PropertyInfo> BuildProperties(Type type)
		{
			var result = new List<PropertyInfo>();
			var properties = from p in type.GetProperties() select p;
			foreach (var property in properties)
			{
				// Only include properties that have a public getter and are not static
				if (property.GetGetMethod() == null ||
					!property.GetGetMethod().IsPublic ||
					property.GetGetMethod().IsStatic)
				{
					continue;
				}

				// Skip properties marked with XmlIgnore
				var jsonIgnoreAttr = property.FindAttribute<XmlIgnoreAttribute>();
				if (jsonIgnoreAttr != null)
				{
					continue;
				}

				// Skip properties marked as obsolete
				var obsoleteAttr = property.FindAttribute<ObsoleteAttribute>();
				if (obsoleteAttr != null)
				{
					continue;
				}

				result.Add(property);
			}

			return result;
		}

		// Generates C# code for property assignments, handling resources, brushes, special types, and collections
		private string BuildPropertyCode(PropertyInfo property, object o, string idPrefix, bool light)
		{
			var sb = new StringBuilder();

			var value = property.GetValue(o);

			var asList = value as IList;
			if (asList == null)
			{
				// Handle single value properties
				string strValue = null;

				// Special handling for resource-based properties (brushes, fonts)
				if (typeof(IBrush).IsAssignableFrom(property.PropertyType) ||
					property.PropertyType == typeof(SpriteFontBase))
				{
					var baseObject = o as BaseObject;
					string s;
					// Check if the resource path is stored in the widget's resources dictionary
					if (baseObject != null && baseObject.Resources.TryGetValue(property.Name, out s))
					{
						var typeName = property.PropertyType.Name;
						if (typeof(IImage).IsAssignableFrom(property.PropertyType))
						{
							typeName = "TextureRegion";
						}

						// Generate the asset manager load call
						if (property.PropertyType != typeof(IBrush))
						{
							if (typeName == "SpriteFontBase")
							{
								typeName = "Font";
							}
							strValue = "MyraEnvironment.DefaultAssetManager.Load" + typeName + "(\"" + s + "\")";
						}
						else
						{
							strValue = "new SolidBrush(\"" + s + "\")";
						}
					}
				}
				else if (property.PropertyType == typeof(Thickness))
				{
					// Special handling for Thickness struct
					var thickness = (Thickness)value;
					strValue = "new Thickness(" + thickness.ToString() + ")";
				}
				else
				{
					// Convert the value to a C# literal
					strValue = BuildValue(value, converter);
				}

				if (strValue == null)
				{
					return null;
				}

				// Generate the property assignment statement
				sbBuild.Append("\n");
				if (!light)
				{
					sbBuild.Append("\t\t\t");
				}
				sb.Append(idPrefix + property.Name);
				sb.Append(" = ");
				sb.Append(strValue);
				sb.Append(";");
			}
			else
			{
				// Handle collection properties - generate Add() calls for each item
				foreach (var comp in asList)
				{
					sbBuild.Append("\n");
					if (!light)
					{
						sbBuild.Append("\t\t\t");
					}

					sb.Append(idPrefix + property.Name);
					sb.Append(".Add(");
					sb.Append(BuildValue(comp, converter));
					sb.Append(");");
				}
			}

			return sb.ToString();
		}

		// Converts an object value to its C# code representation (literals, constructors, enums, etc.)
		private static string BuildValue(object value, PrimitiveConverter converter)
		{
			if (value == null)
			{
				return "null";
			}

			// Special handling for Color values
			if (value is Color)
			{
				var name = ((Color)value).GetColorName();
				if (!string.IsNullOrEmpty(name))
				{
					// Use predefined color name if available (e.g., Color.Red)
					return "Color." + name;
				}
				else
				{
					// Generate ColorStorage.CreateColor() call for custom colors
					var c = (Color)value;
					return string.Format("ColorStorage.CreateColor({0}, {1}, {2}, {3})", (int)c.R, (int)c.G, (int)c.B, (int)c.A);
				}
			}

			// Convert primitive types and strings to C# literals using CodeDom
			if (value is string || value.GetType().IsPrimitive)
			{
				return converter.PrimitiveToLiteral(value);
			}

			var sb = new StringBuilder();
			// Handle enum values
			if (value.GetType().IsEnum)
			{
				sb.Append(value.GetType());
				sb.Append(".");
				sb.Append(value);
				return sb.ToString().Replace("+", ".");
			}

			// Generate object initializer for complex types
			sb.Append("new " + value.GetType().GetFriendlyName());

			var isEmpty = true;
			var properties = from p in value.GetType().GetProperties() select p;
			foreach (var property in properties)
			{
				// Only include public readable/writable properties
				if (property.GetGetMethod() == null ||
					property.GetSetMethod() == null ||
					!property.GetGetMethod().IsPublic ||
					property.GetGetMethod().IsStatic)
				{
					continue;
				}

				// Skip properties marked with XmlIgnore
				var jsonIgnoreAttribute = property.FindAttribute<XmlIgnoreAttribute>();
				if (jsonIgnoreAttribute != null)
				{
					continue;
				}

				var subValue = property.GetValue(value);

				// Skip properties that have their default values
				if (property.HasDefaultValue(subValue))
				{
					continue;
				}

				// Skip internal/unused properties
				if (value.GetType().Name == "Color" && property.Name == "PackedValue")
				{
					continue;
				}

				// Generate the initializer block on first property
				if (isEmpty)
				{
					sb.Append("\n\t\t\t{");
					isEmpty = false;
				}

				// Add property assignment
				sb.Append("\n\t\t\t\t" + property.Name);
				sb.Append(" = ");
				sb.Append(BuildValue(subValue, converter));
				sb.Append(",");
			}

			// Close initializer or add empty constructor
			if (!isEmpty)
			{
				sb.Append("\n\t\t\t}");
			}
			else
			{
				sb.Append("()");
			}

			return sb.ToString();
		}

		/// <summary>
		/// Main export entry point that validates options and exports both main and designer files
		/// </summary>
		public string[] Export()
		{
			// Validate required export options
			if (string.IsNullOrEmpty(_project.ExportOptions.Namespace))
			{
				throw new Exception("Namespace could not be empty.");
			}

			if (string.IsNullOrEmpty(_project.ExportOptions.Class))
			{
				throw new Exception("Class could not be empty.");
			}

			if (string.IsNullOrEmpty(_project.ExportOptions.OutputPath))
			{
				throw new Exception("Output path could not be empty.");
			}

			var result = new List<string>();

			// Export the main C# file (if it doesn't already exist)
			var path = ExportMain();
			if (!string.IsNullOrEmpty(path))
			{
				result.Add(path);
			}

			// Export the designer file (always generated)
			path = ExportDesigner();
			if (!string.IsNullOrEmpty(path))
			{
				result.Add(path);
			}

			return result.ToArray();
		}

		public void Dispose() => converter.Dispose();
	}

	/// <summary>
	/// Converts primitive values to C# code literals using CodeDom. Handles special cases like floats
	/// which need trailing 'f' removed for compatibility.
	/// </summary>
	class PrimitiveConverter : IDisposable
	{
		// CodeDom provider for generating C# code
		private readonly CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");

		/// <summary>
		/// Converts a primitive value (string, number, boolean, etc.) to its C# literal representation
		/// </summary>
		public string PrimitiveToLiteral(object input)
		{
			using (var writer = new StringWriter())
			{
				// Use CodeDom to generate a C# literal for the primitive value
				provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
				var result = writer.ToString();

				// CodeDom adds a trailing 'f' for floats, but we want to remove it for consistency
				if (input is float && !string.IsNullOrEmpty(result))
				{
					result = result.ToLower();
					if (result.EndsWith("f"))
					{
						result = result.Substring(0, result.Length - 1);
					}
				}

				return result;
			}
		}

		public void Dispose() => provider.Dispose();
	}
}