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
	/// Provides factory methods for creating common procedural brushes.
	/// </summary>
	public static class BrushFactory
	{
		/// <summary>
		/// Creates a solid rounded rectangle brush from a procedurally generated nine-patch texture.
		/// The brush stretches while keeping the corners intact, which produces a rectangle with
		/// rounded corners of the given radius.
		/// </summary>
		/// <remarks>
		/// Myra must be set up before calling this method (MyraEnvironment.Game for
		/// MonoGame/FNA/Stride or MyraEnvironment.Platform for the platform-agnostic builds).
		/// </remarks>
		/// <param name="size">
		/// The width and height of the generated texture in pixels. Larger values provide smoother,
		/// higher-resolution corners. Defaults to 64.
		/// </param>
		/// <param name="radius">
		/// The corner radius of the rounded rectangle in pixels. It should not exceed half of
		/// <paramref name="size"/>. Defaults to 16.
		/// </param>
		/// <param name="color">
		/// The color the rounded rectangle is filled with. Defaults to white.
		/// </param>
		/// <returns>A brush that stretches into a rectangle with rounded corners.</returns>
		public static IBrush CreateSolidRoundedRect(int size = 64, int radius = 16, Color color = default)
		{
			if (radius < 0 || radius > size / 2)
			{
				throw new ArgumentOutOfRangeException(nameof(radius), "Radius should be in the range of 0..size / 2.");
			}

			var c = color == default ? Color.White : color;

			// Premultiplied RGBA: the RGB channels are multiplied by the alpha channel so the
			// texture matches the convention used by textures loaded through Myra texture managers.
			var data = new byte[size * size * 4];

			var midpoint = size * 0.5f;
			for (var y = 0; y < size; ++y)
			{
				for (var x = 0; x < size; ++x)
				{
					var sd = RoundedRectSdf(x + 0.5f, y + 0.5f, midpoint, midpoint, midpoint, midpoint, radius);
					var coverage = Coverage(sd);

					var factor = coverage * c.A / 255.0f;
					var i = (y * size + x) * 4;
					data[i] = (byte)(c.R * factor);
					data[i + 1] = (byte)(c.G * factor);
					data[i + 2] = (byte)(c.B * factor);
					data[i + 3] = (byte)(255 * factor);
				}
			}

			return CreateNinePatch(size, radius, data);
		}

		/// <summary>
		/// Creates a hollow rounded rectangle brush from a procedurally generated nine-patch texture.
		/// The brush produces a rectangle with rounded corners: a border of the specified thickness
		/// and color around an interior filled with the specified fill color.
		/// </summary>
		/// <remarks>
		/// Myra must be set up before calling this method (MyraEnvironment.Game for
		/// MonoGame/FNA/Stride or MyraEnvironment.Platform for the platform-agnostic builds).
		/// </remarks>
		/// <param name="size">
		/// The width and height of the generated texture in pixels. Larger values provide smoother,
		/// higher-resolution corners. Defaults to 64.
		/// </param>
		/// <param name="radius">
		/// The corner radius of the rounded rectangle in pixels. It should not exceed half of
		/// <paramref name="size"/>. Defaults to 16.
		/// </param>
		/// <param name="borderWidth">
		/// The thickness of the border in pixels. It should not exceed half of <paramref name="size"/>.
		/// Defaults to 4.
		/// </param>
		/// <param name="color">
		/// The color of the border. Defaults to white.
		/// </param>
		/// <param name="fillColor">
		/// The color the interior is filled with. Use a transparent color to leave the interior empty.
		/// Defaults to transparent.
		/// </param>
		/// <returns>A brush that stretches into a hollow rectangle with rounded corners.</returns>
		public static IBrush CreateHollowRoundedRect(int size = 64, int radius = 16, int borderWidth = 4,
			Color color = default, Color fillColor = default)
		{
			if (radius < 0 || radius > size / 2)
			{
				throw new ArgumentOutOfRangeException(nameof(radius), "Radius should be in the range of 0..size / 2.");
			}

			if (borderWidth < 0 || borderWidth > size / 2)
			{
				throw new ArgumentOutOfRangeException(nameof(borderWidth), "Border width should be in the range of 0..size / 2.");
			}

			var c = color == default ? Color.White : color;

			// Premultiplied RGBA: the RGB channels are multiplied by the alpha channel so the
			// texture matches the convention used by textures loaded through Myra texture managers.
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

					var borderFactor = (outerCoverage - innerCoverage) * c.A / 255.0f;
					var fillFactor = innerCoverage * fillColor.A / 255.0f;

					var i = (y * size + x) * 4;
					data[i] = (byte)(c.R * borderFactor + fillColor.R * fillFactor);
					data[i + 1] = (byte)(c.G * borderFactor + fillColor.G * fillFactor);
					data[i + 2] = (byte)(c.B * borderFactor + fillColor.B * fillFactor);
					data[i + 3] = (byte)(255 * (borderFactor + fillFactor));
				}
			}

			return CreateNinePatch(size, radius, data);
		}

		private static IBrush CreateNinePatch(int size, int radius, byte[] data)
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