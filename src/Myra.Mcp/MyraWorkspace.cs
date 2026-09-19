using System;
using System.IO;

namespace Myra.Mcp;

/// <summary>
/// Confines every path argument to a configured root directory, so the server is a Myra-layout
/// tool rather than a general filesystem bridge. Paths that resolve outside the root, or layout
/// paths without the <c>.xmmp</c> extension, are rejected before any filesystem access. Symlinks in
/// existing path components are resolved before the confinement check, so a symlinked directory
/// under the root that points elsewhere cannot escape it.
/// </summary>
public sealed class MyraWorkspace
{
    /// <summary>The absolute, symlink-resolved root directory that confines all path arguments.</summary>
    public string Root { get; }

    public MyraWorkspace(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("Workspace root must not be empty.", nameof(root));
        }

        Root = RealizePath(Path.GetFullPath(root));
    }

    /// <summary>
    /// Resolves <paramref name="path"/> (absolute, or relative to <see cref="Root"/>) to an absolute,
    /// symlink-resolved path and asserts it stays within the root. When <paramref name="mustBeXmmp"/>
    /// is true the path must end in <c>.xmmp</c>. Returns the resolved real path for the caller to use.
    /// </summary>
    /// <exception cref="ArgumentException">The path is empty, escapes the root, or (when required) is not a .xmmp file.</exception>
    public string Resolve(string path, bool mustBeXmmp)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        var combined = Path.IsPathRooted(path) ? path : Path.Combine(Root, path);

        string real;
        try
        {
            real = RealizePath(Path.GetFullPath(combined));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // A symlink loop or permission error means we cannot confirm the path is inside the
            // root, so reject it (fail closed) rather than falling back to the unresolved path.
            throw new ArgumentException($"Path '{path}' could not be resolved safely and is rejected.", nameof(path));
        }

        if (!IsWithinRoot(real))
        {
            throw new ArgumentException(
                $"Path '{path}' resolves to '{real}', which is outside the workspace root '{Root}'.",
                nameof(path));
        }

        if (mustBeXmmp && !real.EndsWith(".xmmp", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Layout path '{path}' must have the .xmmp extension.", nameof(path));
        }

        return real;
    }

    private bool IsWithinRoot(string fullPath)
    {
        // Case-sensitive comparison so that on a case-sensitive filesystem root "/work/ui" does not
        // accept "/work/UI/..." (a different directory). Fail-closed is the safe direction for a
        // boundary; on a case-insensitive filesystem this can over-reject a case-variant path.
        if (string.Equals(fullPath, Root, StringComparison.Ordinal))
        {
            return true;
        }

        var rootWithSep = Root.EndsWith(Path.DirectorySeparatorChar)
            ? Root
            : Root + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(rootWithSep, StringComparison.Ordinal);
    }

    // Resolves symlinks on each existing path component, so a symlinked directory that points
    // outside the root cannot slip past the confinement check. Non-existent trailing components
    // (a not-yet-created file) stay lexical. A resolution failure (symlink loop, permission error)
    // is NOT swallowed: it propagates so the caller rejects the path (fail closed). Full protection
    // against a symlink swapped mid-operation (a TOCTOU race) is deferred; see the plan's Deferred Ideas.
    private static string RealizePath(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return fullPath;
        }

        var current = root;
        var remainder = fullPath.Substring(root.Length);
        foreach (var part in remainder.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileSystemInfo? info =
                Directory.Exists(current) ? new DirectoryInfo(current) :
                File.Exists(current) ? new FileInfo(current) :
                null;
            var target = info?.ResolveLinkTarget(returnFinalTarget: true);
            if (target != null)
            {
                current = target.FullName;
            }
        }

        return current;
    }
}
