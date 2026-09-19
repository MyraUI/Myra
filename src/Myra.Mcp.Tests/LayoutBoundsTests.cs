using System;
using System.IO;
using System.Linq;
using Myra.Mcp;
using Xunit;

namespace Myra.Mcp.Tests;

/// <summary>
/// Unit tests for the headless layout-bounds pass (<see cref="LayoutService.LayoutBounds"/> /
/// <see cref="LayoutService.LayoutBoundsFile"/>). Expected coordinates follow Myra's documented
/// alignment + Left/Top offset semantics, not the implementation's own arithmetic.
/// </summary>
public class LayoutBoundsTests
{
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");

    private static LayoutService ServiceRootedAtAssets() => new(new MyraWorkspace(AssetsDir));

    private static WidgetBounds ById(LayoutBoundsResult result, string id)
        => result.Widgets.Single(w => w.Id == id);

    [Fact]
    public void Stretched_root_fills_the_viewport()
    {
        var result = ServiceRootedAtAssets().LayoutBoundsFile("layoutBounds.xmmp");

        Assert.True(result.Valid);
        Assert.Null(result.Error);
        Assert.Equal(1280, result.ViewportWidth);
        Assert.Equal(720, result.ViewportHeight);

        // The Stretch/Stretch root Panel (no Id) is the first widget and fills the viewport.
        var root = result.Widgets.First();
        Assert.Null(root.Id);
        Assert.Equal("Panel", root.Type);
        Assert.Equal((0, 0, 1280, 720), (root.X, root.Y, root.Width, root.Height));
        Assert.False(root.ZeroSize);
        Assert.False(root.Clipped);
    }

    [Fact]
    public void Fixed_child_reports_its_left_top_width_height()
    {
        var normal = ById(ServiceRootedAtAssets().LayoutBoundsFile("layoutBounds.xmmp"), "normal");

        Assert.Equal((100, 50, 200, 80), (normal.X, normal.Y, normal.Width, normal.Height));
        Assert.True(normal.Visible);
        Assert.False(normal.ZeroSize);
        Assert.False(normal.Clipped);
    }

    [Fact]
    public void Hidden_widget_and_its_descendants_are_not_visible_and_not_flagged()
    {
        // A hidden widget (and any child of a hidden container) is never arranged, so its zero/stale
        // bounds must NOT be reported as a collapsed-layout bug.
        var mml = "<Project><Panel HorizontalAlignment=\"Stretch\" VerticalAlignment=\"Stretch\">"
                + "<Panel Id=\"hidden\" Visible=\"false\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" />"
                + "<Panel Id=\"hiddenBox\" Visible=\"false\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" Width=\"200\" Height=\"80\">"
                + "<Panel Id=\"childOfHidden\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" />"
                + "</Panel></Panel></Project>";

        var result = ServiceRootedAtAssets().LayoutBounds(mml, assetRoot: AssetsDir);

        var hidden = ById(result, "hidden");
        Assert.False(hidden.Visible);
        Assert.False(hidden.ZeroSize);

        // childOfHidden is itself Visible=true, but its parent is hidden -> effectively not visible.
        var childOfHidden = ById(result, "childOfHidden");
        Assert.False(childOfHidden.Visible);
        Assert.False(childOfHidden.ZeroSize);
    }

    [Fact]
    public void Widget_past_the_viewport_edge_is_flagged_clipped()
    {
        var clipped = ById(ServiceRootedAtAssets().LayoutBoundsFile("layoutBounds.xmmp"), "clipped");

        // Left 1200 + Width 200 = 1400 > 1280.
        Assert.True(clipped.Clipped);
        Assert.False(clipped.ZeroSize);
    }

    [Fact]
    public void Widget_before_the_viewport_origin_is_flagged_clipped()
    {
        // A negative Left offset pushes the top-left to x < 0 - the other clipped branch.
        var mml = "<Project><Panel HorizontalAlignment=\"Stretch\" VerticalAlignment=\"Stretch\">"
                + "<Panel Id=\"before\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" Left=\"-50\" Top=\"10\" Width=\"100\" Height=\"40\" />"
                + "</Panel></Project>";

        var before = ById(ServiceRootedAtAssets().LayoutBounds(mml, assetRoot: AssetsDir), "before");

        Assert.Equal(-50, before.X);
        Assert.True(before.Clipped);
        Assert.False(before.ZeroSize);
    }

    [Fact]
    public void Collapsed_widget_is_flagged_zero_size()
    {
        var empty = ById(ServiceRootedAtAssets().LayoutBoundsFile("layoutBounds.xmmp"), "empty");

        Assert.True(empty.ZeroSize);
        Assert.Equal(0, empty.Width);
        Assert.Equal(0, empty.Height);
        Assert.False(empty.Clipped);
    }

    [Fact]
    public void Custom_viewport_is_reported_and_used_for_clipping()
    {
        var result = ServiceRootedAtAssets().LayoutBoundsFile("layoutBounds.xmmp", viewportWidth: 320, viewportHeight: 240);

        Assert.Equal(320, result.ViewportWidth);
        Assert.Equal(240, result.ViewportHeight);
        Assert.True(ById(result, "clipped").Clipped);
    }

    [Fact]
    public void Invalid_layout_reports_error_and_no_widgets()
    {
        var result = ServiceRootedAtAssets().LayoutBounds("<Project><Panel><Buton /></Panel></Project>");

        Assert.False(result.Valid);
        Assert.NotNull(result.Error);
        Assert.Empty(result.Widgets);
    }

    [Theory]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    [InlineData(-1, 720)]
    public void Nonpositive_viewport_is_rejected(int width, int height)
    {
        Assert.Throws<ArgumentException>(
            () => ServiceRootedAtAssets().LayoutBoundsFile("layoutBounds.xmmp", viewportWidth: width, viewportHeight: height));
    }

    [Fact]
    public void Rootless_project_is_valid_with_no_widgets()
    {
        // A layout that loads but has no root widget must not NRE the arrange pass.
        var result = ServiceRootedAtAssets().LayoutBounds("<Project />");

        Assert.True(result.Valid);
        Assert.Null(result.Error);
        Assert.Empty(result.Widgets);
    }

    [Fact]
    public void Content_overload_arranges_raw_mml()
    {
        var mml = File.ReadAllText(Path.Combine(AssetsDir, "layoutBounds.xmmp"));

        var result = ServiceRootedAtAssets().LayoutBounds(mml, assetRoot: AssetsDir);

        Assert.True(result.Valid);
        var root = result.Widgets.First();
        Assert.Equal((0, 0, 1280, 720), (root.X, root.Y, root.Width, root.Height));
    }
}
