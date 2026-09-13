using System;
using FontStashSharp;
using Myra.Utility;
using FontStashSharp.RichText;


#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#elif STRIDE
using Stride.Core.Mathematics;
using Stride.Graphics;
using Texture2D = Stride.Graphics.Texture;
#else
using FontStashSharp.Interfaces;
using System.Drawing;
using Myra.Platform;
using System.Numerics;
using Texture2D = System.Object;
using Color = FontStashSharp.FSColor;
#endif


namespace Myra.Graphics2D
{
	partial class RenderContext
	{
		/// <summary>
		/// Low-level rendering
		/// </summary>
		private class Renderer : IDisposable
		{
#if MONOGAME
			private static SamplerState _textureFilteringAnisotropic = new SamplerState
			{
				Filter = TextureFilter.Anisotropic,
				AddressU = TextureAddressMode.Clamp,
				AddressV = TextureAddressMode.Clamp,
				AddressW = TextureAddressMode.Clamp,
				BorderColor = Color.Transparent,
				MaxAnisotropy = 16,
				MaxMipLevel = 16,
				MipMapLevelOfDetailBias = 0f,
				ComparisonFunction = CompareFunction.Never,
				FilterMode = TextureFilterMode.Default
			};
#elif STRIDE
			private static readonly RasterizerStateDescription _uiRasterizerState;

			static Renderer()
			{
				var rs = new RasterizerStateDescription();
				rs.SetDefault();
				rs.ScissorTestEnable = true;
				_uiRasterizerState = rs;
			}
#endif

			private enum ModeType
			{
				Sprite,
				SDF
			}

#if MONOGAME || FNA
			private SDFTextBatch _sdfTextBatch;
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
#endif

#if MONOGAME || FNA || STRIDE
			private readonly SpriteBatch _renderer;
#else
			private readonly IMyraRenderer _renderer;
			private readonly FontStashRenderer _fontStashRenderer;
			private readonly FontStashRenderer2 _fontStashRenderer2;
			private VertexPositionColorTexture _topLeft = new VertexPositionColorTexture(),
				_topRight = new VertexPositionColorTexture(),
				_bottomLeft = new VertexPositionColorTexture(),
				_bottomRight = new VertexPositionColorTexture();
#endif

			private bool _beginCalled;
			private Rectangle _scissor;
			private ModeType? _mode;
			private TextureFiltering? _textureFiltering;

			public Transform Transform;

			/// <summary>
			/// Gets or sets the scissor rectangle for clipping rendered output.
			/// </summary>
			public Rectangle Scissor
			{
				get
				{
					return _scissor;
				}

				set
				{
					_scissor = value;

					if (MyraEnvironment.DisableClipping)
					{
						return;
					}

					Flush();

#if MONOGAME || FNA
					var device = _renderer.GraphicsDevice;
					value.X += device.Viewport.X;
					value.Y += device.Viewport.Y;
#endif

					CrossEngineStuff.Scissor = value;
				}
			}

			public float Opacity { get; set; }

			public TextureFiltering ImageTextureFiltering { get; set; }
			public TextureFiltering TextTextureFiltering { get; set; }

#if MONOGAME || FNA

			private SDFTextBatch SDFTextBatch
			{
				get
				{
					if (_sdfTextBatch == null)
					{
						_sdfTextBatch = new SDFTextBatch(MyraEnvironment.GraphicsDevice);
					}

					return _sdfTextBatch;
				}
			}
#endif

			public Renderer()
			{
#if MONOGAME || FNA || STRIDE
				_renderer = new SpriteBatch(MyraEnvironment.Game.GraphicsDevice);
#else
				_renderer = MyraEnvironment.Platform.Renderer;

				if (_renderer.RendererType == RendererType.Sprite)
				{
					_fontStashRenderer = new FontStashRenderer(_renderer);
					_fontStashRenderer2 = null;
				}
				else
				{
					_fontStashRenderer = null;
					_fontStashRenderer2 = new FontStashRenderer2(_renderer);
				}
#endif
			}

			public void Dispose()
			{
				_renderer.Dispose();

#if MONOGAME || FNA
				SDFTextBatch?.Dispose();
#endif

				GC.SuppressFinalize(this);
			}


			public void AddOpacity(float opacity)
			{
				Opacity *= opacity;
			}

