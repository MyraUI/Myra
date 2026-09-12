using System;
using System.Reflection;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using AssetManagementBase;
using Myra.Graphics2D.UI;
using System.Collections.Generic;
using FontStashSharp;
using Myra.Events;



#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;


#if MONOGAME
using MonoGame.Framework.Utilities;
#endif

#if FNA
using static SDL3.SDL;
#endif

#elif STRIDE
using Stride.Engine;
using Stride.Graphics;
using Stride.Core.Mathematics;
using Stride.Input;
#else
using Myra.Platform;
using System.Drawing;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra
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
	/// Provides global configuration and utility methods for Myra UI framework.
	/// </summary>
	public static partial class MyraEnvironment
	{
#if MONOGAME
		private static readonly Dictionary<MouseCursorType, MouseCursor> _mouseCursors = new Dictionary<MouseCursorType, MouseCursor>
		{
			[MouseCursorType.Arrow] = MouseCursor.Arrow,
			[MouseCursorType.IBeam] = MouseCursor.IBeam,
			[MouseCursorType.Wait] = MouseCursor.Wait,
			[MouseCursorType.Crosshair] = MouseCursor.Crosshair,
			[MouseCursorType.WaitArrow] = MouseCursor.WaitArrow,
			[MouseCursorType.SizeNWSE] = MouseCursor.SizeNWSE,
			[MouseCursorType.SizeNESW] = MouseCursor.SizeNESW,
			[MouseCursorType.SizeWE] = MouseCursor.SizeWE,
			[MouseCursorType.SizeNS] = MouseCursor.SizeNS,
			[MouseCursorType.SizeAll] = MouseCursor.SizeAll,
			[MouseCursorType.No] = MouseCursor.No,
			[MouseCursorType.Hand] = MouseCursor.Hand,
		};
#elif FNA
		private static readonly Dictionary<SDL_SystemCursor, IntPtr> _systemCursors = new Dictionary<SDL_SystemCursor, IntPtr>();

		private static IntPtr GetSystemCursor(SDL_SystemCursor type)
		{
			IntPtr result;
			if (_systemCursors.TryGetValue(type, out result))
			{
				return result;
			}

			result = SDL_CreateSystemCursor(type);
			_systemCursors[type] = result;

			return result;
		}

		private static readonly Dictionary<MouseCursorType, SDL_SystemCursor> _mouseCursors = new Dictionary<MouseCursorType, SDL_SystemCursor>
		{
			[MouseCursorType.Arrow] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_DEFAULT,
			[MouseCursorType.IBeam] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_TEXT,
			[MouseCursorType.Wait] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_WAIT,
			[MouseCursorType.Crosshair] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_CROSSHAIR,
			[MouseCursorType.WaitArrow] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_PROGRESS,
			[MouseCursorType.SizeNWSE] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_NWSE_RESIZE,
			[MouseCursorType.SizeNESW] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_NESW_RESIZE,
			[MouseCursorType.SizeWE] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_EW_RESIZE,
			[MouseCursorType.SizeNS] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_NS_RESIZE,
			[MouseCursorType.SizeAll] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_MOVE,
			[MouseCursorType.No] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_NOT_ALLOWED,
			[MouseCursorType.Hand] = SDL_SystemCursor.SDL_SYSTEM_CURSOR_POINTER,
		};
#endif

		private static MouseCursorType _mouseCursorType;

		/// <summary>
		/// Gets the version number of Myra.
		/// </summary>
		public static string Version
		{
			get
			{
				var assembly = typeof(MyraEnvironment).Assembly;
				var name = new AssemblyName(assembly.FullName);

				return name.Version.ToString();
			}
		}

		/// <summary>
		/// Gets or sets the event handling strategy used for input events (event capturing or event bubbling).
		/// </summary>
		public static EventHandlingStrategy EventHandlingModel { get; set; } = EventHandlingStrategy.EventCapturing;

		/// <summary>
		/// Gets or sets a value indicating whether the mouse cursor type should be automatically changed based on the hovered widget.
		/// </summary>
		public static bool SetMouseCursorFromWidget { get; set; } = true;

		/// <summary>
		/// Gets or sets the current mouse cursor type displayed.
		/// </summary>
		public static MouseCursorType MouseCursorType
		{
			get => _mouseCursorType;
			set
			{
				if (_mouseCursorType == value)
				{
					return;
				}

				_mouseCursorType = value;
#if MONOGAME
				MouseCursor mouseCursor;
				if (!_mouseCursors.TryGetValue(value, out mouseCursor))
				{
					throw new Exception($"Could not find mouse cursor {value}");
				}

				Mouse.SetCursor(mouseCursor);
#elif FNA
				SDL_SystemCursor mouseCursor;
				if (!_mouseCursors.TryGetValue(value, out mouseCursor))
				{
					throw new Exception($"Could not find mouse cursor {value}");
				}

				var mouseCursorPtr = GetSystemCursor(mouseCursor);
				SDL_SetCursor(mouseCursorPtr);
#elif PLATFORM_AGNOSTIC
				Platform.SetMouseCursorType(value);
#endif
			}
		}

		/// <summary>
		/// Gets or sets the default mouse cursor type to display when no widget-specific cursor is active.
		/// </summary>
		public static MouseCursorType DefaultMouseCursorType { get; set; }

#if MONOGAME || FNA || STRIDE

		private static Game _game;

		/// <summary>
		/// Gets or sets the game instance that Myra uses for rendering. Must be set before using Myra.
		/// </summary>
		public static Game Game
		{
			get
			{
				if (_game == null)
				{
					throw new Exception("MyraEnvironment.Game is null. Please, set it to the Game instance before using Myra.");
				}

				return _game;
			}

			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}

				if (_game == value)
				{
					return;
				}

#if !STRIDE
				if (_game != null)
				{
					_game.Disposed -= GameOnDisposed;
				}
#endif

				_game = value;

#if !STRIDE
				if (_game != null)
				{
					_game.Disposed += GameOnDisposed;
				}
#endif
			}
		}

		/// <summary>
		/// Gets the graphics device from the current game instance.
		/// </summary>
		public static GraphicsDevice GraphicsDevice
		{
			get => Game.GraphicsDevice;
		}
