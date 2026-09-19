using System;
using System.IO;
using System.Xml.Linq;
using AssetManagementBase;
using Myra.Graphics2D.UI;
using Myra.Mcp;
using Xunit;

namespace Myra.Mcp.Tests;

/// <summary>
/// The feasibility gate: proves a real .xmmp loads through Myra's engine with the headless
/// platform (no GraphicsDevice) and round-trips back to XML.
/// </summary>
public class HeadlessLoadTests
{
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");

    private static Project Load(string fileName)
    {
        MyraEngine.Initialize();
        var assetManager = AssetManager.CreateFileAssetManager(AssetsDir);
        var xml = File.ReadAllText(Path.Combine(AssetsDir, fileName));
        return Project.LoadFromXml(xml, assetManager);
    }

    [Theory]
    [InlineData("checkButton.xmmp")]              // default skin, no external assets
    [InlineData("allControls.xmmp")]              // default skin, many widgets + atlas regions
    [InlineData("GridWithExternalResources.xmmp")] // external png + font
    public void Loads_real_layout_headlessly(string fileName)
    {
        var project = Load(fileName);

        Assert.NotNull(project);
        Assert.NotNull(project.Root);
    }

    [Fact]
    public void Loaded_project_round_trips_to_parseable_xml()
    {
        var project = Load("checkButton.xmmp");

        var xml = project.ToXml();

        // Re-parsing proves ToXml produced well-formed XML.
        var doc = XDocument.Parse(xml);
        Assert.Equal("Project", doc.Root!.Name.LocalName);
    }
}
