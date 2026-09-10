using Myra.Graphics2D.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace Myra.Samples;

public class ScalingGame : Game
{
	private readonly GraphicsDeviceManager _graphics;
	private Desktop _desktop;
	private SpriteBatch _spriteBatch;

	public ScalingGame()
	{
		_graphics = new GraphicsDeviceManager(this)
		{
			PreferredBackBufferWidth = 1200,
			PreferredBackBufferHeight = 800
		};
		Window.AllowUserResizing = true;
		IsMouseVisible = true;
	}

	protected override void LoadContent()
	{
		base.LoadContent();

		MyraEnvironment.Game = this;

		//			Stylesheet.Current = DefaultAssets.DefaultStylesheet2X;

		var mainForm = new MainForm();

		_desktop = new Desktop
		{
			Root = mainForm
		};

		_spriteBatch = new SpriteBatch(GraphicsDevice);

#if MONOGAME && !ANDROID
		// Inform Myra that external text input is available
		// So it stops translating Keys to chars
		_desktop.HasExternalTextInput = true;

		// Provide that text input
		Window.TextInput += (s, a) =>
		{
			_desktop.OnChar(a.Character);
		};
#endif
	}

	protected override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);

		GraphicsDevice.Clear(Color.Black);
		_desktop.Render();

		var fontSystem = DefaultAssets.DefaultStylesheet.Fonts.First().Font.FontSystem;
		if (fontSystem.Atlases.Count > 0)
		{
			_spriteBatch.Begin();

			var atlas = fontSystem.Atlases[0].Texture;

			// _spriteBatch.Draw(atlas, Vector2.Zero, Color.White);

			_spriteBatch.End();
		}
	}
}