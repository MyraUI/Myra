using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AssetManagementBase;
using Myra.Attributes;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;

namespace Myra.Mcp;

/// <summary>
/// Loads and validates <c>.xmmp</c> layouts through Myra's engine, and renders a text widget tree.
/// The loader throws on the first error, so a result carries at most one <see cref="Diagnostic"/>.
/// All paths are confined through <see cref="MyraWorkspace"/> and all work runs under
/// <see cref="MyraEngine.Gate"/>.
/// </summary>
public sealed class LayoutService
{
    private readonly MyraWorkspace _workspace;

    public LayoutService(MyraWorkspace workspace)
    {
        _workspace = workspace;
    }

    /// <summary>Validates raw MML. <paramref name="assetRoot"/> (confined) resolves external asset references; defaults to the workspace root. <paramref name="stylesheetPath"/> (confined) overrides the stylesheet used for validation.</summary>
    public ValidationResult Validate(string mml, string? assetRoot = null, string? stylesheetPath = null)
    {
        var root = assetRoot != null ? _workspace.Resolve(assetRoot, mustBeXmmp: false) : _workspace.Root;
        var stylesheet = stylesheetPath != null ? _workspace.Resolve(stylesheetPath, mustBeXmmp: false) : null;
        return ValidateCore(mml, root, stylesheet);
    }

    /// <summary>Reads a confined <c>.xmmp</c> file and validates it. Asset references resolve against the file's directory unless <paramref name="assetRoot"/> is given; <paramref name="stylesheetPath"/> (confined) overrides the stylesheet.</summary>
    public ValidationResult ValidateFile(string path, string? assetRoot = null, string? stylesheetPath = null)
    {
        var full = _workspace.Resolve(path, mustBeXmmp: true);
        var mml = File.ReadAllText(full);
        var root = assetRoot != null ? _workspace.Resolve(assetRoot, mustBeXmmp: false) : Path.GetDirectoryName(full)!;
        var stylesheet = stylesheetPath != null ? _workspace.Resolve(stylesheetPath, mustBeXmmp: false) : null;
        return ValidateCore(mml, root, stylesheet);
    }

    /// <summary>Reads a confined <c>.xmmp</c> file, returning its raw XML together with the validation outcome and widget tree.</summary>
    public ReadResult Read(string path)
    {
        var full = _workspace.Resolve(path, mustBeXmmp: true);
        var mml = File.ReadAllText(full);
        var v = ValidateCore(mml, Path.GetDirectoryName(full)!, stylesheetFullPath: null);
        return new ReadResult(mml, v.Valid, v.Error, v.WidgetTree);
    }

