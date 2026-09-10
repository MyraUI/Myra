using System;
using FontStashSharp;
using Myra;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;



#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
using Color = FontStashSharp.FSColor;
#endif

namespace AssetManagementBase
{
	/// <summary>
	/// Provides extension methods for the AssetManager class to load Myra-specific assets like texture atlases, fonts, and stylesheets.
	/// </summary>
	public static partial class MyraAssetManagerExtensions
	{
		private static AssetLoader<StaticSpriteFont> _staticFontLoader = (manager, assetName, settings, tag) =>
		{
			var fontData = manager.ReadAsString(assetName);

			var result = StaticSpriteFont.FromBMFont(fontData,
						name =>
						{
							var region = LoadTextureRegion(manager, name);
							return new TextureWithOffset(region.Texture, region.Bounds.Location);
						});

			return result;
		};

		private static AssetLoader<TextureRegionAtlas> _atlasLoader = (manager, assetName, settings, tag) =>
		{
			var data = manager.ReadAsString(assetName);

#if !PLATFORM_AGNOSTIC
			var result = TextureRegionAtlas.FromXml(data, name => manager.LoadTexture2D(MyraEnvironment.GraphicsDevice, name, true));
#else
			var result = TextureRegionAtlas.FromXml(data, name => manager.LoadTexture2D(name).Texture);
#endif

			result.Name = assetName;

			return result;
		};

		private static AssetLoader<Project> _projectLoader = (manager, assetName, settings, tag) =>
		{
			var data = manager.ReadAsString(assetName);

			return Project.LoadFromXml(data, manager);
		};

		/// <summary>
		/// Loads a texture region atlas from an XML asset file.
		/// </summary>
		/// <param name="assetManager">The asset manager instance.</param>
		/// <param name="assetName">The name of the atlas asset to load.</param>
		/// <returns>The loaded texture region atlas.</returns>
		public static TextureRegionAtlas LoadTextureRegionAtlas(this AssetManager assetManager, string assetName) => assetManager.UseLoader(_atlasLoader, assetName);

		/// <summary>
		/// Loads a texture region from an asset, either from a texture region atlas or as a standalone texture.
		/// </summary>
		/// <remarks>
		/// Examples: "atlas.xmat:commodore-64" (a region of a named atlas) or "image.png" (a standalone texture).
		/// </remarks>
		/// <param name="assetManager">The asset manager instance.</param>
		/// <param name="assetName">The name of the asset to load.</param>
		/// <returns>The loaded texture region.</returns>
		public static TextureRegion LoadTextureRegion(this AssetManager assetManager, string assetName)
		{
			string regionName;
			if (TextureRegionAtlas.TryGetRegionName(ref assetName, out regionName))
			{
				// Atlas:region. I.e. "ui_stylesheet.xmat:commodore-64"
				var textureRegionAtlas = assetManager.LoadTextureRegionAtlas(assetName);
				return textureRegionAtlas[regionName];
			}

			// Ordinary texture
#if MONOGAME || FNA || STRIDE
			var texture = assetManager.LoadTexture2D(MyraEnvironment.GraphicsDevice, assetName);
			var result = new TextureRegion(texture, new Rectangle(0, 0, texture.Width, texture.Height));
#else
			var texture = assetManager.LoadTexture2D(assetName);
			var result = new TextureRegion(texture.Texture, new Rectangle(0, 0, texture.Width, texture.Height));
#endif

			result.Name = assetName;

			return result;
		}

		/// <summary>
		/// Loads a Myra project from an XML asset file.
		/// </summary>
		/// <param name="assetManager">The asset manager instance.</param>
		/// <param name="assetName">The name of the project asset to load.</param>
		/// <returns>The loaded project.</returns>
		public static Project LoadProject(this AssetManager assetManager, string assetName) => assetManager.UseLoader(_projectLoader, assetName);

		private static StaticSpriteFont MyraLoadStaticSpriteFont(this AssetManager assetManager, string assetName) => assetManager.UseLoader(_staticFontLoader, assetName);

		internal static SpriteFontBase LoadFont(this AssetManager assetManager, string assetName, int? fontSize)
		{
			SpriteFontBase result = null;

			do
			{
				if (assetName.Contains(".fnt"))
				{
					result = assetManager.MyraLoadStaticSpriteFont(assetName);
					break;
				}

				if (assetName.Contains(".ttf") || assetName.Contains(".otf"))
				{
					if (fontSize == null)
					{
						throw new Exception("Missing font size.");
					}

					var fontSystem = assetManager.LoadFontSystem(assetName);
					result = fontSystem.GetFont(fontSize.Value);
					break;
				}
			}
			while (false);

			if (result == null)
			{
				throw new Exception(string.Format("Can't load font '{0}'", assetName));
			}

			return result;

		}

		/// <summary>
		/// Loads a sprite font by name, optionally parsing a size parameter from the asset name.
		/// </summary>
		/// <remarks>
		/// Examples: "fonts/arial64.fnt" (a static font) or "fonts/comic.ttf:48" (a dynamic font). The size after ':' is only valid for dynamic fonts, not static .fnt fonts.
		/// </remarks>
		/// <param name="assetManager">The asset manager instance.</param>
		/// <param name="assetName">The name of the font asset, optionally with a size parameter.</param>
		/// <returns>The loaded sprite font.</returns>
		public static SpriteFontBase LoadFont(this AssetManager assetManager, string assetName)
		{
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

			var result = assetManager.LoadFont(assetName, fontSize);

			result.Name = originalAssetName;
			return result;
		}
	}
}
