using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Myra.Mcp.Tests;

/// <summary>
/// End-to-end: spawns the built server as a child process and drives it over stdio with a real MCP
/// client, exercising the initialize handshake, tools/list, and tools/call.
/// </summary>
public class McpServerIntegrationTests
{
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
    private static readonly string ServerDll = Path.Combine(AppContext.BaseDirectory, "Myra.Mcp.dll");

    private static Task<McpClient> ConnectAsync()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "myra",
            Command = "dotnet",
            Arguments = new[] { ServerDll, "--root", AssetsDir },
        });

        return McpClient.CreateAsync(transport);
    }

    [Fact]
    public async Task Server_exposes_exactly_the_seven_tools()
    {
        await using var client = await ConnectAsync();

        var names = (await client.ListToolsAsync()).Select(t => t.Name).OrderBy(n => n).ToArray();

        Assert.Equal(
            new[] { "describe_widget", "inspect_stylesheet", "layout_bounds", "list_widget_types", "read_layout", "save_layout", "validate_layout" },
            names);
    }

    [Fact]
    public async Task validate_layout_tool_reports_valid_and_invalid()
    {
        await using var client = await ConnectAsync();

        var good = await client.CallToolAsync(
            "validate_layout",
            new Dictionary<string, object?> { ["content"] = File.ReadAllText(Path.Combine(AssetsDir, "checkButton.xmmp")) });
        Assert.True(GetBool(good, "valid"));

        var bad = await client.CallToolAsync(
            "validate_layout",
            new Dictionary<string, object?> { ["content"] = "<Project><Panel><Buton /></Panel></Project>" });
        Assert.False(GetBool(bad, "valid"));
    }

    [Fact]
    public async Task layout_bounds_tool_returns_widget_bounds()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync(
            "layout_bounds",
            new Dictionary<string, object?> { ["content"] = File.ReadAllText(Path.Combine(AssetsDir, "checkButton.xmmp")) });

        Assert.True(GetBool(result, "valid"));
        Assert.True(GetArrayLength(result, "widgets") > 0);
    }

    private static bool GetBool(CallToolResult result, string property)
        => GetProperty(result, property).GetBoolean();

    private static int GetArrayLength(CallToolResult result, string property)
        => GetProperty(result, property).GetArrayLength();

    private static JsonElement GetProperty(CallToolResult result, string property)
    {
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        using var doc = JsonDocument.Parse(text);
        foreach (var member in doc.RootElement.EnumerateObject())
        {
            if (string.Equals(member.Name, property, StringComparison.OrdinalIgnoreCase))
            {
                return member.Value.Clone();
            }
        }

        throw new Xunit.Sdk.XunitException($"Property '{property}' not found in tool result: {text}");
    }
}
