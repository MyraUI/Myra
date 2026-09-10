using AssetManagementBase;
using System;
using System.Collections.Generic;
using Xunit;

namespace Myra.Tests
{
	[Collection("Myra Tests")]
	public class AssetLoadingTests
	{
		private AssetManager CreateAssetManager() => Utility.CreateAssetManager();

		[Fact]
		public void LoadTextureRegionAtlas_ValidAtlas_LoadsSuccessfully()
		{
			var assetManager = CreateAssetManager();

			var atlas = assetManager.LoadTextureRegionAtlas("Stylesheets/Default/default_ui_skin.xmat");

			Assert.NotNull(atlas);
			Assert.NotNull(atlas.Regions);
			Assert.Equal(106, atlas.Regions.Count);
			Assert.NotNull(atlas.Texture);
			Assert.Equal(1024, atlas.Texture.Width);
			Assert.Equal(1024, atlas.Texture.Height);
		}

		[Fact]
		public void LoadTextureRegion_AtlasColonFormat_LoadsSuccessfully()
		{
			var assetManager = CreateAssetManager();

			var region = assetManager.LoadTextureRegion("Stylesheets/Default/default_ui_skin.xmat:button");

			Assert.NotNull(region);
			Assert.NotNull(region.Texture);
			Assert.Equal(12, region.Bounds.Width);
			Assert.Equal(20, region.Bounds.Height);
			Assert.Equal(121, region.Bounds.X);
			Assert.Equal(0, region.Bounds.Y);
		}

		[Fact]
		public void LoadTextureRegion_AtlasFormatMissingRegion_ThrowsKeyNotFoundException()
		{
			var assetManager = CreateAssetManager();

			// Missing region name results in KeyNotFoundException for empty string
			var ex = Assert.Throws<AssetNotFoundException>(() => assetManager.LoadTextureRegion("Stylesheets/Default/default_ui_skin.xmat:"));

			Assert.NotNull(ex);
		}

		[Fact]
		public void LoadTextureRegion_InvalidAtlasRegion_ThrowsKeyNotFoundException()
		{
			var assetManager = CreateAssetManager();

			var ex = Assert.Throws<KeyNotFoundException>(() => assetManager.LoadTextureRegion("Stylesheets/Default/default_ui_skin.xmat:nonExistentRegion"));

			Assert.NotNull(ex);
		}

		[Fact]
		public void LoadTextureRegion_FilePath_LoadsSuccessfully()
		{
			var assetManager = CreateAssetManager();

			var region = assetManager.LoadTextureRegion("MonoGameLogo.png");

			Assert.NotNull(region);
			Assert.NotNull(region.Texture);
			Assert.Equal(64, region.Bounds.Width);
			Assert.Equal(64, region.Bounds.Height);
			Assert.Equal(0, region.Bounds.X);
			Assert.Equal(0, region.Bounds.Y);
		}

		[Fact]
		public void LoadFont_BMFontFile_LoadsSuccessfully()
		{
			var assetManager = CreateAssetManager();
			var assetName = "arial64.fnt";

			var font = assetManager.LoadFont(assetName);

			Assert.NotNull(font);
			Assert.Equal(assetName, font.Name);
			Utility.AssertEqualEpsilon(63f, font.FontSize);
		}

		[Fact]
		public void LoadFont_TTFFile_LoadsSuccessfully()
		{
			var assetManager = CreateAssetManager();
			var assetName = "Stylesheets/Default/Inter-Regular.ttf:32";

			var font = assetManager.LoadFont(assetName);

			Assert.NotNull(font);
			Assert.Equal(assetName, font.Name);
			Utility.AssertEqualEpsilon(32f, font.FontSize);
		}

		[Fact]
		public void LoadFont_TTFMultipleSizes_ReturnsDifferentInstances()
		{
			var assetManager = CreateAssetManager();
			var assetName16 = "Stylesheets/Default/Inter-Regular.ttf:16";
			var assetName32 = "Stylesheets/Default/Inter-Regular.ttf:32";

			var font16 = assetManager.LoadFont(assetName16);
			var font32 = assetManager.LoadFont(assetName32);

			Assert.NotNull(font16);
			Assert.NotNull(font32);
			Assert.Equal(assetName16, font16.Name);
			Assert.Equal(assetName32, font32.Name);
			Assert.NotEqual(font16, font32);
			Utility.AssertEqualEpsilon(16f, font16.FontSize);
			Utility.AssertEqualEpsilon(32f, font32.FontSize);
		}

		[Fact]
		public void LoadFont_TTFWithInvalidSize_ThrowsFormatException()
		{
			var assetManager = CreateAssetManager();

			var ex = Assert.Throws<Exception>(() =>
				assetManager.LoadFont("Stylesheets/Default/Inter-Regular.ttf:notanumber"));

			Assert.NotNull(ex);
		}

		[Fact]
		public void LoadFont_TTFWithNegativeSize_ThrowsArgumentOutOfRangeException()
		{
			var assetManager = CreateAssetManager();

			// Negative size is invalid and throws ArgumentOutOfRangeException
			var ex = Assert.Throws<Exception>(() =>
				assetManager.LoadFont("Stylesheets/Default/Inter-Regular.ttf:-32"));

			Assert.NotNull(ex);
		}

		[Fact]
		public void LoadFont_TTFWithoutSize_ThrowsException()
		{
			var assetManager = CreateAssetManager();

			var ex = Assert.Throws<Exception>(() =>
				assetManager.LoadFont("Stylesheets/Default/Inter-Regular.ttf"));

			Assert.NotNull(ex);
		}

		[Fact]
		public void LoadStylesheet_FontsHaveCorrectProperties()
		{
			var assetManager = CreateAssetManager();

			var stylesheet = assetManager.LoadStylesheet("Stylesheets/Default/default_ui_skin.xmms");

			var defaultFont = stylesheet.Fonts["default-font"];
			Assert.NotNull(defaultFont);
			Assert.NotNull(defaultFont.Font);
			Assert.Equal("default-font", defaultFont.Id);
			Assert.Equal("Inter-Regular.ttf", defaultFont.File);
			Assert.Equal(20, defaultFont.Size);
		}

		[Fact]
		public void LoadStylesheet_HasValidAtlas()
		{
			var assetManager = CreateAssetManager();

			var stylesheet = assetManager.LoadStylesheet("Stylesheets/Default/default_ui_skin.xmms");

			Assert.NotNull(stylesheet.Atlas);
			Assert.Equal(106, stylesheet.Atlas.Regions.Count);
			Assert.NotNull(stylesheet.Atlas.Texture);
			Assert.True(stylesheet.Atlas.Regions.ContainsKey("button"));
			Assert.True(stylesheet.Atlas.Regions.ContainsKey("cursor"));
		}
	}
}
