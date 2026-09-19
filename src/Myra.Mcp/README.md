# Myra.Mcp

A headless [Model Context Protocol](https://modelcontextprotocol.io) server that lets AI coding agents (Claude Code, Codex) work with Myra `.xmmp` UI layouts through Myra's own engine. Agents get a real load, validate, and save loop plus widget introspection, with no window and no GraphicsDevice.

It runs anywhere .NET runs (including macOS) because it references `Myra.PlatformAgnostic` with a stub rendering backend, so it does not need MonoGame, FNA, or a GPU.

## What it does

* Validates a layout and returns the exact Myra error (with a line number for XML syntax errors).
* Reads a layout back as a widget tree plus its raw XML.
* Saves a layout, refusing to write anything the engine rejects (unless you force it).
* Arranges a layout at a viewport (default 1280x720) and returns every widget's rectangle plus flags for collapsed (zero-size) and out-of-viewport (clipped) widgets, so an agent can catch layout problems without a render.
* Lists every widget tag and describes each widget's properties, enum options, defaults, attached properties (such as `Grid.Row`), and style names.
* Inspects a stylesheet and its atlas: the named styles per widget, the font ids, and the atlas region (drawable) names an agent can reference.

Layouts only (`.xmmp`) in this version. Stylesheets are used as read-only validation context.

## Building

```bash
dotnet build src/Myra.Mcp
```

The project targets `net8.0` with `RollForward=Major`, so it runs on a newer installed runtime (for example net10).

## Workspace root

Every path argument is confined to a workspace root, so the server is a Myra layout tool rather than a general filesystem bridge. Paths that resolve outside the root, or layout paths that do not end in `.xmmp`, are rejected before any file access.

The root is resolved in this order:

1. `--root <dir>` command line argument
2. `MYRA_MCP_ROOT` environment variable
3. the current working directory

Point the root at your game's UI directory.

## Tools

| Tool | Parameters | Returns |
|------|------------|---------|
| `validate_layout` | `content` or `path` (one of), optional `assetRoot`, optional `stylesheetPath` | `{ valid, error?, widgetTree? }` |
| `read_layout` | `path` | `{ raw, valid, error?, widgetTree? }` |
| `save_layout` | `path`, `content`, optional `force`, optional `stylesheetPath` | `{ saved, valid, error?, path }` |
| `layout_bounds` | `content` or `path` (one of), optional `viewportWidth` (1280), `viewportHeight` (720), `assetRoot`, `stylesheetPath` | `{ valid, error?, viewportWidth, viewportHeight, widgets }` |
| `list_widget_types` | (none) | `[ { name, baseType, role } ]` |
| `describe_widget` | `name` | `{ name, properties, attachedProperties, styleNames }` |
| `inspect_stylesheet` | optional `stylesheetPath` | `{ styles, fonts, atlasRegions }` |

`stylesheetPath` (on `validate_layout` and `save_layout`) points at a `.xmms` stylesheet inside the root to validate against instead of the default skin. It is read-only validation context, so widgets and styles the layout references resolve against your game's stylesheet rather than Myra's built-in one.

Result shapes:

* `error` (a diagnostic) is `{ message, kind, line?, column? }`, where `kind` is one of `unknown-tag`, `unknown-property`, `bad-value`, `xml-syntax`, `asset`, or `other`. Line and column are always present for `xml-syntax` errors and best-effort otherwise.
* `save_layout` writes your MML verbatim. An invalid layout is refused unless `force` is `true`. `valid` reports validation independently of whether the file was written.
* `layout_bounds` arranges the layout at the viewport and returns `{ valid, error?, viewportWidth, viewportHeight, widgets }`. `widgets` lists every widget in tree order (root first) as `{ id, type, x, y, width, height, visible, zeroSize, clipped }`, where `x`, `y`, `width`, `height` are absolute viewport pixels of the widget's margin-inclusive box, `id` is null when unset, and `visible` is `true` only when the widget and all its ancestors are visible (so it was actually arranged). `zeroSize` is `true` when a visible widget has a `0` dimension, and `clipped` is `true` when a visible widget's rectangle extends outside the viewport. Both flags stay `false` for a hidden widget (its bounds are not meaningful), so an intentionally hidden widget is never reported as a collapsed or clipped layout bug. An invalid layout returns `valid` `false` with `error` and no widgets. Clipping is viewport-only; a widget clipped by a scrolling or `ClipToBounds` parent while inside the viewport is not flagged.
* `property` is `{ name, type, isAttribute, default?, enumValues? }`. `attachedProperty` is `{ owner, name, syntax, type }` (for example `syntax` `Grid.Row`).
* `inspect_stylesheet` returns `{ styles, fonts, atlasRegions }`, where `styles` is a list of `{ widget, names }` (the named styles referenceable via a widget's `StyleName`), `fonts` the defined font ids, and `atlasRegions` the atlas region (drawable) names. Omit `stylesheetPath` to inspect the built-in default skin.

## Connecting an agent

Build first, then point the agent at the built assembly. The path below is the Debug build; use your own absolute paths.

### Claude Code

```bash
claude mcp add myra -- dotnet /ABS/Myra/src/Myra.Mcp/bin/Mcp/Debug/net8.0/Myra.Mcp.dll --root /ABS/your-game/UI
```

Or in `.mcp.json`:

```json
{
  "mcpServers": {
    "myra": {
      "command": "dotnet",
      "args": [
        "/ABS/Myra/src/Myra.Mcp/bin/Mcp/Debug/net8.0/Myra.Mcp.dll",
        "--root",
        "/ABS/your-game/UI"
      ]
    }
  }
}
```

### Codex

In `~/.codex/config.toml`:

```toml
[mcp_servers.myra]
command = "dotnet"
args = [
  "/ABS/Myra/src/Myra.Mcp/bin/Mcp/Debug/net8.0/Myra.Mcp.dll",
  "--root",
  "/ABS/your-game/UI",
]
```

The server speaks JSON-RPC on stdout and writes logs to stderr, as MCP over stdio requires.

## Not in scope (this version)

Image or preview rendering, attaching to a running app, editing `.xmms` stylesheets or font/atlas descriptors, general filesystem access, and multi-error reports (the engine stops at the first error, so each result carries one diagnostic).
