using Myra.Utility;
using System;
using Myra.MML;
using FontStashSharp.RichText;


#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.TextureAtlases
{
	/// <summary>
	/// Represents a texture region with a color tint applied.
	/// </summary>
	public class TintedRegion : IImage, IHasColor
	{
		internal const char Separator = '|';

		/// <summary>
		/// Gets the underlying texture region.
		/// </summary>
		public TextureRegion Region { get; }

		/// <summary>
		/// Gets the tint color applied to the region.
		/// </summary>
		public Color Color { get; }

		/// <summary>
		/// Gets the size of the region.
		/// </summary>
		public Point Size => Region.Size;

		/// <summary>
		/// Initializes a new instance of the <see cref="TintedRegion"/> class.
		/// </summary>
		/// <param name="image">The texture region to tint.</param>
		/// <param name="color">The tint color to apply.</param>
		public TintedRegion(TextureRegion image, Color color)
		{
			Region = image ?? throw new ArgumentNullException(nameof(image));
			Color = color;
		}

		/// <summary>
		/// Draws the tinted region to the specified destination rectangle.
		/// </summary>
		/// <param name="context">The render context.</param>
		/// <param name="dest">The destination rectangle.</param>
		/// <param name="color">The color to blend with the tint.</param>
		public void Draw(RenderContext context, Rectangle dest, Color color)
		{
			if (Color != Color.White)
			{
				color = new Color((int)(Color.R * color.R / 255.0f),
					(int)(Color.G * color.G / 255.0f),
					(int)(Color.B * color.B / 255.0f),
					(int)(Color.A * color.A / 255.0f));
			}

			Region.Draw(context, dest, color);
		}

		/// <summary>
		/// Determines whether the specified object is equal to the current tinted region.
		/// </summary>
		/// <param name="obj">The object to compare.</param>
		/// <returns>true if the regions and colors are equal; otherwise, false.</returns>
		public override bool Equals(object obj)
		{
			var asTinted = obj as TintedRegion;
			if (asTinted == null)
			{
				return false;
			}

			return Region.Equals(asTinted.Region) && Color == asTinted.Color;
		}

		/// <summary>
		/// Serves as the default hash function.
		/// </summary>
		/// <returns>A hash code for the current object.</returns>
		public override int GetHashCode()
		{
			return Region.GetHashCode() ^ Color.GetHashCode();
		}

		/// <summary>
		/// Returns a string representation of the tinted region.
		/// </summary>
		/// <returns>A string containing the region and color information.</returns>
		public override string ToString()
		{
			if (Color == Color.White)
			{
				return Region.ToString();
			}

			return Region.ToString() + Separator + Color.ToColorString();
		}

		internal static bool TryParse(ref string assetName, out Color? tint)
		{
			tint = null;

			var parts = assetName.Split(Separator);
			if (parts.Length < 2)
			{
				return false;
			}

			var color = ColorStorage.FromName(parts[1]);
			if (color == null)
			{
				throw new Exception($"Could not parse color name '{parts[1]}'");
			}

			assetName = parts[0];
			tint = color.Value;

			return true;
		}
	}
}
