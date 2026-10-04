using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace Myra.Samples;

public class FixedSizeGame : Game
{
	private const int FixedWidth = 1200;
	private const int FixedHeight = 800;

	private readonly GraphicsDeviceManager _graphics;
	private Desktop _desktop;
	private SpriteBatch _spriteBatch;

	public static FixedSizeGame Instance { get; private set; }

	public FixedSizeGame()
	{
		Instance = this;

		_graphics = new GraphicsDeviceManager(this)
		{
			PreferredBackBufferWidth = FixedWidth,
			PreferredBackBufferHeight = FixedHeight
		};
		Window.AllowUserResizing = true;
		IsMouseVisible = true;
	}

	protected override void LoadContent()
	{
		base.LoadContent();

		// Set the default font rasterization mode to SDF for better scaling
		FontSystemDefaults.FontRasterizationMode = FontRasterizationMode.SDF;

		MyraEnvironment.Game = this;

		// Set the default texture filtering to linear for smoother scaling
		MyraEnvironment.ImageTextureFiltering = TextureFiltering.Linear;

		var mainForm = new AllWidgets();

		_desktop = new Desktop
		{
			Root = mainForm,
			// Set the bounds fetcher to return a fixed size rectangle
			BoundsFetcher = () => new Rectangle(0, 0, FixedWidth, FixedHeight),
			TransformOrigin = Vector2.Zero
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

		var viewport = GraphicsDevice.Viewport;

		// Calculate the scale factor to fit the fixed size into the current viewport while maintaining aspect ratio
		_desktop.Scale = new Vector2((float)viewport.Width / FixedWidth, (float)viewport.Height / FixedHeight);
		_desktop.Render();
	}
}