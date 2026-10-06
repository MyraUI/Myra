using System.Globalization;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Myra.Utility;
using FontStashSharp.RichText;
using System;


#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
using System.Numerics;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.MML
{
	/// <summary>
	/// Defines the interface for serializing and deserializing types to and from strings.
	/// </summary>
	public interface ITypeSerializer
	{
		/// <summary>
		/// Serializes an object to a string.
		/// </summary>
		/// <param name="obj">The object to serialize.</param>
		/// <returns>The serialized string representation.</returns>
		string Serialize(object obj);

		/// <summary>
		/// Deserializes a string to an object.
		/// </summary>
		/// <param name="str">The string to deserialize.</param>
		/// <returns>The deserialized object.</returns>
		object Deserialize(string str);
	}

	/// <summary>
	/// Abstract base class for type serializers that serialize and deserialize generic types.
	/// </summary>
	/// <typeparam name="T">The type being serialized/deserialized.</typeparam>
	public abstract class TypeSerializer<T> : ITypeSerializer
	{
		/// <summary>
		/// Deserializes a string to an object.
		/// </summary>
		/// <param name="str">The string to deserialize.</param>
		/// <returns>The deserialized object.</returns>
		public object Deserialize(string str) => DeserializeT(str);

		/// <summary>
		/// Serializes an object to a string.
		/// </summary>
		/// <param name="obj">The object to serialize.</param>
		/// <returns>The serialized string representation.</returns>
		public string Serialize(object obj) => SerializeT((T)obj);

		/// <summary>
		/// Deserializes a string to a typed value.
		/// </summary>
		/// <param name="str">The string to deserialize.</param>
		/// <returns>The deserialized typed value.</returns>
		public abstract T DeserializeT(string str);

		/// <summary>
		/// Serializes a typed value to a string.
		/// </summary>
		/// <param name="obj">The typed value to serialize.</param>
		/// <returns>The serialized string representation.</returns>
		public abstract string SerializeT(T obj);
	}

	internal sealed class Vector2Serializer : TypeSerializer<Vector2>
	{
		public override Vector2 DeserializeT(string str)
		{
			var parts = str.Split(',');
			var x = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
			var y = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
			return new Vector2(x, y);
		}

		public override string SerializeT(Vector2 obj)
		{
			return obj.X.ToString(CultureInfo.InvariantCulture) + ", " + obj.Y.ToString(CultureInfo.InvariantCulture);
		}
	}

	internal sealed class ColorSerializer : TypeSerializer<Color>
	{
		public override Color DeserializeT(string str)
		{
			var color = ColorStorage.FromName(str);
			if (color == null)
			{
				throw new Exception(string.Format("Could not find parse color '{0}'", str));
			}

			return color.Value;
		}

		public override string SerializeT(Color obj) => obj.ToColorString();
	}

	internal sealed class ThicknessSerializer : TypeSerializer<Thickness>
	{
		public override Thickness DeserializeT(string str)
		{
			return Thickness.FromString(str);
		}

		public override string SerializeT(Thickness obj)
		{
			return obj.ToString();
		}
	}

	internal sealed class RectangleSerializer : TypeSerializer<Rectangle>
	{
		public override Rectangle DeserializeT(string str) => str.ParseRectangle();

		public override string SerializeT(Rectangle obj) => obj.RectangleToString();
	}

	internal sealed class DimensionSerializer : TypeSerializer<Dimension>
	{
		public override Dimension DeserializeT(string str) => Dimension.Parse(str);

		public override string SerializeT(Dimension obj) => obj.ToString();
	}
}
