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
	public static partial class MyraAssets
	{
		private static AssetManager _defaultAssetManager;

		/// <summary>
		/// Gets or sets the asset manager used to load default assets.
		/// </summary>
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

		public static TextureRegion LoadTextureRegion(string assetName, Stylesheet customStylesheet = null, AssetManager customAssetManager = null)
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

			if (!assetName.Contains("."))
			{
				// If there's no extension, assume it's a current stylesheet font
				if (!stylesheet.Fonts.TryGetValue(assetName, out var font))
				{
					throw new Exception($"Font '{assetName}' not found in current stylesheet.");
				}

				var result = font.Font;
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

				result.Name = originalAssetName;

				return result;
			}

			return assetManager.LoadFont(assetName, fontSize);
		}
	}
}