			public void Draw(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, float depth = 0.0f)
			{
				Vector2 sz;
				if (sourceRectangle != null)
				{
					sz = new Vector2(sourceRectangle.Value.Width, sourceRectangle.Value.Height);
				}
				else
				{
#if MONOGAME || FNA || STRIDE
					sz = new Vector2(texture.Width, texture.Height);
#else
					Point p;
					if (_fontStashRenderer != null)
					{
						p = _fontStashRenderer.TextureManager.GetTextureSize(texture);
					}
					else
					{
						p = _fontStashRenderer2.TextureManager.GetTextureSize(texture);
					}

					sz = new Vector2(p.X, p.Y);
#endif
				}

				var pos = new Vector2(destinationRectangle.X, destinationRectangle.Y);
				var scale = new Vector2(destinationRectangle.Width / sz.X, destinationRectangle.Height / sz.Y);
				Draw(texture, pos, sourceRectangle, color, rotation, scale, depth);
			}

			public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 scale, float depth = 0.0f)
			{
				SetState(ModeType.Sprite, ImageTextureFiltering);

				color = CrossEngineStuff.MultiplyColor(color, Opacity);
				scale *= Transform.Scale;
				rotation += Transform.Rotation;

#if MONOGAME || FNA
				position = Transform.Apply(position);

				_renderer.Draw(texture, position, sourceRectangle, color, rotation, Vector2.Zero, scale, SpriteEffects.None, depth);
#elif STRIDE
				position = Transform.Apply(position);

				_renderer.Draw(texture, position, sourceRectangle, color, rotation, Vector2.Zero, scale, SpriteEffects.None, ImageOrientation.AsIs, depth);
#else
				if (_fontStashRenderer != null)
				{
					position = Transform.Apply(position);
					_renderer.DrawSprite(texture, position, sourceRectangle, color, rotation, scale, depth);
				}
				else
				{
					Rectangle r;
					if (sourceRectangle != null)
					{
						r = sourceRectangle.Value;
					}
					else
					{
						var textureSize = _fontStashRenderer2.TextureManager.GetTextureSize(texture);
						r = new Rectangle(0, 0, textureSize.X, textureSize.Y);
					}

					var size = new Vector2(scale.X * r.Width, scale.Y * r.Height);
					_renderer.DrawQuad(texture, color, position, ref Transform.Matrix, depth, size, r,
						ref _topLeft, ref _topRight, ref _bottomLeft, ref _bottomRight);
				}
#endif
			}

			public void DrawString(SpriteFontBase font, string text, Vector2 position, Color color, Vector2 scale, float rotation, float layerDepth = 0.0f)
			{
				SetTextState(font);

				color = CrossEngineStuff.MultiplyColor(color, Opacity);
				position = Transform.Apply(position);

				scale *= Transform.Scale;
				rotation += Transform.Rotation;

#if MONOGAME || FNA || STRIDE
				font.DrawText(_renderer, text, position, color, rotation, Vector2.Zero, scale, layerDepth);
#else
				if (_fontStashRenderer != null)
				{
					font.DrawText(_fontStashRenderer, text, position, color, rotation, Vector2.Zero, scale);
				}
				else
				{
					font.DrawText(_fontStashRenderer2, text, position, color, rotation, Vector2.Zero, scale);
				}
#endif
			}

