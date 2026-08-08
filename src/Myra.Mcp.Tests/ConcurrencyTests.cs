using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Myra.Mcp;
using Xunit;

namespace Myra.Mcp.Tests;

public class ConcurrencyTests
{
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");

    [Fact]
    public async Task Overlapping_engine_operations_stay_correct()
    {
        var service = new LayoutService(new MyraWorkspace(AssetsDir));
        var catalog = new WidgetCatalog();
        var validMml = File.ReadAllText(Path.Combine(AssetsDir, "checkButton.xmmp"));
        const string invalidMml = "<Project><Panel><Buton /></Panel></Project>";

        // Fire many overlapping operations against Myra's process-global state; the shared
        // MyraEngine.Gate must keep each call's result correct and exception-free.
        var tasks = new List<Task>();
        for (var i = 0; i < 32; i++)
        {
            tasks.Add(Task.Run(() => Assert.True(service.Validate(validMml).Valid)));
            tasks.Add(Task.Run(() => Assert.False(service.Validate(invalidMml).Valid)));
            tasks.Add(Task.Run(() => Assert.NotNull(catalog.DescribeWidget("Button"))));
            tasks.Add(Task.Run(() => Assert.Contains("Grid", catalog.ListWidgetTypes().Select(w => w.Name))));
        }

        await Task.WhenAll(tasks);
    }
}
