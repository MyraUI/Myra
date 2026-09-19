using System;
using System.IO;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using NvgSharp;
using Color = Microsoft.Xna.Framework.Color;

namespace Myra.Samples;

/// <summary>
/// Sample game demonstrating three custom <see cref="IBrush"/> implementations:
/// <see cref="MatrixBrush"/> (digital rain text), <see cref="NvgBrush"/> (NvgSharp vector
/// graphics) and <see cref="AnimatedGifBrush"/> (animated GIFs decoded with StbImageSharp).
/// </summary>
public class CustomBrushesGame : Game
{
	private readonly GraphicsDeviceManager _graphics;

	private Desktop _desktop;
	private MatrixBrush _matrixBrush;
	private NvgBrush _nvgBrush;
	private AnimatedGifBrush _animatedGifBrush;

	private double _time;

	public CustomBrushesGame()
	{
		_graphics = new GraphicsDeviceManager(this)
		{
			PreferredBackBufferWidth = 1280,
			PreferredBackBufferHeight = 760,
			// Depth24Stencil8 is required by NvgSharp for stencil-based stroke rendering
			PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8
		};

		Window.AllowUserResizing = true;
		IsMouseVisible = true;
	}

	protected override void LoadContent()
	{
		base.LoadContent();

		FontSystemDefaults.FontRasterizationMode = FontRasterizationMode.SDF;

		MyraEnvironment.Game = this;

		var assetsFolder = Path.Combine(AppContext.BaseDirectory, "Assets");

		_matrixBrush = new MatrixBrush();

		_nvgBrush = new NvgBrush();
		_nvgBrush.PaintHandler = PaintNvgCanvas;

		_animatedGifBrush = new AnimatedGifBrush(GraphicsDevice, File.OpenRead(Path.Combine(assetsFolder, "neon.gif")));

		_desktop = new Desktop
		{
			Background = new SolidBrush(new Color(22, 28, 42)),
			Root = BuildUi()
		};

#if MONOGAME && !ANDROID
		// Inform Myra that external text input is available
		_desktop.HasExternalTextInput = true;
		Window.TextInput += (s, a) => _desktop.OnChar(a.Character);
#endif
	}

	protected override void Update(GameTime gameTime)
	{
		base.Update(gameTime);

		_time = gameTime.TotalGameTime.TotalSeconds;
	}

	protected override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);

		GraphicsDevice.Clear(Color.Black);
		_desktop.Render();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_animatedGifBrush?.Dispose();
			_animatedGifBrush = null;

			_desktop?.Dispose();
			_desktop = null;
		}

		base.Dispose(disposing);
	}

	private Widget BuildUi()
	{
		var layout = new VerticalStackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			Padding = new Thickness(32),
			Spacing = 24
		};

		layout.Widgets.Add(new Label
		{
			Text = "Myra Custom Brushes",
			HorizontalAlignment = HorizontalAlignment.Center
		});

		var cards = new HorizontalStackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Spacing = 40
		};

		cards.Widgets.Add(CreateCard("MatrixBrush\ndigital rain with DebugFont", _matrixBrush));
		cards.Widgets.Add(CreateCard("NvgBrush\nrendered with NvgSharp", _nvgBrush));
		cards.Widgets.Add(CreateCard("AnimatedGifBrush\ndecoded with StbImageSharp", _animatedGifBrush));

		layout.Widgets.Add(cards);

		layout.Widgets.Add(new Label
		{
			Text = "One AnimatedGifBrush instance reused by several widgets (the middle one is semi-transparent):",
			HorizontalAlignment = HorizontalAlignment.Center
		});

		var tiles = new HorizontalStackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Spacing = 20
		};

		for (var i = 0; i < 5; i++)
		{
			tiles.Widgets.Add(new Panel
			{
				Background = _animatedGifBrush,
				Width = 96,
				Height = 96,
				Opacity = i == 2 ? 0.5f : 1f
			});
		}

		layout.Widgets.Add(tiles);

		return layout;
	}

	/// <summary>
	/// Builds a demo card: a fixed-size <see cref="Panel"/> using the given brush as its
	/// background, with a caption label pinned to the bottom.
	/// </summary>
	private static Widget CreateCard(string caption, IBrush brush)
	{
		var card = new Panel
		{
			Background = brush,
			Width = 340,
			Height = 220,
			ClipToBounds = true
		};

		card.Widgets.Add(new Label
		{
			Text = caption,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(8),
			TextColor = new Color(255, 255, 255, 200)
		});

		return card;
	}

	/// <summary>
	/// Draws the NvgSharp demo scene inside the brush's destination rectangle:
	/// a dark panel, an animated radial glow and three rotating arcs with a comet.
	/// </summary>
	private void PaintNvgCanvas(NvgContext vg, Point size)
	{
		var t = (float)_time;
		var w = size.X;
		var h = size.Y;
		var cx = w * 0.5f;
		var cy = h * 0.5f;

		vg.SaveState();

		// Background panel
		vg.BeginPath();
		vg.RoundedRect(0, 0, w, h, 24);
		vg.FillColor(Rgba(14, 18, 28, 255));
		vg.Fill();

		// Radial glow
		var glow = vg.RadialGradient(cx, cy, 0, w * 0.7f, Rgba(64, 128, 255, 220), Rgba(64, 128, 255, 0));
		vg.BeginPath();
		vg.Rect(0, 0, w, h);
		vg.FillPaint(glow);
		vg.Fill();

		// Rotating arcs
		for (var i = 0; i < 3; i++)
		{
			var speed = 1f + i * 0.6f;
			var angle = t * speed;

			vg.BeginPath();
			vg.Arc(cx, cy, w * (0.14f + i * 0.09f), angle, angle + 1.3f, Winding.ClockWise);
			vg.StrokeWidth(7f - i);
			vg.StrokeColor(Rgba((byte)(90 + i * 50), (byte)(255 - i * 40), (byte)(200 + i * 20), 240));
			vg.Stroke();
		}

		// Comet
		var cometAngle = t * 2.3f;
		var px = cx + (float)Math.Cos(cometAngle) * w * 0.3f;
		var py = cy + (float)Math.Sin(cometAngle) * w * 0.3f;

		vg.BeginPath();
		vg.Circle(px, py, 9);
		vg.FillColor(Rgba(255, 255, 255, 255));
		vg.Fill();

		// Animated progress bar
		var progress = (float)(t * 0.5f % 1.0f);
		vg.BeginPath();
		vg.RoundedRect(24, h - 38, w - 48, 12, 6);
		vg.FillColor(Rgba(0, 0, 0, 140));
		vg.Fill();

		vg.BeginPath();
		vg.RoundedRect(24, h - 38, (w - 48) * progress, 12, 6);
		vg.FillColor(Rgba(80, 200, 120, 255));
		vg.Fill();

		vg.RestoreState();
	}

	private static Color Rgba(byte r, byte g, byte b, byte a)
	{
		return new Color(r, g, b, a);
	}
}