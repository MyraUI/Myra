using System;
using AssetManagementBase;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI.Styles;
using FontStashSharp;
using FontStashSharp.RichText;



#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra
{
	/// <summary>
	/// Provides methods for loading Myra assets such as textures, images, brushes, and fonts.
	/// </summary>
	public static partial class MyraAssets
	{
		private static AssetManager _defaultAssetManager;

		/// <summary>
		/// Gets or sets the asset manager used to load external assets.
		/// </summary>
		/// <remarks>
		/// Used to load external assets when no custom asset manager is provided to the Load* methods. If not set, it is lazily created as a file asset manager rooted at the application base directory.
		/// </remarks>
		public static AssetManager DefaultAssetManager
		{
			get
			{
				if (_defaultAssetManager == null)
				{
					_defaultAssetManager = AssetManager.CreateFileAssetManager(AppContext.BaseDirectory);
				}

				return _defaultAssetManager;
			}

			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));

				}
				_defaultAssetManager = value;
			}
		}

		/// <summary>
		/// Loads a texture region by name, resolving it from the current stylesheet atlas or an asset manager.
		/// </summary>
		/// <remarks>
		/// Examples: "icon-folder" (a region of the current stylesheet atlas), "atlas.xmat:commodore-64" (a region of a named atlas), or "images/LogoOnly_64px.png" (a standalone texture).
		/// </remarks>
		/// <param name="assetName">The name of the texture region asset.</param>
		/// <param name="customStylesheet">An optional stylesheet to use instead of the current one.</param>
		/// <param name="customAssetManager">An optional asset manager. If null, <see cref="DefaultAssetManager"/> is used to load external assets.</param>
		/// <returns>The loaded texture region.</returns>
		private static TextureRegion LoadTextureRegion(string assetName, Stylesheet customStylesheet = null, AssetManager customAssetManager = null)
		{
			var stylesheet = customStylesheet ?? Stylesheet.Current;
			if (stylesheet != null && assetName.IndexOf(TextureRegionAtlas.Separator) == -1 && !assetName.Contains("."))
			{
				// If there's no extension, assume it's a texture region of the stylesheet atlas
				var textureRegionAtlas = stylesheet.Atlas;
				return textureRegionAtlas[assetName];
			}

			var assetManager = customAssetManager ?? DefaultAssetManager;
			return assetManager.LoadTextureRegion(assetName);
		}

		/// <summary>
		/// Loads an image by name, optionally applying a tint color specified in the asset name.
		/// </summary>
		/// <remarks>
		/// A tint color can be appended to the asset name after a '|' separator, e.g. "MonoGameLogo.png|red" or "MonoGameLogo.png|#FF0000".
		/// </remarks>
		/// <param name="assetName">The name of the image asset, optionally with a tint color suffix.</param>
		/// <param name="customStylesheet">An optional stylesheet to use instead of the current one.</param>
		/// <param name="customAssetManager">An optional asset manager. If null, <see cref="DefaultAssetManager"/> is used to load external assets.</param>
		/// <returns>The loaded image.</returns>
		public static IImage LoadImage(string assetName, Stylesheet customStylesheet = null, AssetManager customAssetManager = null)
		{
			Color? color = null;
			TintedRegion.TryParse(ref assetName, out color);

			var region = LoadTextureRegion(assetName, customStylesheet, customAssetManager);
			if (color == null)
			{
				return region;
			}

			return new TintedRegion(region, color.Value);
		}

		/// <summary>
		/// Loads a brush by name, resolving it as a stylesheet region, a solid color, or an image.
		/// </summary>
		/// <remarks>
		/// Examples: "red" or "#FF0000" (a solid color), "button" (a region of the current stylesheet atlas), or "MonoGameLogo.png|blue" (an optionally tinted image).
		/// </remarks>
		/// <param name="assetName">The name of the brush asset.</param>
		/// <param name="customStylesheet">An optional stylesheet to use instead of the current one.</param>
		/// <param name="customAssetManager">An optional asset manager. If null, <see cref="DefaultAssetManager"/> is used to load external assets.</param>
		/// <returns>The loaded brush.</returns>
		public static IBrush LoadBrush(string assetName, Stylesheet customStylesheet = null, AssetManager customAssetManager = null)
		{
			var stylesheet = customStylesheet ?? Stylesheet.Current;

			if (!assetName.Contains(".") && assetName.IndexOf(TintedRegion.Separator) == -1 && assetName.IndexOf(StylesheetFont.Separator) == -1)
			{
				// It's either a default stylesheet texture atlas region or color name
				if (stylesheet == null || !stylesheet.Atlas.Regions.TryGetValue(assetName, out var region))
				{
					// Color
					var color = ColorStorage.FromName(assetName);
					if (color == null)
					{
						throw new Exception($"Could not parse brush name '{assetName}'");
					}

					return new SolidBrush(color.Value);
				}
			}

			return LoadImage(assetName, customStylesheet, customAssetManager);
		}

		/// <summary>
		/// Loads a font by name, optionally specifying a font size parameter.
		/// </summary>
		/// <remarks>
		/// Examples: "default-font" (a font of the current stylesheet), "default-font:36" (same, at a custom size), "fonts/arial64.fnt" (a static font), or "fonts/comic.ttf:48" (a dynamic font). The size after ':' is only valid for dynamic fonts, not static .fnt fonts.
		/// </remarks>
		/// <param name="assetName">The name of the font asset, optionally with a size parameter.</param>
		/// <param name="customStylesheet">An optional stylesheet to use instead of the current one.</param>
		/// <param name="customAssetManager">An optional asset manager. If null, <see cref="DefaultAssetManager"/> is used to load external assets.</param>
		/// <returns>The loaded sprite font.</returns>
		public static SpriteFontBase LoadFont(string assetName, Stylesheet customStylesheet = null, AssetManager customAssetManager = null)
		{
			var assetManager = customAssetManager ?? DefaultAssetManager;
			var stylesheet = customStylesheet ?? Stylesheet.Current;

			int? fontSize = null;
			string parameter;

			var originalAssetName = assetName;
			if (StylesheetFont.TryGetParameter(ref assetName, out parameter))
			{
				int fs;
				if (!int.TryParse(parameter, out fs) || fs <= 0)
				{
					throw new Exception($"Invalid font size {fontSize}.");
				}

				fontSize = fs;
			}

			SpriteFontBase result;
			if (!assetName.Contains("."))
			{
				// If there's no extension, assume it's a current stylesheet font
				if (!stylesheet.Fonts.TryGetValue(assetName, out var font))
				{
					throw new Exception($"Font '{assetName}' not found in current stylesheet.");
				}

				result = font.Font;
				if (fontSize != null)
				{
					var asDynamicFont = result as DynamicSpriteFont;
					if (asDynamicFont != null)
					{
						// Custom font size
						result = asDynamicFont.FontSystem.GetFont(fontSize.Value);
					}
					else
					{
						throw new Exception($"Font '{assetName}' size can't be modified.");
					}
				}
			}
			else
			{
				result = assetManager.LoadFont(assetName, fontSize);
			}

			result.Name = originalAssetName;

			return result;
		}
	}
}
