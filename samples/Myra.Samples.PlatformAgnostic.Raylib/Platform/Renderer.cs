using System;
using System.Numerics;
using FontStashSharp;
using FontStashSharp.Interfaces;
using Myra.Platform;
using Raylib_cs;
using Rectangle = System.Drawing.Rectangle;

namespace Myra.Samples.AllWidgets
{
	internal class Renderer : IMyraRenderer
	{
		private readonly Texture2DManager _textureManager = new Texture2DManager();
		private Rectangle _scissor;

		public RendererType RendererType => RendererType.Sprite;

		public ITexture2DManager TextureManager => _textureManager;

		public Rectangle Viewport { get; set; }

		public Rectangle Scissor
		{
			get => _scissor;
			set => _scissor = value;
		}

		public void Dispose()
		{
		}

		public void Begin(TextureFiltering textureFiltering)
		{
			// Set blend mode for premultiplied alpha textures
			// Premultiplied formula: result = source + dest * (1 - sourceAlpha)
			Raylib.BeginBlendMode(BlendMode.AlphaPremultiply);
		}

		public void End()
		{
			// Restore default blend mode
			Raylib.EndBlendMode();
		}

		public void DrawQuad(object texture, ref VertexPositionColorTexture topLeft, ref VertexPositionColorTexture topRight, ref VertexPositionColorTexture bottomLeft, ref VertexPositionColorTexture bottomRight)
		{
			// Not used when RendererType is Sprite
			throw new NotImplementedException();
		}

		public void DrawSprite(object texture, Vector2 pos, Rectangle? src, FSColor color, float rotation, Vector2 scale, float depth)
		{
			if (texture == null)
				return;

			var tex = (Texture)texture;
			var raylibTexture = tex.RaylibTexture;

			// Define source rectangle
			Raylib_cs.Rectangle sourceRect;
			if (src.HasValue)
			{
				sourceRect = src.Value.ToRaylib();
			}
			else
			{
				sourceRect = new Raylib_cs.Rectangle(0, 0, tex.Width, tex.Height);
			}

			// Define destination rectangle with scaling
			Raylib_cs.Rectangle destRect = new Raylib_cs.Rectangle(
				pos.X,
				pos.Y,
				sourceRect.Width * scale.X,
				sourceRect.Height * scale.Y
			);

			// Draw the sprite using Raylib's batched drawing
			Raylib.DrawTexturePro(raylibTexture, sourceRect, destRect, Vector2.Zero, rotation * 57.2958f, color.ToRaylib());
		}
	}
}
