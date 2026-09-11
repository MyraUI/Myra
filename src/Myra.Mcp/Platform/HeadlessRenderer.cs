using System.Drawing;
using System.Numerics;
using FontStashSharp;
using FontStashSharp.Interfaces;
using Myra.Graphics2D;
using Myra.Platform;

namespace Myra.Mcp.Platform;

/// <summary>
/// A headless <see cref="IMyraRenderer"/>. Only <see cref="TextureManager"/> is exercised while a
/// layout loads; the drawing members are reached solely through <c>Desktop.Render()</c>, which the
/// server never calls, so they throw.
/// </summary>
internal sealed class HeadlessRenderer : IMyraRenderer
{
    private readonly HeadlessTexture2DManager _textureManager = new();

    public ITexture2DManager TextureManager => _textureManager;

    public RendererType RendererType => RendererType.Sprite;

    public Rectangle Scissor { get; set; }

    public void Begin(TextureFiltering textureFiltering)
    {
        // No-op: nothing is drawn headlessly.
    }

    public void End()
    {
        // No-op.
    }

    public void DrawSprite(object texture, Vector2 pos, Rectangle? src, FSColor color, float rotation, Vector2 scale, float depth)
        => throw new System.NotSupportedException("Myra.Mcp runs headless; drawing is not supported.");

    public void DrawQuad(object texture, ref VertexPositionColorTexture topLeft, ref VertexPositionColorTexture topRight, ref VertexPositionColorTexture bottomLeft, ref VertexPositionColorTexture bottomRight)
        => throw new System.NotSupportedException("Myra.Mcp runs headless; drawing is not supported.");
}
