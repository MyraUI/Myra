using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Myra.Mcp.Tools;

/// <summary>MCP tools for discovering the Myra widget vocabulary. Thin wrappers over <see cref="WidgetCatalog"/>.</summary>
[McpServerToolType]
public static class IntrospectionTools
{
    [McpServerTool(Name = "list_widget_types")]
    [Description("List every widget tag available in MML, with its base type and role (container, single-child container, or widget).")]
    public static IReadOnlyList<WidgetTypeInfo> ListWidgetTypes(WidgetCatalog catalog)
        => catalog.ListWidgetTypes();

    [McpServerTool(Name = "describe_widget")]
    [Description("Describe a widget: its settable properties (with enum options and defaults), the attached properties a child can carry (e.g. Grid.Row), and the available style names.")]
    public static WidgetDescription DescribeWidget(
        WidgetCatalog catalog,
        [Description("The widget tag name, e.g. Button.")] string name)
        => catalog.DescribeWidget(name)
           ?? throw new System.ArgumentException($"Unknown widget '{name}'. Call list_widget_types to see valid tags.");

    [McpServerTool(Name = "inspect_stylesheet")]
    [Description("Inspect a stylesheet and its atlas: the named styles referenceable via a widget's StyleName (grouped by widget type), the font ids, and the atlas region (drawable) names. Omit 'stylesheetPath' to inspect the built-in default skin.")]
    public static StylesheetInfo InspectStylesheet(
        StylesheetService service,
        [Description("Optional .xmms stylesheet path within the workspace root. Omit to inspect the built-in default skin.")] string? stylesheetPath = null)
        => service.Inspect(stylesheetPath);
}
