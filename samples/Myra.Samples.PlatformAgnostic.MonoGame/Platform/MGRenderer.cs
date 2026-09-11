using System;
using System.Drawing;
using System.Numerics;
using FontStashSharp;
using FontStashSharp.Interfaces;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Platform;

namespace Myra.Samples.AllWidgets
{
	internal class MGRenderer: IMyraRenderer
	{
		private static RasterizerState _uiRasterizerState;

		private static RasterizerState UIRasterizerState
		{
			get
			{
				if (_uiRasterizerState != null)
				{
					return _uiRasterizerState;
				}

				_uiRasterizerState = new RasterizerState
				{
					ScissorTestEnable = true
				};
				return _uiRasterizerState;
			}
		}

		private readonly Texture2DManager _textureManager;
		private bool _beginCalled;
		private readonly SpriteBatch _batch;
		private TextureFiltering _textureFiltering;

		public GraphicsDevice GraphicsDevice => _textureManager.Device;

		public ITexture2DManager TextureManager => _textureManager;

		public RendererType RendererType => RendererType.Sprite;

		public Rectangle Scissor
		{
			get
			{
				var rect = GraphicsDevice.ScissorRectangle;

				rect.X -= GraphicsDevice.Viewport.X;
				rect.Y -= GraphicsDevice.Viewport.Y;

				return rect.ToSystemDrawing();
			}

			set
			{
				Flush();
				value.X += GraphicsDevice.Viewport.X;
				value.Y += GraphicsDevice.Viewport.Y;
				GraphicsDevice.ScissorRectangle = value.ToXNA();
			}
		}

		public MGRenderer(GraphicsDevice device)
		{
			_textureManager = new Texture2DManager(device);
			_batch = new SpriteBatch(GraphicsDevice);
		}

		public void Dispose()
		{
			_batch.Dispose();
		}

		public void Begin(TextureFiltering textureFiltering)
		{
			var samplerState = textureFiltering == TextureFiltering.Nearest ? SamplerState.PointClamp : SamplerState.LinearClamp;
			_batch.Begin(SpriteSortMode.Deferred,
				BlendState.AlphaBlend,
				samplerState,
				null,
				UIRasterizerState,
				null);

			_beginCalled = true;
			_textureFiltering = textureFiltering;
		}

		public void End()
		{
			_batch.End();
			_beginCalled = false;
		}

		public void DrawSprite(object texture, Vector2 position, Rectangle? sourceRectangle, FSColor color, float rotation, Vector2 scale, float depth)
		{
			var xnaTexture = (Texture2D)texture;

			_batch.Draw(xnaTexture,
				position.ToXNA(),
				sourceRectangle?.ToXNA(),
				color.ToXNA(),
				rotation,
				Vector2.Zero.ToXNA(),
				scale.ToXNA(),
				SpriteEffects.None,
				depth);
		}

		public void DrawQuad(object texture, ref FontStashSharp.Interfaces.VertexPositionColorTexture topLeft, ref FontStashSharp.Interfaces.VertexPositionColorTexture topRight, ref FontStashSharp.Interfaces.VertexPositionColorTexture bottomLeft, ref FontStashSharp.Interfaces.VertexPositionColorTexture bottomRight)
		{
			throw new NotImplementedException();
		}

		public void Draw(object texture, Rectangle dest, Rectangle? src, FSColor color)
		{
			var xnaTexture = (Texture2D)texture;

			_batch.Draw(xnaTexture,
				dest.ToXNA(),
				src != null ? src.Value.ToXNA() : (Microsoft.Xna.Framework.Rectangle?)null,
				color.ToXNA());
		}

		private void Flush()
		{
			if (_beginCalled)
			{
				End();
				Begin(_textureFiltering);
			}
		}
	}
}
