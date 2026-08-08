using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Myra.Mcp;
using Xunit;

namespace Myra.Mcp.Tests;

public class LayoutValidationTests
{
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");

    private static LayoutService ServiceRootedAtAssets() => new(new MyraWorkspace(AssetsDir));

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AssetsDir, name));

    private static int Indent(string line) => line.Length - line.TrimStart().Length;

    [Fact]
    public void Valid_layout_reports_valid_with_nested_widget_tree()
    {
        // checkButton.xmmp is Panel > CheckButton > VerticalStackPanel > Label x3.
        var result = ServiceRootedAtAssets().Validate(Fixture("checkButton.xmmp"), AssetsDir);

        Assert.True(result.Valid);
        Assert.Null(result.Error);

        var tree = result.WidgetTree!;
        var lines = tree.Split('\n');

        Assert.StartsWith("Panel", lines[0]); // root, no indentation
        var checkButton = lines.Single(l => l.TrimStart().StartsWith("CheckButton"));
        var stackPanel = lines.Single(l => l.TrimStart().StartsWith("VerticalStackPanel"));
        Assert.True(Indent(checkButton) > Indent(lines[0]), "CheckButton must nest under Panel");
        Assert.True(Indent(stackPanel) > Indent(checkButton), "VerticalStackPanel must nest under CheckButton");
        Assert.Equal(3, Regex.Matches(tree, @"(?m)^\s*Label\b").Count); // exactly three Labels
    }

    [Fact]
    public void Custom_stylesheet_path_is_used_for_validation()
    {
        var mml = "<Project><Panel><Label Text=\"hi\" /></Panel></Project>";
        var service = ServiceRootedAtAssets();

        // A real .xmms stylesheet is loaded and the layout validates against it.
        var withStylesheet = service.Validate(mml, AssetsDir, stylesheetPath: "Stylesheets/Commodore64/ui_stylesheet.xmms");
        Assert.True(withStylesheet.Valid);
        Assert.Contains("Label", withStylesheet.WidgetTree);

        // Proof the parameter is actually consulted (not ignored): pointing it at a stylesheet file
        // that does not exist makes stylesheet loading fail, so validation fails. If stylesheetPath
        // were dropped, this same MML would stay valid against the default skin.
        var withMissingStylesheet = service.Validate(mml, AssetsDir, stylesheetPath: "Stylesheets/DoesNotExist.xmms");
        Assert.False(withMissingStylesheet.Valid);
    }

    [Fact]
    public void Stylesheet_path_outside_root_is_rejected()
    {
        var mml = "<Project><Panel /></Project>";

        Assert.Throws<ArgumentException>(
            () => ServiceRootedAtAssets().Validate(mml, AssetsDir, stylesheetPath: "../../../../etc/skin.xmms"));
    }

    [Fact]
    public void Unknown_tag_reports_unknown_tag_error()
    {
        var mml = "<Project><Panel><Buton /></Panel></Project>";

        var result = ServiceRootedAtAssets().Validate(mml);

        Assert.False(result.Valid);
        Assert.NotNull(result.Error);
        Assert.Contains("Could not resolve tag", result.Error!.Message);
        Assert.Equal("unknown-tag", result.Error.Kind);
    }

    [Fact]
    public void Unknown_property_element_reports_unknown_property_error()
    {
        // Unknown *attributes* are silently ignored by Myra; the "doesnt have property" error
        // fires for unknown property-element syntax.
        var mml = "<Project><Panel><Panel.Nonexistent>x</Panel.Nonexistent></Panel></Project>";

        var result = ServiceRootedAtAssets().Validate(mml);

        Assert.False(result.Valid);
        Assert.Contains("doesnt have property", result.Error!.Message);
        Assert.Equal("unknown-property", result.Error.Kind);
    }

    [Fact]
    public void Bad_enum_value_reports_error()
    {
        var mml = "<Project><Panel><Label HorizontalAlignment=\"Centre\" /></Panel></Project>";

        var result = ServiceRootedAtAssets().Validate(mml);

        Assert.False(result.Valid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Malformed_xml_reports_line_and_column()
    {
        var mml = "<Project><Panel><Label></Panel></Project>"; // Label never closed

        var result = ServiceRootedAtAssets().Validate(mml);

        Assert.False(result.Valid);
        Assert.Equal("xml-syntax", result.Error!.Kind);
        Assert.NotNull(result.Error.Line);
    }

    [Fact]
    public void ValidateFile_rejects_path_outside_root()
    {
        var service = ServiceRootedAtAssets();

        Assert.Throws<ArgumentException>(() => service.ValidateFile("../../../../etc/passwd.xmmp"));
    }

    [Fact]
    public void Read_returns_raw_xml_and_widget_tree()
    {
        var result = ServiceRootedAtAssets().Read("checkButton.xmmp");

        Assert.Contains("CheckButton", result.Raw);
        Assert.True(result.Valid);
        Assert.Contains("CheckButton", result.WidgetTree);
    }
}