    /// <summary>
    /// Validates <paramref name="mml"/> and writes it verbatim to a confined <c>.xmmp</c> path. An
    /// invalid layout is refused (nothing written) unless <paramref name="force"/> is set. The write
    /// is atomic (temp file then rename in the same directory) with a confinement re-check before the rename.
    /// </summary>
    public SaveResult Save(string path, string mml, bool force = false, string? stylesheetPath = null)
    {
        var full = _workspace.Resolve(path, mustBeXmmp: true);
        var directory = Path.GetDirectoryName(full)!;
        var assetRoot = Directory.Exists(directory) ? directory : _workspace.Root;
        var stylesheet = stylesheetPath != null ? _workspace.Resolve(stylesheetPath, mustBeXmmp: false) : null;

        var validation = ValidateCore(mml, assetRoot, stylesheet);
        if (!validation.Valid && !force)
        {
            return new SaveResult(false, false, validation.Error, full);
        }

        // `full` is already confined and symlink-resolved by Resolve above. Write to a temp file in
        // the same directory and rename, so the target file is never left half-written.
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, Path.GetFileName(full) + ".tmp-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(temp, mml);

            // Fresh confinement re-check immediately before the rename: re-resolving the original
            // path re-runs symlink resolution with new syscalls, so a symlink swapped in during
            // validation (which does its own disk I/O) is caught before we write to the target.
            if (!string.Equals(_workspace.Resolve(path, mustBeXmmp: true), full, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Path '{path}' changed target during save; refusing to write.", nameof(path));
            }

            File.Move(temp, full, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }

        return new SaveResult(true, validation.Valid, validation.Error, full);
    }

    /// <summary>
    /// Arranges raw <paramref name="mml"/> at a <paramref name="viewportWidth"/> x <paramref name="viewportHeight"/>
    /// viewport and returns every widget's arranged rectangle plus zero-size / out-of-viewport (clipped) flags.
    /// Asset references resolve against <paramref name="assetRoot"/> (confined) or the workspace root;
    /// <paramref name="stylesheetPath"/> (confined) overrides the stylesheet used for measurement.
    /// </summary>
    public LayoutBoundsResult LayoutBounds(string mml, int viewportWidth = 1280, int viewportHeight = 720, string? assetRoot = null, string? stylesheetPath = null)
    {
        var root = assetRoot != null ? _workspace.Resolve(assetRoot, mustBeXmmp: false) : _workspace.Root;
        var stylesheet = stylesheetPath != null ? _workspace.Resolve(stylesheetPath, mustBeXmmp: false) : null;
        return LayoutBoundsCore(mml, root, stylesheet, viewportWidth, viewportHeight);
    }

    /// <summary>
    /// Reads a confined <c>.xmmp</c> file and arranges it at the given viewport (see <see cref="LayoutBounds"/>).
    /// Asset references resolve against the file's directory unless <paramref name="assetRoot"/> is given.
    /// </summary>
    public LayoutBoundsResult LayoutBoundsFile(string path, int viewportWidth = 1280, int viewportHeight = 720, string? assetRoot = null, string? stylesheetPath = null)
    {
        var full = _workspace.Resolve(path, mustBeXmmp: true);
        var mml = File.ReadAllText(full);
        var root = assetRoot != null ? _workspace.Resolve(assetRoot, mustBeXmmp: false) : Path.GetDirectoryName(full)!;
        var stylesheet = stylesheetPath != null ? _workspace.Resolve(stylesheetPath, mustBeXmmp: false) : null;
        return LayoutBoundsCore(mml, root, stylesheet, viewportWidth, viewportHeight);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup is best-effort.
        }
    }

