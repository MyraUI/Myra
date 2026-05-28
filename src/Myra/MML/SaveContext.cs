using Myra.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Myra.Attributes;
using System.Linq;
using Myra.Graphics2D;
using FontStashSharp;
using FontStashSharp.RichText;
using info.lundin.math;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.MML
{
	// Serializes .NET objects to XML elements.
	// Mirror of LoadContext: converts objects back to XML for saving UI projects.
	// Omits properties with default values to minimize file size.
	internal class SaveContext: BaseContext
	{
		// Predicate to determine if a property should be serialized.
		// Defaults to HasDefaultValue: skips properties with default values.
		// Can be overridden per context (e.g., Project.ShouldSerializeProperty for stylesheet filtering).
		public Func<object, PropertyInfo, bool> ShouldSerializeProperty = HasDefaultValue;

		// Converts property value to string for XML serialization.
		// Handles custom serializers, colors, resources, and primitives.
		private static string SaveSimpleProperty(BaseObject baseObject, object value, Type propertyType, string propertyName)
		{
			string str = null;

			var serializer = FindSerializer(propertyType);
			if (serializer != null)
			{
				// Custom serializer (e.g., for Vector2, Rectangle)
				str = serializer.Serialize(value);
			}
			else if (propertyType == typeof(Color?))
			{
				// Nullable color: convert to hex string
				str = ((Color?)value).Value.ToHexString();
			}
			else if (propertyType == typeof(Color))
			{
				// Non-nullable color: convert to hex string
				str = ((Color)value).ToHexString();
			}
			else if (typeof(IBrush).IsAssignableFrom(propertyType) || propertyType == typeof(SpriteFontBase))
			{
				// Resource (brush or font): use resource name tracked during load
				if (baseObject != null)
				{
					baseObject.Resources.TryGetValue(propertyName, out str);
				}
			}
			else
			{
				// Primitive type: use culture-invariant string conversion
				str = Convert.ToString(value, CultureInfo.InvariantCulture);
			}

			return str;
		}

		// Serializes an object to an XML element, recursively handling child objects.
		// Only includes properties that pass ShouldSerializeProperty filter (typically non-default values).
		public XElement Save(object obj, bool skipComplex = false, string tagName = null, Type parentType = null)
		{
			var type = obj.GetType();

			var baseObject = obj as BaseObject;

			// Separate properties into simple (attributes) and complex (child elements)
			List<PropertyInfo> complexProperties, simpleProperties;
			ParseProperties(type, true, out complexProperties, out simpleProperties);

			// Create root element with tag name or type name
			var el = new XElement(tagName ?? type.Name);

			// Phase 1: Serialize simple properties as XML attributes
			foreach (var property in simpleProperties)
			{
				// Skip if property shouldn't be serialized (e.g., has default value)
				if (!ShouldSerializeProperty(obj, property))
				{
					continue;
				}

				// Skip obsolete properties (for backward compatibility: don't save old properties)
				var attr = property.FindAttribute<ObsoleteAttribute>();
				if (attr != null)
				{
					continue;
				}

				var value = property.GetValue(obj);
				if (value != null)
				{
					string str = SaveSimpleProperty(baseObject, value, property.PropertyType, property.Name);
					if (!string.IsNullOrEmpty(str))
					{
						el.Add(new XAttribute(property.Name, str));
					}
				}
			}

			// Phase 2: Serialize attached properties (if this is a BaseObject with parent context)
			if (baseObject != null && parentType != null)
			{
				var attachedProperties = AttachedPropertiesRegistry.GetPropertiesOfType(parentType);
				foreach (var property in attachedProperties)
				{
					var value = property.GetValueObject(baseObject);
					// Only serialize if non-default
					if (value != null && !value.Equals(property.DefaultValueObject))
					{
						var propertyName = property.OwnerType.Name + "." + property.Name;
						var str = SaveSimpleProperty(baseObject, value,
							property.PropertyType, propertyName);
						if (!string.IsNullOrEmpty(str))
						{
							el.Add(new XAttribute(propertyName, str));
						}
					}
				}
			}

			// Phase 3: Serialize complex properties (if not skipped)
			if (!skipComplex)
			{
				// Find [Content] property for implicit children
				var contentProperty = (from p in complexProperties
									   where p.FindAttribute<ContentAttribute>()
									   != null
									   select p).FirstOrDefault();

				foreach (var property in complexProperties)
				{
					// Skip if property shouldn't be serialized
					if (!ShouldSerializeProperty(obj, property))
					{
						continue;
					}

					var value = property.GetValue(obj);
					if (value == null)
					{
						continue;
					}

					var propertyName = type.Name + "." + property.Name;
					var isContent = property == contentProperty;
					var asList = value as IList;

					if (asList == null)
					{
						// Single object property: recursively serialize as child element
						// If it's the content property, serialize directly into element
						// Otherwise, use full property name as tag
						el.Add(isContent?Save(value):Save(value, false, propertyName));
					}
					else
					{
						// Collection property: each item is a child element
						var collectionRoot = el;

						// If not a [Content] property and non-empty, create wrapper element for collection
						if (property.FindAttribute<ContentAttribute>() == null && asList.Count > 0)
						{
							collectionRoot = new XElement(propertyName);
							el.Add(collectionRoot);
						}

						// Serialize each item in collection, preserving parent type context
						foreach (var comp in asList)
						{
							collectionRoot.Add(Save(comp, parentType: obj.GetType()));
						}
					}
				}
			}

			return el;
		}

		// Checks if a property has its default value.
		// Default serialization filter: skips properties with default values to reduce file size.
		public static bool HasDefaultValue(object w, PropertyInfo property)
		{
			var value = property.GetValue(w);
			if (property.HasDefaultValue(value))
			{
				return true;
			}

			return false;
		}
	}
}
