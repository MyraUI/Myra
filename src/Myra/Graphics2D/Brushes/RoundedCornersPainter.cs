using System;
using Myra.Graphics2D.TextureAtlases;
using Myra.Utility;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#elif STRIDE
using Stride.Core.Mathematics;
using Texture2D = Stride.Graphics.Texture;
#else
using System.Drawing;
using Texture2D = System.Object;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.Brushes
{
	/// <summary>
	/// The shared implementation of the procedural brushes that draw rectangles with rounded
	/// corners (<see cref="RoundedCornersSolidBrush"/> and <see cref="RoundedCornersHollowBrush"/>):
	/// generation of the premultiplied RGBA pixels, creation and release of the nine-patch region
	/// the brushes draw, and validation of the values they are configured with.
	/// </summary>
	internal static class RoundedCornersPainter
	{
		/// <summary>
		/// Creates a nine-patch region filled entirely with the specified color.
		/// </summary>
		/// <param name="size">The width and height of the generated texture in pixels.</param>
		/// <param name="radius">The corner radius in pixels.</param>
		/// <param name="color">The color the rounded rectangle is filled with.</param>
		public static NinePatchRegion CreateSolid(int size, int radius, Color color)
		{
			return CreateRegion(size, radius, GenerateSolid(size, radius, color));
		}

		/// <summary>
		/// Creates a nine-patch region with a border of the specified color around an interior
		/// filled with the specified fill color.
		/// </summary>
		/// <param name="size">The width and height of the generated texture in pixels.</param>
		/// <param name="radius">The corner radius in pixels.</param>
		/// <param name="borderWidth">The thickness of the border in pixels.</param>
		/// <param name="color">The color of the border.</param>
		/// <param name="fillColor">The color the interior is filled with.</param>
		public static NinePatchRegion CreateHollow(int size, int radius, int borderWidth, Color color, Color fillColor)
		{
			return CreateRegion(size, radius, GenerateHollow(size, radius, borderWidth, color, fillColor));
		}

		/// <summary>
		/// Assigns the newly generated region and releases the texture of the previous one. The
		/// render batch is flushed beforehand, as the previous texture could still be referenced
		/// by pending draw calls.
		/// </summary>
		/// <param name="context">The render context whose batch has to be flushed.</param>
		/// <param name="region">The region to replace.</param>
		/// <param name="newRegion">The newly generated region.</param>
		public static void Regenerate(RenderContext context, ref NinePatchRegion region, NinePatchRegion newRegion)
		{
			var previous = region;

			region = newRegion;

			if (previous != null)
			{
				context.End();
				Dispose(previous);
				context.Begin();
			}
		}

		/// <summary>
		/// Releases the texture of the specified region.
		/// </summary>
		/// <param name="region">The region to release the texture of.</param>
		public static void Dispose(NinePatchRegion region)
		{
			if (region == null)
			{
				return;
			}

#if MONOGAME || FNA || STRIDE
			region.Texture?.Dispose();
#endif
		}

		/// <summary>
		/// Ensures that the size of the generated texture is valid.
		/// </summary>
		/// <param name="size">The value to validate.</param>
		/// <param name="paramName">The name of the validated property.</param>
		public static void ValidateSize(int size, string paramName)
		{
			if (size <= 0)
			{
				throw new ArgumentOutOfRangeException(paramName, "Size should be positive.");
			}
		}

		/// <summary>
		/// Ensures that the corner radius is valid for the given size of the generated texture.
		/// </summary>
		/// <param name="radius">The value to validate.</param>
		/// <param name="size">The width and height of the generated texture in pixels.</param>
		/// <param name="paramName">The name of the validated property.</param>
		public static void ValidateRadius(int radius, int size, string paramName)
		{
			if (radius < 0 || radius > size / 2)
			{
				throw new ArgumentOutOfRangeException(paramName, "Radius should be in the range of 0..Size / 2.");
			}
		}

		/// <summary>
		/// Ensures that the border width is valid for the given size of the generated texture.
		/// </summary>
		/// <param name="borderWidth">The value to validate.</param>
		/// <param name="size">The width and height of the generated texture in pixels.</param>
		/// <param name="paramName">The name of the validated property.</param>
		public static void ValidateBorderWidth(int borderWidth, int size, string paramName)
		{
			if (borderWidth < 0 || borderWidth > size / 2)
			{
				throw new ArgumentOutOfRangeException(paramName, "Border width should be in the range of 0..Size / 2.");
			}
		}

		/// <summary>
		/// Generates the premultiplied RGBA pixels of a rounded rectangle filled with a single color.
		/// </summary>
		private static byte[] GenerateSolid(int size, int radius, Color color)
		{
			var data = new byte[size * size * 4];

			var midpoint = size * 0.5f;
			for (var y = 0; y < size; ++y)
			{
				for (var x = 0; x < size; ++x)
				{
					var sd = RoundedRectSdf(x + 0.5f, y + 0.5f, midpoint, midpoint, midpoint, midpoint, radius);
					var factor = Coverage(sd) * color.A / 255.0f;

					var i = (y * size + x) * 4;
					data[i] = (byte)(color.R * factor);
					data[i + 1] = (byte)(color.G * factor);
					data[i + 2] = (byte)(color.B * factor);
					data[i + 3] = (byte)(255 * factor);
				}
			}

			return data;
		}

		/// <summary>
		/// Generates the premultiplied RGBA pixels of a rounded rectangle with a border around an
		/// interior filled with a separate color.
		/// </summary>
		private static byte[] GenerateHollow(int size, int radius, int borderWidth, Color color, Color fillColor)
		{
			var data = new byte[size * size * 4];

			var midpoint = size * 0.5f;
			var halfOuter = midpoint;
			var halfInner = midpoint - borderWidth;
			var innerRadius = Math.Max(radius - borderWidth, 0);
			for (var y = 0; y < size; ++y)
			{
				for (var x = 0; x < size; ++x)
				{
					var px = x + 0.5f;
					var py = y + 0.5f;

					// Border band: covered by the outer rounded rectangle but not the inner one.
					// Interior: covered by the inner rounded rectangle.
					var outerCoverage = Coverage(RoundedRectSdf(px, py, midpoint, midpoint, halfOuter, halfOuter, radius));
					var innerCoverage = Coverage(RoundedRectSdf(px, py, midpoint, midpoint, halfInner, halfInner, innerRadius));

					var borderFactor = (outerCoverage - innerCoverage) * color.A / 255.0f;
					var fillFactor = innerCoverage * fillColor.A / 255.0f;

					var i = (y * size + x) * 4;
					data[i] = (byte)(color.R * borderFactor + fillColor.R * fillFactor);
					data[i + 1] = (byte)(color.G * borderFactor + fillColor.G * fillFactor);
					data[i + 2] = (byte)(color.B * borderFactor + fillColor.B * fillFactor);
					data[i + 3] = (byte)(255 * (borderFactor + fillFactor));
				}
			}

			return data;
		}

		/// <summary>
		/// Uploads the generated pixels into a nine-patch texture region whose border is the corner
		/// radius, so the corners are never stretched.
		/// </summary>
		private static NinePatchRegion CreateRegion(int size, int radius, byte[] data)
		{
#if MONOGAME || FNA || STRIDE
			var texture = CrossEngineStuff.CreateTexture(MyraEnvironment.GraphicsDevice, size, size);
			CrossEngineStuff.SetTextureData(texture, new Rectangle(0, 0, size, size), data);
#else
			var textureManager = MyraEnvironment.Platform.Renderer.TextureManager;
			var texture = textureManager.CreateTexture(size, size);
			textureManager.SetTextureData(texture, new Rectangle(0, 0, size, size), data);
#endif

			return new NinePatchRegion(texture, new Rectangle(0, 0, size, size), new Thickness(radius));
		}

		/// <summary>
		/// Converts a signed distance into a pixel coverage value in the range of 0..1,
		/// with a 1px antialiasing band around the shape boundary.
		/// </summary>
		private static float Coverage(float sd)
		{
			// 1px antialiasing band around the rounded rectangle
			var alpha = 0.5f - sd;
			if (alpha < 0)
			{
				return 0;
			}

			if (alpha > 1)
			{
				return 1;
			}

			return alpha;
		}

		/// <summary>
		/// Signed distance to a rounded rectangle with the given center, half extents and corner
		/// radius. Negative inside, positive outside.
		/// </summary>
		private static float RoundedRectSdf(float px, float py, float cx, float cy, float halfWidth, float halfHeight, float radius)
		{
			var qx = Math.Abs(px - cx) - halfWidth + radius;
			var qy = Math.Abs(py - cy) - halfHeight + radius;

			var ox = Math.Max(qx, 0);
			var oy = Math.Max(qy, 0);

			return (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - radius;
		}
	}
}