    private static ValidationResult ValidateCore(string mml, string assetRoot, string? stylesheetFullPath)
    {
        lock (MyraEngine.Gate)
        {
            MyraEngine.Initialize();
            try
            {
                var project = LoadProject(mml, assetRoot, stylesheetFullPath);
                return new ValidationResult(true, null, RenderTree(project));
            }
            catch (XmlException xe)
            {
                return new ValidationResult(false, new Diagnostic(xe.Message, "xml-syntax", NullIfZero(xe.LineNumber), NullIfZero(xe.LinePosition)), null);
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, Classify(ex, mml), null);
            }
        }
    }

    // Shared load path for validation and layout: creates the confined asset manager, loads an
    // optional custom stylesheet, and parses. Throws on the first loader error (the caller catches
    // and classifies). Must run under MyraEngine.Gate.
    private static Project LoadProject(string mml, string assetRoot, string? stylesheetFullPath)
    {
        var assetManager = AssetManager.CreateFileAssetManager(assetRoot);
        Stylesheet? customStylesheet = null;
        if (stylesheetFullPath != null)
        {
            var stylesheetManager = AssetManager.CreateFileAssetManager(Path.GetDirectoryName(stylesheetFullPath)!);
            customStylesheet = stylesheetManager.LoadStylesheet(Path.GetFileName(stylesheetFullPath));
        }

        return Project.LoadFromXml(mml, assetManager, customStylesheet);
    }

    // Loads, runs Myra's Measure/Arrange pass on the root at the viewport, and reads back each
    // widget's rectangle. A load failure returns valid=false with a diagnostic and no widgets,
    // mirroring ValidateCore. Runs under MyraEngine.Gate (mutates process-global engine state).
    private static LayoutBoundsResult LayoutBoundsCore(string mml, string assetRoot, string? stylesheetFullPath, int viewportWidth, int viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            throw new ArgumentException($"Viewport must be positive; got {viewportWidth}x{viewportHeight}.");
        }

        lock (MyraEngine.Gate)
        {
            MyraEngine.Initialize();
            try
            {
                var project = LoadProject(mml, assetRoot, stylesheetFullPath);
                var root = project.Root;
                if (root == null)
                {
                    // Valid but rootless layout: nothing to arrange, no widgets to report.
                    return new LayoutBoundsResult(true, null, viewportWidth, viewportHeight, Array.Empty<WidgetBounds>());
                }

                root.Measure(new Point(viewportWidth, viewportHeight));
                root.Arrange(new Rectangle(0, 0, viewportWidth, viewportHeight));
                return new LayoutBoundsResult(true, null, viewportWidth, viewportHeight, Collect(root, viewportWidth, viewportHeight));
            }
            catch (XmlException xe)
            {
                return new LayoutBoundsResult(false, new Diagnostic(xe.Message, "xml-syntax", NullIfZero(xe.LineNumber), NullIfZero(xe.LinePosition)), viewportWidth, viewportHeight, Array.Empty<WidgetBounds>());
            }
            catch (Exception ex)
            {
                return new LayoutBoundsResult(false, Classify(ex, mml), viewportWidth, viewportHeight, Array.Empty<WidgetBounds>());
            }
        }
    }

    // Reports the root followed by every descendant in tree order, each as its absolute viewport
    // rectangle. Traversal tracks EFFECTIVE visibility (a widget is arranged only if it and all its
    // ancestors are visible; Myra's containers skip invisible children during Arrange, so a hidden
    // widget or a child of a hidden container keeps zero/stale bounds). The zeroSize/clipped
    // layout-problem flags fire only for effectively-visible widgets, so an intentionally hidden
    // widget is never reported as a collapsed or clipped layout bug.
    private static WidgetBounds[] Collect(Widget root, int viewportWidth, int viewportHeight)
    {
        var result = new List<WidgetBounds>();
        Walk(root, ancestorsVisible: true, result, viewportWidth, viewportHeight);
        return result.ToArray();
    }

    private static void Walk(Widget widget, bool ancestorsVisible, List<WidgetBounds> into, int viewportWidth, int viewportHeight)
    {
        var visible = ancestorsVisible && widget.Visible;
        into.Add(ToWidgetBounds(widget, visible, viewportWidth, viewportHeight));
        foreach (var child in widget.GetChildren())
        {
            Walk(child, visible, into, viewportWidth, viewportHeight);
        }
    }

    // SHORTCUT: rotation/scale are not decomposed into an axis-aligned box - Width/Height are the
    // un-rotated Bounds size at the transformed origin. Upgrade if rotated layouts need a true AABB:
    // compute it from the four transformed corners via ToGlobal.
    private static WidgetBounds ToWidgetBounds(Widget widget, bool visible, int viewportWidth, int viewportHeight)
    {
        var origin = widget.ToGlobal(Point.Empty);
        var box = widget.Bounds;
        var x = origin.X;
        var y = origin.Y;
        var width = box.Width;
        var height = box.Height;

        var zeroSize = visible && (width == 0 || height == 0);
        var clipped = visible && (x < 0 || y < 0 || x + width > viewportWidth || y + height > viewportHeight);

        return new WidgetBounds(
            string.IsNullOrEmpty(widget.Id) ? null : widget.Id,
            widget.GetType().Name,
            x, y, width, height, visible, zeroSize, clipped);
    }

    private static int? NullIfZero(int value) => value > 0 ? value : null;

    private static Diagnostic Classify(Exception ex, string mml)
    {
        // `kind` keys off Myra's literal loader messages because Myra throws plain System.Exception
        // with no error codes. If a future Myra upgrade rewords these, matches fall through to
        // "other" (the message is still surfaced verbatim); the classification tests below catch it.
        var msg = ex.Message;
        var kind =
            msg.Contains("Could not resolve tag") ? "unknown-tag" :
            msg.Contains("doesnt have property") || msg.Contains("attached property") ? "unknown-property" :
            ex is FileNotFoundException or DirectoryNotFoundException ? "asset" :
            ex is FormatException or ArgumentException or InvalidCastException or OverflowException ? "bad-value" :
            "other";

        var (line, column) = BestEffortPosition(mml, msg);
        return new Diagnostic(msg, kind, line, column);
    }

    // Semantic errors carry no engine position (the Project is never returned on a throw), so
    // recover an approximate line by locating the token named in the message in the source XML.
    private static (int? line, int? column) BestEffortPosition(string mml, string message)
    {
        string? token = null;
        var quoted = Regex.Match(message, "'([^']+)'");
        if (quoted.Success)
        {
            token = quoted.Groups[1].Value;
        }
        else
        {
            var property = Regex.Match(message, @"property\s+([A-Za-z_][A-Za-z0-9_]*)");
            if (property.Success)
            {
                token = property.Groups[1].Value;
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            return (null, null);
        }

        try
        {
            var doc = XDocument.Parse(mml, LoadOptions.SetLineInfo);
            foreach (var el in doc.Descendants())
            {
                if (el.Name.LocalName == token && el is IXmlLineInfo eli && eli.HasLineInfo())
                {
                    return (eli.LineNumber, eli.LinePosition);
                }

                var attr = el.Attributes().FirstOrDefault(a => a.Name.LocalName == token);
                if (attr is IXmlLineInfo ali && ali.HasLineInfo())
                {
                    return (ali.LineNumber, ali.LinePosition);
                }
            }
        }
        catch
        {
            // Best-effort only; a malformed re-parse just yields no position.
        }

        return (null, null);
    }

    private static string RenderTree(Project project)
    {
        var sb = new StringBuilder();
        if (project.Root != null)
        {
            Walk(project.Root, 0, sb);
        }

        return sb.ToString().TrimEnd();
    }

    // Walks the widget hierarchy via the [Content] property, which marks the child holder on every
    // container shape (Container.Widgets, ContentControl.Content, item lists). See MyraPad MainForm.cs.
    private static void Walk(object node, int depth, StringBuilder sb)
    {
        var type = node.GetType();
        sb.Append(' ', depth * 2).Append(type.Name);

        if (type.GetProperty("Id")?.GetValue(node) is string id && !string.IsNullOrEmpty(id))
        {
            sb.Append(" #").Append(id);
        }

        AppendStringProperty(sb, node, type, "Text");
        sb.AppendLine();

        var contentProperty = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.GetCustomAttribute<ContentAttribute>() != null);
        if (contentProperty == null)
        {
            return;
        }

        object? content;
        try
        {
            content = contentProperty.GetValue(node);
        }
        catch
        {
            return;
        }

        switch (content)
        {
            case null:
            case string:
                return;
            case IEnumerable children:
                foreach (var child in children)
                {
                    if (child != null)
                    {
                        Walk(child, depth + 1, sb);
                    }
                }

                break;
            default:
                Walk(content, depth + 1, sb);
                break;
        }
    }

    private static void AppendStringProperty(StringBuilder sb, object node, Type type, string name)
    {
        var property = type.GetProperty(name);
        if (property?.PropertyType == typeof(string) && property.GetValue(node) is string value && !string.IsNullOrEmpty(value))
        {
            sb.Append(' ').Append(name).Append("=\"").Append(value).Append('"');
        }
    }
}
