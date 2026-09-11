using System;
using System.IO;
using System.Linq;
using Myra.Mcp;
using Xunit;

namespace Myra.Mcp.Tests;

public class StylesheetInspectionTests
{
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");

    private static StylesheetService ServiceRootedAtAssets() => new(new MyraWorkspace(AssetsDir));

    [Fact]
    public void Inspects_custom_stylesheet_fonts_regions_and_named_styles()
    {
        // named_styles.xmms references the Commodore64 atlas (regions "button", "button-down"),
        // the commodore-64 font, and defines a named "danger" ButtonStyle alongside the default.
        var info = ServiceRootedAtAssets().Inspect("Stylesheets/Commodore64/named_styles.xmms");

        Assert.Contains("commodore-64", info.Fonts);
        Assert.Contains("button", info.AtlasRegions);
        Assert.Contains("button-down", info.AtlasRegions);

        var buttonStyles = info.Styles.Single(g => g.Widget == "Button");
        Assert.Contains("danger", buttonStyles.Names);
        Assert.DoesNotContain("", buttonStyles.Names); // the default style is not a referenceable name
    }

    [Fact]
    public void Inspects_default_stylesheet_when_no_path_given()
    {
        // Exercises the no-path branch (the built-in default skin). Its three collections are always
        // returned as well-formed arrays without loading anything from the workspace.
        var info = ServiceRootedAtAssets().Inspect();

        Assert.NotNull(info.Styles);
        Assert.NotNull(info.Fonts);
        Assert.NotNull(info.AtlasRegions);
    }

    [Fact]
    public void Rejects_stylesheet_path_outside_root()
    {
        Assert.Throws<ArgumentException>(
            () => ServiceRootedAtAssets().Inspect("../../../../etc/skin.xmms"));
    }
}
