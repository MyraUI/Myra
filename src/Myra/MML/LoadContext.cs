using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Myra.Attributes;
using FontStashSharp;
using Myra.Utility;
using FontStashSharp.RichText;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.MML
{
	// Deserializes objects from XML elements to .NET objects.
	// Handles properties, events, resources, enums, colors, and complex nested structures.
	internal class LoadContext: BaseContext
	{
		// Wrapper for either regular property or attached property with unified interface
		struct SimplePropertyInfo
		{
			public PropertyInfo Property;  // Regular property info, or null if attached
			public BaseAttachedPropertyInfo AttachedProperty;  // Attached property info, or null if regular
			public string Name;  // Property name
			public Type PropertyType;  // Property's type

			public SimplePropertyInfo(PropertyInfo property)
			{
				Property = property;
				AttachedProperty = null;

				Name = property.Name;
				PropertyType = property.PropertyType;
			}

			public SimplePropertyInfo(BaseAttachedPropertyInfo property)
			{
				Property = null;
				AttachedProperty = property;

				Name = property.Name;
				PropertyType = property.PropertyType;
			}

			// Sets value on either regular or attached property
			public void SetValue(object obj, object value)
			{
				if (Property != null)
				{
					Property.SetValue(obj, value);
				} else if (AttachedProperty != null && obj is BaseObject)
				{
					AttachedProperty.SetValueObject((BaseObject)obj, value);
				}
			}
		}

		// Backward compatibility: maps old class names to new names (e.g., "TextField" → "TextBox")
		public Dictionary<string, string> LegacyClassNames = null;

		// Backward compatibility: maps old property names to new names
		public Dictionary<string, string> LegacyPropertyNames = null;

		// Named color palette for resolving color names in XML (e.g., "Red", "Blue")
		public Dictionary<string, Color> Colors;

		// XML element names to skip during deserialization
		public HashSet<string> NodesToIgnore = null;

		// Factory function for instantiating objects from XML elements (allows custom construction)
		public Func<Type, XElement, object> ObjectCreator = (type, el) => Activator.CreateInstance(type);

		// Widget assemblies and namespaces to search when looking up widget types by name
		public Dictionary<Assembly, string[]> Assemblies;

		// Loads external resources (brushes, fonts, textures) by name using asset manager
		public Func<Type, string, object> ResourceGetter = null;

		// Mapping of deserialized objects to their source XML elements (for debugging/position tracking)
		public readonly List<Tuple<object, XElement>> ObjectsNodes = new List<Tuple<object, XElement>>();

		private const string UserDataAttributePrefix = "_";  // Prefix for custom user attributes

		// Deserializes an object from XML element, recursively loading children and properties
		public void Load<T>(object obj, XElement el, T handler) where T : class
		{
			// Track object and its source XML for debugging/introspection
			ObjectsNodes.Add(new Tuple<object, XElement>(obj, el));

			var type = obj.GetType();
			var handlerType = typeof(T);

			var baseObject = obj as BaseObject;

			// Separate properties into simple (attributes) and complex (elements)
			List<PropertyInfo> complexProperties, simpleProperties;
			ParseProperties(type, false, out complexProperties, out simpleProperties);

			// Process XML attributes as simple properties
			string newName;
			foreach (var attr in el.Attributes())
			{
				var propertyName = attr.Name.ToString();

				// Apply legacy name mapping for backward compatibility
				if (LegacyPropertyNames != null && LegacyPropertyNames.TryGetValue(propertyName, out newName))
				{
					propertyName = newName;
				}

				SimplePropertyInfo? simplePropertyInfo = null;

				if (propertyName.Contains("."))
				{
					// Attached property: "ClassName.PropertyName" syntax
					var parts = propertyName.Split('.');
					if (parts.Length != 2)
					{
						throw new Exception($"Couldn't parse attached property {propertyName}");
					}
					var parentType = Project.GetWidgetTypeByName(parts[0].Trim());
					if (parentType == null)
					{
						throw new Exception($"Couldn't find type {parts[0].Trim()} for attached property {propertyName}");
					}

					var properties = AttachedPropertiesRegistry.GetPropertiesOfType(parentType);
					var property = (from p in properties where p.Name == parts[1].Trim() select p).FirstOrDefault();
					if (property == null)
					{
						throw new Exception($"Type {parentType.Name} doesn't have attached property {parts[1].Trim()}");
					}

					simplePropertyInfo = new SimplePropertyInfo(property);
				} else
				{
					// Regular property
					var property = (from p in simpleProperties where p.Name == propertyName select p).FirstOrDefault();
					if (property != null)
					{
						simplePropertyInfo = new SimplePropertyInfo(property);
					}
				}

				if (simplePropertyInfo != null)
				{
					// Deserialize property value based on type
					object value = null;

					var propertyType = simplePropertyInfo.Value.PropertyType;
					var serializer = FindSerializer(propertyType);
					if (serializer != null)
					{
						// Custom serializer (e.g., for Vector2, Rectangle)
						value = serializer.Deserialize(attr.Value);
					} else
					if (propertyType.IsEnum ||
						propertyType.IsNullableEnum())
					{
						// Enum parsing
						if (propertyType.IsNullableEnum())
						{
							propertyType = propertyType.GetNullableType();
						}
						value = Enum.Parse(propertyType, attr.Value);
					}
					else if (propertyType == typeof(Color) || propertyType == typeof(Color?))
					{
						// Color parsing: named or built-in
						Color color;
						if (Colors != null && Colors.TryGetValue(attr.Value, out color))
						{
							value = color;
						}
						else
						{
							value = ColorStorage.FromName(attr.Value);
							if (value == null)
							{
								throw new Exception(string.Format("Could not find parse color '{0}'", attr.Value));
							}
						}
					}
					else if ((typeof(IBrush).IsAssignableFrom(propertyType) ||
							 propertyType == typeof(SpriteFontBase)) &&
							 !string.IsNullOrEmpty(attr.Value) &&
							 ResourceGetter != null)
					{
						// Resource loading: brushes, fonts, textures
						try
						{
							var texture = ResourceGetter(propertyType, attr.Value);
							if (texture == null)
							{
								throw new Exception(string.Format("Could not find resource '{0}'", attr.Value));
							}
							value = texture;

							// Track resource name for serialization
							if (baseObject != null)
							{
								baseObject.Resources[simplePropertyInfo.Value.Name] = attr.Value;
							}
						}
						catch (Exception)
						{
						}
					}
					else
					{
						// Primitive type conversion (int, float, string, etc.)
						if (propertyType.IsNullablePrimitive())
						{
							propertyType = propertyType.GetNullableType();
						}

						value = Convert.ChangeType(attr.Value, propertyType, CultureInfo.InvariantCulture);
					}

					simplePropertyInfo.Value.SetValue(obj, value);
				}
				else if (handler != null && type.GetEvent(attr.Name.LocalName) != null)
				{
					// Event handler wiring: attribute is method name on handler object
					var method = handlerType.GetMethod(attr.Value, BindingFlags.Public | BindingFlags.Instance);
					var eventHandler = type.GetEvent(attr.Name.LocalName);
					if (method == null)
					{
						throw new InvalidOperationException($"Handler of type '{handlerType}' does not contain method '{attr.Value}'. If it does, ensure the method is both public and non-static.");
					}

					var delegateMethod = method.CreateDelegate(eventHandler.EventHandlerType, handler);
					eventHandler.AddEventHandler(obj, delegateMethod);
				}
				else
				{
					// Custom user attributes with "_" prefix
					if (propertyName.StartsWith(UserDataAttributePrefix) && baseObject != null)
					{
						baseObject.UserData.Add(propertyName, attr.Value);
					}
				}
			}
			

			// Find content property: [Content] marked property for implicit child addition
			var contentProperty = (from p in complexProperties
								   where p.FindAttribute<ContentAttribute>()
								   != null select p).FirstOrDefault();

			// Process XML child elements
			foreach (var child in el.Elements())
			{
				var childName = child.Name.ToString();

				// Skip explicitly ignored node names
				if (NodesToIgnore != null && NodesToIgnore.Contains(childName))
				{
					continue;
				}

				var isProperty = false;
				if (childName.Contains("."))
				{
					// Property element: "ClassName.PropertyName" or "PropertyName" syntax
					var parts = childName.Split('.');
					childName = parts[1];
					isProperty = true;
				}

				// Apply legacy property name mapping
				if (LegacyPropertyNames != null && LegacyPropertyNames.TryGetValue(childName, out newName))
				{
					childName = newName;
				}

				// Try to match property name
				var property = (from p in complexProperties where p.Name == childName select p).FirstOrDefault();
				if (property != null)
				{
					// Handle different property types: List, Dict, or single object
					do
					{
						var value = property.GetValue(obj);
						var asList = value as IList;
						if (asList != null)
						{
							// List property: each child element is a list item
							foreach (var child2 in child.Elements())
							{
								var item = ObjectCreator(property.PropertyType.GenericTypeArguments[0], child2);
								Load(item, child2, handler);
								asList.Add(item);
							}

							break;
						}

						var asDict = value as IDictionary;
						if (asDict != null)
						{
							// Dictionary property: each child element is a dict value with optional id key
							foreach (var child2 in child.Elements())
							{
								var item = ObjectCreator(property.PropertyType.GenericTypeArguments[1], child2);
								Load(item, child2, handler);

								var id = string.Empty;
								if (child2.Attribute(IdName) != null)
								{
									id = child2.Attribute(IdName).Value;
								}

								asDict[id] = item;
							}

							break;
						}

						// Single object property
						if (property.SetMethod == null)
						{
							// Read-only property: load into existing value
							Load(value, child, handler);
						}
						else
						{
							// Writable property: create and assign new value
							var newValue = ObjectCreator(property.PropertyType, child);
							Load(newValue, child, handler);
							property.SetValue(obj, newValue);
						}
						break;
					} while (true);
				}
				else
				{
					// Not a property: must be a widget class name
					if (isProperty)
					{
						throw new Exception(string.Format("Class {0} doesnt have property {1}", type.Name, childName));
					}

					// Look up widget type by name (with legacy name support)
					var widgetName = childName;
					if (LegacyClassNames != null && LegacyClassNames.TryGetValue(widgetName, out newName))
					{
						widgetName = newName;
					}

					// Search for widget type in configured assemblies and namespaces
					Type itemType = null;
					foreach (var pair in Assemblies)
					{
						foreach (var ns in pair.Value)
						{
							var widgetType = pair.Key.GetType(ns + "." + widgetName);
							if (widgetType != null)
							{
								itemType = widgetType;
								break;
							}
						}

						if (itemType != null)
							break;
					}

					if (itemType != null)
					{
						// Create and load widget, then add to content property
						var item = ObjectCreator(itemType, child);
						Load(item, child, handler);

						if (contentProperty == null)
						{
							throw new Exception(string.Format("Class {0} lacks property marked with ContentAttribute", type.Name));
						}

						var containerValue = contentProperty.GetValue(obj);
						var asList = containerValue as IList;
						if (asList != null)
						{
							// Content property is a list
							asList.Add(item);
						} else
						{
							// Content property is a single value
							contentProperty.SetValue(obj, item);
						}
					}
					else
					{
						throw new Exception(string.Format("Could not resolve tag '{0}'", widgetName));
					}
				}
			}
		}
	}
}
