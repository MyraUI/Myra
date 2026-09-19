using System;
using System.Drawing;
using System.IO;
using System.Linq;
using AssetManagementBase;
using Myra.Graphics2D.UI;
using Myra.Mcp;
using Xunit;

namespace Myra.Mcp.Tests;

/// <summary>
/// Feasibility gate: proves Myra's Measure/Arrange layout pass runs headless (no Desktop, no
/// GraphicsDevice) and produces non-zero computed bounds, including for text widgets whose size
/// comes from FontStashSharp measurement under the stub texture manager. If this throws, the
/// layout_bounds tool cannot be built on this engine path.
/// </summary>
public class HeadlessLayoutTests
{
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");

    private static Widget ArrangeRoot(string fileName, int width, int height)
    {
        MyraEngine.Initialize();
        var assetManager = AssetManager.CreateFileAssetManager(AssetsDir);
        var xml = File.ReadAllText(Path.Combine(AssetsDir, fileName));
        var project = Project.LoadFromXml(xml, assetManager);
        var root = project.Root;
        Assert.NotNull(root);

        // The gate: Measure then Arrange must complete without a GraphicsDevice.
        root!.Measure(new Point(width, height));
        root.Arrange(new Rectangle(0, 0, width, height));
        return root;
    }

    [Theory]
    [InlineData("checkButton.xmmp")]               // default skin, text labels
    [InlineData("allControls.xmmp")]               // many widget types + atlas regions
    [InlineData("GridWithExternalResources.xmmp")] // external png + .fnt bitmap font
    public void Arranges_real_layout_headlessly(string fileName)
    {
        var root = ArrangeRoot(fileName, 1280, 720);

        Assert.True(root.Bounds.Width > 0, $"root width was {root.Bounds.Width}");
        Assert.True(root.Bounds.Height > 0, $"root height was {root.Bounds.Height}");
    }

    [Fact]
    public void Text_widget_measures_to_nonzero_bounds()
    {
        // checkButton.xmmp contains Labels; a non-zero Label rectangle proves FontStashSharp text
        // measurement ran under the headless texture manager (no GPU glyph atlas readback).
        var root = ArrangeRoot("checkButton.xmmp", 1280, 720);

        var label = root.GetChildren(recursive: true).FirstOrDefault(w => w is Label);
        Assert.NotNull(label);
        Assert.True(
            label!.Bounds.Width > 0 && label.Bounds.Height > 0,
            $"label bounds were {label.Bounds.Width}x{label.Bounds.Height}");
    }
}
