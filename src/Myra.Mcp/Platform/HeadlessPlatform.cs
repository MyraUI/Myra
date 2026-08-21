using System.Drawing;
using Myra.Graphics2D.UI;
using Myra.Platform;

namespace Myra.Mcp.Platform;

/// <summary>
/// A headless <see cref="IMyraPlatform"/> with no window, GPU, or input. Provides the renderer
/// (for its texture manager) and a fixed view size; all input members return empty state. This is
/// what <c>MyraEnvironment.Platform</c> is set to so layouts can load without a GraphicsDevice.
/// </summary>
internal sealed class HeadlessPlatform : IMyraPlatform
{
    private readonly HeadlessRenderer _renderer = new();

    public Point ViewSize { get; } = new Point(1920, 1080);

    public IMyraRenderer Renderer => _renderer;

    public MouseInfo GetMouseInfo() => new MouseInfo();

    public void SetKeysDown(bool[] keys)
    {
        // No keyboard input headlessly.
    }

    public void SetMouseCursorType(MouseCursorType mouseCursorType)
    {
        // No cursor headlessly.
    }

    public TouchCollection GetTouchState() => TouchCollection.Empty;
}