			public void DrawRichText(RichTextLayout richText, Vector2 position, Color color,
				Vector2? sourceScale = null, float rotation = 0, float layerDepth = 0.0f,
				TextHorizontalAlignment horizontalAlignment = TextHorizontalAlignment.Left)
			{
				SetTextState(richText.Font);

				color = CrossEngineStuff.MultiplyColor(color, Opacity);
				position = Transform.Apply(position);

				var scale = sourceScale ?? Vector2.One;

				scale *= Transform.Scale;
				rotation += Transform.Rotation;

#if MONOGAME || FNA || STRIDE
				if (richText.Font.FontRasterizationMode == FontRasterizationMode.Standard)
				{
					richText.Draw(_renderer, position, color, rotation, Vector2.Zero, scale, layerDepth, horizontalAlignment);
				}
				else
				{
#if MONOGAME || FNA
					richText.Draw(SDFTextBatch, position, color, rotation, Vector2.Zero, scale, layerDepth, horizontalAlignment);
#else
					richText.Draw(_renderer, position, color, rotation, Vector2.Zero, scale, layerDepth, horizontalAlignment);
#endif
				}
#else
				if (_fontStashRenderer != null)
				{
					richText.Draw(_fontStashRenderer, position, color, rotation, Vector2.Zero, scale, layerDepth, horizontalAlignment);
				}
				else
				{
					richText.Draw(_fontStashRenderer2, position, color, rotation, Vector2.Zero, scale, layerDepth, horizontalAlignment);
				}
#endif
			}

#if MONOGAME || FNA
			private SamplerState SelectedSamplerState()
			{
				switch (_textureFiltering)
				{
					case TextureFiltering.Nearest:
						return SamplerState.PointClamp;
					case TextureFiltering.Linear:
						return SamplerState.LinearClamp;
					case TextureFiltering.Anisotropic:
#if MONOGAME
						return _textureFilteringAnisotropic;
#else
						throw new NotSupportedException("Anisotropic filtering not supported for FNA");
#endif
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
#elif STRIDE
			private SamplerState SelectedSamplerState()
			{
				switch (_textureFiltering)
				{
					case TextureFiltering.Nearest:
						return MyraEnvironment.GraphicsDevice.SamplerStates.PointClamp;
					case TextureFiltering.Linear:
						return MyraEnvironment.GraphicsDevice.SamplerStates.LinearClamp;
					case TextureFiltering.Anisotropic:
						return MyraEnvironment.GraphicsDevice.SamplerStates.AnisotropicClamp;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
#endif

			public void Begin()
			{
				if (_beginCalled)
				{
					throw new Exception("Begin was called already.");
				}

				_beginCalled = true;
			}

			public void End()
			{
				if (!_beginCalled)
				{
					throw new Exception("Begin wasn't called.");
				}

				_beginCalled = false;
				Flush();
			}

			private void BeginSprite()
			{
#if MONOGAME || FNA
				var samplerState = SelectedSamplerState();

				_renderer.Begin(SpriteSortMode.Deferred,
					BlendState.AlphaBlend,
					samplerState,
					null,
					UIRasterizerState,
					null);
#elif STRIDE
				var samplerState = SelectedSamplerState();

				_renderer.Begin(MyraEnvironment.Game.GraphicsContext,
					SpriteSortMode.Deferred,
					BlendStates.AlphaBlend,
					samplerState,
					null,
					_uiRasterizerState);
#else
				if (_textureFiltering == null)
				{
					throw new Exception("TextureFiltering can'be null in Sprite mode.");
				}

				_renderer.Begin(_textureFiltering.Value);
#endif
			}

			private void SetState(ModeType? mode, TextureFiltering? textureFiltering)
			{
				if (_mode == mode && _textureFiltering == textureFiltering)
				{
					return;
				}

				// End existing mode
				switch (_mode)
				{
					case ModeType.Sprite:
						_renderer.End();
						break;
					case ModeType.SDF:
#if MONOGAME || FNA
						SDFTextBatch.End();
						break;
#else
						throw new NotSupportedException("SDF rendering is not supported by Myra.Stride/Myra.PlatformaAgnostic.");
#endif
				}


				// Set the new state
				_mode = mode;
				_textureFiltering = textureFiltering;

				// Start the new mode
				switch (_mode)
				{
					case ModeType.Sprite:
						BeginSprite();
						break;
					case ModeType.SDF:
#if MONOGAME || FNA
						var sdfTextBatch = SDFTextBatch;
						sdfTextBatch.RasterizerState = UIRasterizerState;
						sdfTextBatch.Supersampling = MyraEnvironment.SDFSupersampling;
						sdfTextBatch.Begin();
						break;
#else
						throw new NotSupportedException("SDF rendering is not supported by Myra.Stride/Myra.PlatformaAgnostic.");
#endif
				}
			}

			private void Flush()
			{
				SetState(null, null);
			}

			private void SetTextState(SpriteFontBase font)
			{
				switch (font.FontRasterizationMode)
				{
					case FontRasterizationMode.Standard:
						SetState(ModeType.Sprite, TextTextureFiltering);
						break;
					case FontRasterizationMode.SDF:
						SetState(ModeType.SDF, null);
						break;
					default:
						throw new NotImplementedException($"Font Rasterization Mode {font.FontRasterizationMode} isn't supported.");
				}
			}
		}
	}
}
