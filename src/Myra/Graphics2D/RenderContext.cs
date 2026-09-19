using System;
using FontStashSharp;
using FontStashSharp.RichText;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#elif STRIDE
using Stride.Core.Mathematics;
using Stride.Graphics;
using Texture2D = Stride.Graphics.Texture;
#else
using System.Drawing;
using System.Numerics;
using Texture2D = System.Object;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D
{
	/// <summary>
	/// Specifies the texture filtering mode used during rendering.
	/// </summary>
	public enum TextureFiltering
	{
		/// <summary>Nearest neighbor filtering (fastest but less smooth).</summary>
		Nearest,
		/// <summary>Linear filtering (bilinear interpolation).</summary>
		Linear,
		/// <summary>Anisotropic filtering (highest quality).</summary>
		Anisotropic
	}

	/// <summary>
	/// Provides rendering context for drawing 2D graphics including shapes, text, and textured regions.
	/// </summary>
	public partial class RenderContext : IDisposable
	{
		private readonly Renderer _renderer = new Renderer();

		/// <summary>
		/// Gets or sets the scissor rectangle used for clipping rendered output.
		/// </summary>
		public Rectangle Scissor
		{
			get => _renderer.Scissor;

			set => _renderer.Scissor = value;
		}

		/// <summary>
		/// Gets or sets the opacity (alpha) value for rendering, ranging from 0.0 (fully transparent) to 1.0 (fully opaque).
		/// </summary>
		public float Opacity
		{
			get => _renderer.Opacity;

			set => _renderer.Opacity = value;
		}

		internal Transform Transform
		{
			get => _renderer.Transform;
			set => _renderer.Transform = value;
		}

		/// <summary>
		/// Releases all resources used by the <see cref="RenderContext"/>.
		/// </summary>
		public void Dispose()
		{
			_renderer.Dispose();
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Adds the specified opacity value to the current opacity level.
		/// </summary>
		/// <param name="opacity">The opacity value to add.</param>
		public void AddOpacity(float opacity) => _renderer.AddOpacity(opacity);

		/// <summary>
		/// Draws a texture to a destination rectangle with rotation, depth and an optional texture filtering override.
		/// </summary>
		/// <param name="texture">The texture to draw.</param>
		/// <param name="destinationRectangle">The destination rectangle to draw the texture to.</param>
		/// <param name="sourceRectangle">An optional rectangle within the texture to draw, or null for the entire texture.</param>
		/// <param name="color">The color tint to apply.</param>
		/// <param name="rotation">The rotation angle in radians.</param>
		/// <param name="depth">The depth layer for sorting.</param>
		/// <param name="textureFiltering">An optional texture filtering override, or null to use the default from <see cref="MyraEnvironment.ImageTextureFiltering"/>.</param>
		public void Draw(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation = 0, float depth = 0.0f, TextureFiltering? textureFiltering = null) =>
			_renderer.Draw(texture, destinationRectangle, sourceRectangle, color, rotation, depth, textureFiltering);

		/// <summary>
		/// Draws a texture at a position with source rectangle, rotation, scale, depth and an optional texture filtering override.
		/// </summary>
		/// <param name="texture">The texture to draw.</param>
		/// <param name="position">The position to draw the texture at.</param>
		/// <param name="sourceRectangle">An optional rectangle within the texture to draw, or null for the entire texture.</param>
		/// <param name="color">The color tint to apply.</param>
		/// <param name="rotation">The rotation angle in radians.</param>
		/// <param name="scale">The scale factor to apply.</param>
		/// <param name="depth">The depth layer for sorting.</param>
		/// <param name="textureFiltering">An optional texture filtering override, or null to use the default from <see cref="MyraEnvironment.ImageTextureFiltering"/>.</param>
		public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 scale, float depth = 0.0f, TextureFiltering? textureFiltering = null) =>
			_renderer.Draw(texture, position, sourceRectangle, color, rotation, scale, depth, textureFiltering);

		/// <summary>
		/// Draws a texture at a position with color, scale, and optional rotation.
		/// </summary>
		/// <param name="texture">The texture to draw.</param>
		/// <param name="pos">The position to draw the texture at.</param>
		/// <param name="color">The color tint to apply.</param>
		/// <param name="scale">The scale factor to apply.</param>
		/// <param name="rotation">The rotation angle in radians.</param>
		public void Draw(Texture2D texture, Vector2 pos, Color color, Vector2 scale, float rotation = 0.0f) =>
			Draw(texture, pos, null, color, rotation, scale);

		/// <summary>
		/// Draws a texture at a position with a source rectangle, color, and rotation.
		/// </summary>
		/// <param name="texture">The texture to draw.</param>
		/// <param name="position">The position to draw the texture at.</param>
		/// <param name="sourceRectangle">An optional rectangle within the texture to draw, or null for the entire texture.</param>
		/// <param name="color">The color tint to apply.</param>
		/// <param name="rotation">The rotation angle in radians.</param>
		public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation) =>
			Draw(texture, position, sourceRectangle, color, rotation, Vector2.One);

		/// <summary>
		/// Draws a texture at a position with a source rectangle and color tint.
		/// </summary>
		/// <param name="texture">The texture to draw.</param>
		/// <param name="position">The position to draw the texture at.</param>
		/// <param name="sourceRectangle">An optional rectangle within the texture to draw, or null for the entire texture.</param>
		/// <param name="color">The color tint to apply.</param>
		public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color) =>
			Draw(texture, position, sourceRectangle, color, 0, Vector2.One);

		/// <summary>
		/// Draws a texture at a position with a color tint.
		/// </summary>
		/// <param name="texture">The texture to draw.</param>
		/// <param name="position">The position to draw the texture at.</param>
		/// <param name="color">The color tint to apply.</param>
		public void Draw(Texture2D texture, Vector2 position, Color color) =>
			Draw(texture, position, null, color, 0, Vector2.One);

		/// <summary>
		/// Draws a string with font, color, scale, rotation, and layer depth.
		/// </summary>
		/// <param name="font">The font to use for rendering.</param>
		/// <param name="text">The text string to draw.</param>
		/// <param name="position">The position to draw the text at.</param>
		/// <param name="color">The color of the text.</param>
		/// <param name="scale">The scale factor to apply.</param>
		/// <param name="rotation">The rotation angle in radians.</param>
		/// <param name="layerDepth">The depth layer for sorting.</param>
		public void DrawString(SpriteFontBase font, string text, Vector2 position, Color color, Vector2 scale, float rotation, float layerDepth = 0.0f) =>
			_renderer.DrawString(font, text, position, color, scale, rotation, layerDepth);

		/// <summary>
		/// Draws a string with font, color, scale, and layer depth.
		/// </summary>
		/// <param name="font">The font to use for rendering.</param>
		/// <param name="text">The text string to draw.</param>
		/// <param name="position">The position to draw the text at.</param>
		/// <param name="color">The color of the text.</param>
		/// <param name="scale">The scale factor to apply.</param>
		/// <param name="layerDepth">The depth layer for sorting.</param>
		public void DrawString(SpriteFontBase font, string text, Vector2 position, Color color, Vector2 scale, float layerDepth = 0.0f) =>
			DrawString(font, text, position, color, scale, 0, layerDepth);

		/// <summary>
		/// Draws a string with font, color, and layer depth.
		/// </summary>
		/// <param name="font">The font to use for rendering.</param>
		/// <param name="text">The text string to draw.</param>
		/// <param name="position">The position to draw the text at.</param>
		/// <param name="color">The color of the text.</param>
		/// <param name="layerDepth">The depth layer for sorting.</param>
		public void DrawString(SpriteFontBase font, string text, Vector2 position, Color color, float layerDepth = 0.0f) =>
			DrawString(font, text, position, color, Vector2.One, 0, layerDepth);

		/// <summary>
		/// Draws a rich text layout at the specified position with optional alignment.
		/// </summary>
		/// <param name="richText">The rich text layout to draw.</param>
		/// <param name="position">The position to draw the text at.</param>
		/// <param name="color">The color of the text.</param>
		/// <param name="sourceScale">An optional scale factor to apply.</param>
		/// <param name="rotation">The rotation angle in radians.</param>
		/// <param name="layerDepth">The depth layer for sorting.</param>
		/// <param name="horizontalAlignment">The horizontal text alignment.</param>
		public void DrawRichText(RichTextLayout richText, Vector2 position, Color color,
			Vector2? sourceScale = null, float rotation = 0, float layerDepth = 0.0f,
			TextHorizontalAlignment horizontalAlignment = TextHorizontalAlignment.Left) =>
			_renderer.DrawRichText(richText, position, color, sourceScale, rotation, layerDepth, horizontalAlignment);

		/// <summary>
		/// Begins a batch of rendering operations.
		/// </summary>
		public void Begin() => _renderer.Begin();

		/// <summary>
		/// Ends the current batch and flushes all pending draw calls.
		/// </summary>
		public void End() => _renderer.End();
	}
}