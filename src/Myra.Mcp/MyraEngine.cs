using Myra.Mcp.Platform;

namespace Myra.Mcp;

/// <summary>
/// Owns one-time headless initialization of the Myra environment and the single lock that
/// serializes every engine operation. Myra's <c>MyraEnvironment.Platform</c> and
/// <c>Stylesheet.Current</c> are mutable process-global statics and are not thread-safe, so all
/// load/validate/save/introspection work must run under <see cref="Gate"/>.
/// </summary>
internal static class MyraEngine
{
    /// <summary>Serializes all engine operations over Myra's process-global static state.</summary>
    public static readonly object Gate = new();

    private static bool _initialized;

    /// <summary>
    /// Installs the headless platform on <c>MyraEnvironment.Platform</c> exactly once. Idempotent
    /// and safe to call from every entry point.
    /// </summary>
    public static void Initialize()
    {
        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            MyraEnvironment.Platform = new HeadlessPlatform();
            _initialized = true;
        }
    }
}