#else

		private static IMyraPlatform _platform;

		/// <summary>
		/// Gets or sets the platform abstraction layer used for platform-agnostic rendering.
		/// </summary>
		public static IMyraPlatform Platform
		{
			get
			{
				if (_platform == null)
				{
					throw new Exception("MyraEnvironment.Platform is null. Please, set it before using Myra.");
				}

				return _platform;
			}

			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}

				_platform = value;
			}
		}
#endif

		/// <summary>
		/// Gets a value indicating whether the current platform is a mobile platform.
		/// </summary>
		public static bool IsMobile
		{
			get
			{
#if MONOGAME
				return PlatformInfo.MonoGamePlatform == MonoGamePlatform.Android ||
					PlatformInfo.MonoGamePlatform == MonoGamePlatform.iOS;
#else
				return false;
#endif
			}
		}

		/// <summary>
		/// Gets or sets the asset manager used to load default assets.
		/// </summary>
		[Obsolete("Use MyraAssets.DefaultAssetManager")]
		public static AssetManager DefaultAssetManager
		{
			get => MyraAssets.DefaultAssetManager;
			set => MyraAssets.DefaultAssetManager = value;
		}

		/// <summary>
		/// Gets or sets the delay in milliseconds before showing a tooltip.
		/// </summary>
		public static int TooltipDelayInMs { get; set; } = 500;

		/// <summary>
		/// Gets or sets the offset from the mouse cursor where tooltips are displayed.
		/// </summary>
		public static Point TooltipOffset { get; set; } = new Point(0, 20);

		/// <summary>
		/// Gets or sets the function used to create tooltip widgets.
		/// </summary>
		public static Func<Widget, Widget> TooltipCreator { get; set; } = w =>
		{
			var tooltip = new Label(null)
			{
				Text = w.Tooltip,
				Tag = w
			};

			tooltip.ApplyLabelStyle(Stylesheet.Current.TooltipStyle);

			return tooltip;
		};

		/// <summary>
		/// Gets or sets the default texture filtering mode for images.
		/// </summary>
		public static TextureFiltering ImageTextureFiltering { get; set; } = TextureFiltering.Nearest;
		/// <summary>
		/// Gets or sets the default texture filtering mode for text.
		/// </summary>
		public static TextureFiltering TextTextureFiltering { get; set; } = TextureFiltering.Nearest;

		/// <summary>
		/// Gets or sets a value indicating whether supersampling is used when rendering SDF text to improve quality when scaling.
		/// </summary>
		public static bool SDFSupersampling { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether text rendering should be smoothed (especially when scaling) at the cost of performance.
		/// </summary>
		[Obsolete("Use TextTextureFiltering")]
		public static bool SmoothText
		{
			get => TextTextureFiltering == TextureFiltering.Linear;

			set
			{
				TextTextureFiltering = value ? TextureFiltering.Linear : TextureFiltering.Nearest;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether modal dialogs should darken the background.
		/// </summary>
		public static bool EnableModalDarkening { get; set; }

		/// <summary>
		/// Gets or sets the color used to darken the background when modal dialogs are displayed.
		/// </summary>
		public static Color DarkeningColor { get; set; } = new Color(0, 0, 0, 192);

		private static void GameOnDisposed(object sender, EventArgs eventArgs)
		{
			Reset();
		}

		/// <summary>
		/// Resets the Myra environment, disposing of cached assets and stylesheets.
		/// </summary>
		public static void Reset()
		{
			DefaultAssets.Reset();
			Stylesheet.Current = null;
		}

		internal static string InternalClipboard;
	}
}