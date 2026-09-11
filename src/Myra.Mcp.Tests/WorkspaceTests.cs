using System;
using System.IO;
using Myra.Mcp;
using Xunit;

namespace Myra.Mcp.Tests;

public class WorkspaceTests
{
    private static MyraWorkspace RootedHere() => new(AppContext.BaseDirectory);

    [Fact]
    public void Resolve_accepts_in_root_xmmp_path()
    {
        var ws = RootedHere();

        var full = ws.Resolve("layouts/main.xmmp", mustBeXmmp: true);

        Assert.StartsWith(ws.Root, full);
        Assert.EndsWith("main.xmmp", full);
    }

    [Fact]
    public void Resolve_rejects_path_escaping_the_root()
    {
        var ws = RootedHere();

        Assert.Throws<ArgumentException>(() => ws.Resolve("../../../../etc/passwd", mustBeXmmp: false));
    }

    [Fact]
    public void Resolve_rejects_non_xmmp_layout_path()
    {
        var ws = RootedHere();

        Assert.Throws<ArgumentException>(() => ws.Resolve("layout.txt", mustBeXmmp: true));
    }

    [Fact]
    public void Resolve_allows_non_xmmp_when_not_required()
    {
        var ws = RootedHere();

        var full = ws.Resolve("assets", mustBeXmmp: false);

        Assert.StartsWith(ws.Root, full);
    }

    [Fact]
    public void Resolve_rejects_symlink_that_escapes_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "myra-ws-root-" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "myra-ws-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        try
        {
            // A symlink inside the root pointing outside it must not open an escape hatch: a purely
            // lexical check would accept "escape/evil.xmmp"; realpath resolution rejects it.
            Directory.CreateSymbolicLink(Path.Combine(root, "escape"), outside);
            var ws = new MyraWorkspace(root);

            Assert.Throws<ArgumentException>(() => ws.Resolve("escape/evil.xmmp", mustBeXmmp: true));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best-effort */ }
            try { Directory.Delete(outside, recursive: true); } catch { /* best-effort */ }
        }
    }
}
