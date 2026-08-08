using System;
using System.IO;
using Myra.Mcp;
using Xunit;

namespace Myra.Mcp.Tests;

public class LayoutSaveTests : IDisposable
{
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
    private readonly string _root;
    private readonly LayoutService _service;

    public LayoutSaveTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "myra-mcp-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _service = new LayoutService(new MyraWorkspace(_root));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    private static string ValidMml() => File.ReadAllText(Path.Combine(AssetsDir, "checkButton.xmmp"));

    private const string InvalidMml = "<Project><Panel><Buton /></Panel></Project>";

    [Fact]
    public void Valid_mml_is_written_and_reloads_clean()
    {
        var result = _service.Save("main.xmmp", ValidMml());

        Assert.True(result.Saved);
        Assert.True(result.Valid);
        Assert.True(File.Exists(result.Path));
        Assert.True(_service.Read("main.xmmp").Valid);
    }

    [Fact]
    public void Invalid_mml_without_force_is_refused_and_not_written()
    {
        var result = _service.Save("broken.xmmp", InvalidMml, force: false);

        Assert.False(result.Saved);
        Assert.False(result.Valid);
        Assert.NotNull(result.Error);
        Assert.False(File.Exists(Path.Combine(_root, "broken.xmmp")));
    }

    [Fact]
    public void Invalid_mml_with_force_is_written_verbatim()
    {
        var result = _service.Save("wip.xmmp", InvalidMml, force: true);

        Assert.True(result.Saved);
        Assert.False(result.Valid);
        Assert.Equal(InvalidMml, File.ReadAllText(result.Path));
    }

    [Fact]
    public void Save_rejects_path_outside_root()
    {
        Assert.Throws<ArgumentException>(() => _service.Save("../escape.xmmp", ValidMml()));
    }

    [Fact]
    public void Save_rejects_non_xmmp_path()
    {
        Assert.Throws<ArgumentException>(() => _service.Save("layout.txt", ValidMml()));
    }
}
