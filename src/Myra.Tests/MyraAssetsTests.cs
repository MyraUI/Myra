using AssetManagementBase;
using FontStashSharp.RichText;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.TextureAtlases;
using System;
using Xunit;

namespace Myra.Tests
{
	[Collection("Myra Tests")]
	public class MyraAssetsTests
	{
		private AssetManager CreateAssetManager() => Utility.CreateAssetManager();

		[Fact]
		public void LoadImage_ColorSeparatorButNoColor_ThrowsException()
		{
			var assetManager = CreateAssetManager();

			// Empty color name throws because it cannot be parsed
			var ex = Assert.Throws<Exception>(() => MyraAssets.LoadImage("MonoGameLogo.png|", customAssetManager: assetManager));

			Assert.NotNull(ex);
		}

		[Fact]
		public void LoadImage_InvalidColorName_IgnoresColor()
		{
			var assetManager = CreateAssetManager();

			// Invalid color names are silently ignored - the image loads without tint
			var ex = Assert.Throws<Exception>(() => MyraAssets.LoadImage("MonoGameLogo.png|invalidColorName", customAssetManager: assetManager));

			Assert.NotNull(ex);
		}

		[Theory]
		[InlineData("red")]
		[InlineData("green")]
		[InlineData("blue")]
		[InlineData("white")]
		[InlineData("#FF0000")]
		[InlineData("#00FF00")]
		[InlineData("#0000FFFF")]
		public void LoadImage_ImageWithColorTint_AppliesTint(string colorName)
		{
			var assetManager = CreateAssetManager();
			var region = MyraAssets.LoadImage($"MonoGameLogo.png|{colorName}", customAssetManager: assetManager);
			Assert.IsType<TintedRegion>(region);

			var tintedRegion = (TintedRegion)region;
			Assert.NotNull(tintedRegion);
			Assert.NotNull(tintedRegion.Region);
			Assert.NotNull(tintedRegion.Region.Texture);
			Assert.Equal(64, tintedRegion.Size.X);
			Assert.Equal(64, tintedRegion.Size.Y);
			var expectedColor = ColorStorage.FromName(colorName).Value;
			Assert.Equal(expectedColor, tintedRegion.Color);
		}

		[Theory]
		[InlineData("red")]
		[InlineData("green")]
		[InlineData("blue")]
		[InlineData("yellow")]
		[InlineData("#FF0000")]
		[InlineData("#00FF00")]
		[InlineData("#0000FF")]
		public void LoadBrush_ColorName_ReturnsSolidBrush(string colorName)
		{
			var brush = MyraAssets.LoadBrush(colorName);

			Assert.NotNull(brush);
			var solidBrush = Assert.IsType<SolidBrush>(brush);
			var expectedColor = ColorStorage.FromName(colorName).Value;
			Assert.Equal(expectedColor, solidBrush.Color);
		}

		[Fact]
		public void LoadBrush_InvalidColorAndNoImage_ThrowsException()
		{
			var ex = Assert.Throws<Exception>(() => MyraAssets.LoadBrush("invalidColor123"));

			Assert.NotNull(ex);
		}

		[Fact]
		public void LoadBrush_WithStylesheet_ResolvesBrush()
		{
			var assetManager = CreateAssetManager();

			var stylesheet = assetManager.LoadStylesheet("Stylesheets/Default/default_ui_skin.xmms");
			var brush = MyraAssets.LoadBrush("button", stylesheet);

			Assert.NotNull(brush);
		}

		[Fact]
		public void LoadFont_FromStylesheet_ReturnsCorrectFont()
		{
			var assetManager = CreateAssetManager();
			var assetName = "default-font";

			var stylesheet = assetManager.LoadStylesheet("Stylesheets/Default/default_ui_skin.xmms");
			var font = MyraAssets.LoadFont(assetName, stylesheet);

			Assert.NotNull(font);
			Assert.Equal(assetName, font.Name);
			Utility.AssertEqualEpsilon(20f, font.FontSize);
		}

		[Fact]
		public void LoadFont_NonExistentStylesheetFont_ThrowsException()
		{
			var assetManager = CreateAssetManager();

			var stylesheet = assetManager.LoadStylesheet("Stylesheets/Default/default_ui_skin.xmms");

			var ex = Assert.Throws<Exception>(() => MyraAssets.LoadFont("nonExistentFont", stylesheet));

			Assert.NotNull(ex);
		}

		[Fact]
		public void LoadStylesheet_DefaultFontWithSize()
		{
			var assetManager = CreateAssetManager();

			var stylesheet = assetManager.LoadStylesheet("Stylesheets/Default/default_ui_skin.xmms");
			var defaultFont = MyraAssets.LoadFont("default-font:36", stylesheet);

			Assert.NotNull(defaultFont);
			Assert.Equal("default-font:36", defaultFont.Name);
			Assert.Equal(36, (int)Math.Round(defaultFont.FontSize));
		}

		[Fact]
		public void LoadStylesheet_DefaultFontNotExistant()
		{
			var assetManager = CreateAssetManager();

			var stylesheet = assetManager.LoadStylesheet("Stylesheets/Default/default_ui_skin.xmms");
			var ex = Assert.Throws<Exception>(() => MyraAssets.LoadFont("default-font2", stylesheet));

			Assert.NotNull(ex);
		}

		[Fact]
		public void LoadStylesheet_FontSizeCantBeModified()
		{
			var assetManager = CreateAssetManager();

			var stylesheet = assetManager.LoadStylesheet("Stylesheets/Commodore64/ui_stylesheet.xmms");
			var ex = Assert.Throws<Exception>(() => MyraAssets.LoadFont("commodore-64:32", stylesheet));

			Assert.NotNull(ex);
		}
	}
}
