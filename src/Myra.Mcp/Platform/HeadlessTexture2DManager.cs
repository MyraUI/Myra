using System.Drawing;
using FontStashSharp.Interfaces;

namespace Myra.Mcp.Platform;

/// <summary>
/// A headless <see cref="ITexture2DManager"/> that only tracks texture dimensions and never
/// allocates GPU memory. Loading and validating a layout creates and sizes textures (the skin
/// atlas, font glyph atlases) but never draws them, so ignoring the pixel data is safe.
/// </summary>
internal sealed class HeadlessTexture2DManager : ITexture2DManager
{
    private sealed class HeadlessTexture
    {
        public int Width { get; }
        public int Height { get; }

        public HeadlessTexture(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    public object CreateTexture(int width, int height) => new HeadlessTexture(width, height);

    public Point GetTextureSize(object texture)
    {
        var t = (HeadlessTexture)texture;
        return new Point(t.Width, t.Height);
    }

    public void SetTextureData(object texture, Rectangle bounds, byte[] data)
    {
        // No GPU backing: pixel data is intentionally discarded. It is never read back because
        // the headless server never renders.
    }
}
