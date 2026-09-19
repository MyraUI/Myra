using System;
using System.IO;
using System.Linq;
using Myra.Mcp;
using Xunit;

namespace Myra.Mcp.Tests;

public class WidgetCatalogTests
{
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");

    [Fact]
    public void ListWidgetTypes_contains_common_widgets()
    {
        var names = new WidgetCatalog().ListWidgetTypes().Select(w => w.Name).ToHashSet();

        foreach (var expected in new[] { "Button", "Label", "Grid", "VerticalStackPanel", "ScrollViewer", "TextBox" })
        {
            Assert.Contains(expected, names);
        }
    }

    [Fact]
    public void DescribeWidget_Button_reports_alignment_enum_and_style_names()
    {
        var button = new WidgetCatalog().DescribeWidget("Button");

        Assert.NotNull(button);
        var alignment = button!.Properties.SingleOrDefault(p => p.Name == "HorizontalAlignment");
        Assert.NotNull(alignment);
        Assert.NotNull(alignment!.EnumValues);
        Assert.Contains("Center", alignment.EnumValues!);
        Assert.Contains("Stretch", alignment.EnumValues!);
        Assert.True(alignment.IsAttribute);

        // The default skin registers at least one Button style.
        Assert.NotEmpty(button.StyleNames);
    }

    [Fact]
    public void DescribeWidget_reports_grid_attached_properties()
    {
        var label = new WidgetCatalog().DescribeWidget("Label");

        Assert.NotNull(label);
        var syntaxes = label!.AttachedProperties.Select(a => a.Syntax).ToHashSet();
        Assert.Contains("Grid.Row", syntaxes);
        Assert.Contains("Grid.Column", syntaxes);
    }

    [Fact]
    public void DescribeWidget_unknown_name_returns_null()
    {
        Assert.Null(new WidgetCatalog().DescribeWidget("NotAWidget"));
    }

    [Fact]
    public void Catalog_only_advertises_loader_accepted_vocabulary()
    {
        var catalog = new WidgetCatalog();
        var service = new LayoutService(new MyraWorkspace(AssetsDir));

        // An enum attribute the catalog reports for Button must load as an attribute.
        var alignment = catalog.DescribeWidget("Button")!.Properties.Single(p => p.Name == "HorizontalAlignment");
        var enumValue = alignment.EnumValues!.First();
        var enumMml = $"<Project><Panel><Button HorizontalAlignment=\"{enumValue}\" /></Panel></Project>";
        Assert.True(service.Validate(enumMml).Valid, $"loader rejected reported enum value {enumValue}");

        // An attached property the catalog reports (Grid.Row) must load on a Grid child.
        var gridRow = catalog.DescribeWidget("Label")!.AttachedProperties.Single(a => a.Syntax == "Grid.Row");
        var attachedMml = $"<Project><Grid><Label {gridRow.Syntax}=\"0\" /></Grid></Project>";
        Assert.True(service.Validate(attachedMml).Valid, "loader rejected reported attached property Grid.Row");
    }
}
