using System;
using System.Globalization;

namespace Myra.Graphics2D.UI
{
	public enum DimensionType
	{
		Auto,
		Pixel,
		Fill,
		Percent
	}

	public struct Dimension : IEquatable<Dimension>
	{
		public static Dimension Auto => new Dimension(DimensionType.Auto);
		public static Dimension Fill => new Dimension(DimensionType.Fill);

		public DimensionType Type { get; set; }
		public float Value { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="Dimension"/> struct.
		/// </summary>
		/// <param name="type">The dimension type.</param>
		/// <param name="value">The dimension value.</param>
		public Dimension(DimensionType type, float value = 0.0f)
		{
			Type = type;
			Value = value;
		}

		public static Dimension Pixel(float value) => new Dimension(DimensionType.Pixel, value);

		public static Dimension Percent(float value) => new Dimension(DimensionType.Percent, value);
		
		public static implicit operator Dimension(int value) => Pixel(value);

		public static implicit operator Dimension(float value) => Pixel(value);

		public static Dimension Parse(string value)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			value = value.Trim();
			if (value.Length == 0)
			{
				throw new FormatException("Dimension value cannot be empty.");
			}

			if (string.Equals(value, nameof(DimensionType.Auto), StringComparison.OrdinalIgnoreCase))
			{
				return Auto;
			}

			if (string.Equals(value, nameof(DimensionType.Fill), StringComparison.OrdinalIgnoreCase))
			{
				return Fill;
			}

			if (value.EndsWith("%", StringComparison.Ordinal))
			{
				var percent = ParseFloat(value.Substring(0, value.Length - 1));
				return Percent(percent / 100.0f);
			}

			if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
			{
				var pixels = ParseFloat(value.Substring(0, value.Length - 2));
				return Pixel(pixels);
			}

			return Pixel(ParseFloat(value));
		}

		public override string ToString()
		{
			switch (Type)
			{
				case DimensionType.Auto:
				case DimensionType.Fill:
					return Type.ToString();
				case DimensionType.Percent:
					return FormatFloat(Value * 100.0f) + "%";
				default:
					return FormatFloat(Value);
			}
		}

		public bool Equals(Dimension other) => Type == other.Type && Value.Equals(other.Value);

		public override bool Equals(object obj) => obj is Dimension other && Equals(other);

		public static bool operator ==(Dimension left, Dimension right) => left.Equals(right);

		public static bool operator !=(Dimension left, Dimension right) => !left.Equals(right);

		private static float ParseFloat(string value) => float.Parse(value.Trim(), CultureInfo.InvariantCulture);

		private static string FormatFloat(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
	}
}
