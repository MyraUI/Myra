using System;
using System.Drawing;
using System.Numerics;
using FontStashSharp;
using FontStashSharp.Interfaces;
using Myra.Graphics2D;
using Myra.Platform;
using TrippyGL;

namespace Myra.Samples.AllWidgets
{
	internal class TrippyRenderer: IMyraRenderer
	{
		private bool _beginCalled;
		
		private readonly Texture2DManager _textureManager;
		private readonly SimpleShaderProgram _shaderProgram;
		private readonly TextureBatcher _batch;

		private TextureFiltering _filtering;

		private DepthState _oldDepthState;
		private bool _oldFaceCullingEnabled;
		private BlendState _oldBlendState;
		private bool _oldScissorEnabled;
		private Rectangle _scissorRectangle;

		public GraphicsDevice Device => _textureManager.Device;

		public ITexture2DManager TextureManager => _textureManager;

		public RendererType RendererType => RendererType.Sprite;

		public Rectangle Scissor
		{
			get
			{
				return _scissorRectangle;
			}

			set
			{
				Flush();

				value.X += Device.Viewport.X;
				value.Y += Device.Viewport.Y;

				// TripplyGL Scissor Rect has y-axis facing upwards
				// Hence we require some transforms
				var result = new Viewport(value.X, (int)(Device.Viewport.Height - value.Height - value.Y), (uint)value.Width, (uint)value.Height);
				Device.ScissorRectangle = result;

				_scissorRectangle = value;
			}
		}

		public TrippyRenderer(GraphicsDevice graphicsDevice)
		{
			_textureManager = new Texture2DManager(graphicsDevice);

			_shaderProgram = SimpleShaderProgram.Create<VertexColorTexture>(graphicsDevice, 0, 0, true);
			OnViewportChanged();
			_batch = new TextureBatcher(Device);
			_batch.SetShaderProgram(_shaderProgram);
		}

		public void Dispose()
		{
			_batch.Dispose();
			_shaderProgram.Dispose();
		}

		public void OnViewportChanged()
		{
			_shaderProgram.Projection = Matrix4x4.CreateOrthographicOffCenter(0, Device.Viewport.Width, Device.Viewport.Height, 0, 0, 1);
		}

		public void Begin(TextureFiltering textureFiltering)
		{
			// Save old state
			_oldDepthState = Device.DepthState;
			_oldFaceCullingEnabled = Device.FaceCullingEnabled;
			_oldBlendState = Device.BlendState;
			_oldScissorEnabled = Device.ScissorTestEnabled;

			// Set new state
			Device.DepthState = DepthState.None;
			Device.FaceCullingEnabled = false;
			Device.BlendState = BlendState.AlphaBlend;
			Device.ScissorTestEnabled = true;

			_batch.Begin();

			_beginCalled = true;
			_filtering = textureFiltering;
		}

		public void End()
		{
			_batch.End();
			_beginCalled = false;

			// Restore old state
			Device.DepthState = _oldDepthState;
			Device.FaceCullingEnabled = _oldFaceCullingEnabled;
			Device.BlendState = _oldBlendState;
			Device.ScissorTestEnabled = _oldScissorEnabled;
		}

		public void DrawQuad(object texture, ref VertexPositionColorTexture topLeft, ref VertexPositionColorTexture topRight, ref VertexPositionColorTexture bottomLeft, ref VertexPositionColorTexture bottomRight)
		{
			throw new NotImplementedException();
		}

		public void DrawSprite(object texture, Vector2 pos, Rectangle? src, FSColor color, float rotation, Vector2 scale, float depth)
		{
			var tex = (Texture2D)texture;

			_batch.Draw(tex,
				pos,
				src,
				color.ToTrippy(),
				scale,
				rotation,
				Vector2.Zero,
				depth);
		}

		private void Flush()
		{
			if (_beginCalled)
			{
				End();
				Begin(_filtering);
			}
		}
	}
}
