using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Myra.Mcp.Tools;

/// <summary>MCP tools for the layout load/validate/save loop. Thin wrappers over <see cref="LayoutService"/>.</summary>
[McpServerToolType]
public static class LayoutTools
{
    [McpServerTool(Name = "validate_layout")]
    [Description("Validate a Myra .xmmp layout and return the first error (if any) plus the widget tree. Provide either 'path' (a .xmmp file inside the workspace root) or raw 'content'.")]
    public static ValidationResult ValidateLayout(
        LayoutService service,
        [Description("Path to a .xmmp file within the workspace root. Provide this or 'content'.")] string? path = null,
        [Description("Raw MML content. Provide this or 'path'.")] string? content = null,
        [Description("Optional directory (within the root) used to resolve referenced assets.")] string? assetRoot = null,
        [Description("Optional .xmms stylesheet path (within the root) to validate against instead of the default skin.")] string? stylesheetPath = null)
    {
        if (!string.IsNullOrEmpty(content))
        {
            return service.Validate(content, assetRoot, stylesheetPath);
        }

        if (!string.IsNullOrEmpty(path))
        {
            return service.ValidateFile(path, assetRoot, stylesheetPath);
        }

        throw new System.ArgumentException("Provide either 'path' or 'content'.");
    }

    [McpServerTool(Name = "read_layout")]
    [Description("Read a .xmmp layout file: returns its raw XML, validation outcome, and widget tree.")]
    public static ReadResult ReadLayout(
        LayoutService service,
        [Description("Path to a .xmmp file within the workspace root.")] string path)
        => service.Read(path);

    [McpServerTool(Name = "save_layout")]
    [Description("Validate MML and write it verbatim to a .xmmp path inside the workspace root. An invalid layout is refused unless 'force' is true.")]
    public static SaveResult SaveLayout(
        LayoutService service,
        [Description("Destination .xmmp path within the workspace root.")] string path,
        [Description("The MML content to write.")] string content,
        [Description("Write even when the layout is invalid.")] bool force = false,
        [Description("Optional .xmms stylesheet path (within the root) to validate against instead of the default skin.")] string? stylesheetPath = null)
        => service.Save(path, content, force, stylesheetPath);

    [McpServerTool(Name = "layout_bounds")]
    [Description("Arrange a .xmmp layout at a viewport (default 1280x720) and return every widget's rectangle { id, type, x, y, width, height } plus zeroSize and clipped (out-of-viewport) flags, so an agent can detect overlap, clipping, and collapsed widgets without a render. Provide either 'path' (a .xmmp file inside the workspace root) or raw 'content'.")]
    public static LayoutBoundsResult LayoutBounds(
        LayoutService service,
        [Description("Path to a .xmmp file within the workspace root. Provide this or 'content'.")] string? path = null,
        [Description("Raw MML content. Provide this or 'path'.")] string? content = null,
        [Description("Viewport width in pixels. Defaults to 1280.")] int viewportWidth = 1280,
        [Description("Viewport height in pixels. Defaults to 720.")] int viewportHeight = 720,
        [Description("Optional directory (within the root) used to resolve referenced assets.")] string? assetRoot = null,
        [Description("Optional .xmms stylesheet path (within the root) to arrange against instead of the default skin.")] string? stylesheetPath = null)
    {
        if (!string.IsNullOrEmpty(content))
        {
            return service.LayoutBounds(content, viewportWidth, viewportHeight, assetRoot, stylesheetPath);
        }

        if (!string.IsNullOrEmpty(path))
        {
            return service.LayoutBoundsFile(path, viewportWidth, viewportHeight, assetRoot, stylesheetPath);
        }

        throw new System.ArgumentException("Provide either 'path' or 'content'.");
    }
}
