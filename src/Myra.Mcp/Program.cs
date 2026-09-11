using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Myra.Mcp;

var root = ResolveWorkspaceRoot(args);
MyraEngine.Initialize();

var builder = Host.CreateApplicationBuilder(args);

// stdout is the MCP protocol channel, so every log must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(new MyraWorkspace(root));
builder.Services.AddSingleton<LayoutService>();
builder.Services.AddSingleton<WidgetCatalog>();
builder.Services.AddSingleton<StylesheetService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

static string ResolveWorkspaceRoot(string[] args)
{
    const string flag = "--root";
    const string prefix = "--root=";

    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == flag && i + 1 < args.Length)
        {
            return args[i + 1];
        }

        if (args[i].StartsWith(prefix, StringComparison.Ordinal))
        {
            return args[i][prefix.Length..];
        }
    }

    var env = Environment.GetEnvironmentVariable("MYRA_MCP_ROOT");
    return !string.IsNullOrWhiteSpace(env) ? env : Directory.GetCurrentDirectory();
}
