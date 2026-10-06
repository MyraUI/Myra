using System.ComponentModel;
using System.Reflection;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;
using System.Collections.Generic;
using Myra.MML;
using FontStashSharp.RichText;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
using System.Numerics;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Utility
{
	internal static class Serialization
	{
		public static readonly Dictionary<Type, ITypeSerializer> _serializers = new Dictionary<Type, ITypeSerializer>
		{
			{typeof(Vector2), new Vector2Serializer()},
			{typeof(Thickness), new ThicknessSerializer()},
			{typeof(Color), new ColorSerializer()},
			{typeof(Rectangle), new RectangleSerializer()},
			{typeof(Dimension), new DimensionSerializer()}
		};

		public static bool HasDefaultValue(this PropertyInfo property, object value)
		{
			if (property.PropertyType == typeof(Thickness) &&
				value.Equals(Thickness.Zero))
			{
				// Skip empty Thickness
				return true;
			}

			var defaultAttribute = property.FindAttribute<DefaultValueAttribute>();
			if (defaultAttribute != null)
			{
				object defaultAttributeValue = defaultAttribute.Value;
				// If property is of Color type, than DefaultValueAttribute should contain its name or hex
				if (property.PropertyType == typeof(Color))
				{
					defaultAttributeValue = ColorStorage.FromName(defaultAttributeValue.ToString()).Value;
				}

				if (property.PropertyType == typeof(string) &&
					string.IsNullOrEmpty((string)defaultAttributeValue) &&
					string.IsNullOrEmpty((string)value))
				{
					// Skip empty/null string
					return true;
				}

				if (Equals(value, defaultAttributeValue))
				{
					// Skip default
					return true;
				}

				if (defaultAttributeValue != null &&
					defaultAttributeValue.GetType() == typeof(string) &&
					_serializers.TryGetValue(property.PropertyType, out ITypeSerializer typeSerializer) &&
					(string)defaultAttributeValue == typeSerializer.Serialize(value))
				{
					return true;
				}
			}

			return false;
		}

		public static string RectangleToString(this Rectangle r) => $"{r.X}, {r.Y}, {r.Width}, {r.Height}";

		public static Rectangle ParseRectangle(this string s)
		{
			var parts = s.Split(',');
			if (parts.Length != 4)
			{
				throw new Exception("Rectangle should consist of 4 numbers");
			}

			Rectangle result;
			result.X = int.Parse(parts[0].Trim());
			result.Y = int.Parse(parts[1].Trim());
			result.Width = int.Parse(parts[2].Trim());
			result.Height = int.Parse(parts[3].Trim());

			return result;
		}
	}
}