using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using NvgSharp;

namespace Myra.Samples;

/// <summary>
/// An <see cref="IBrush"/> that renders NanoVG (NvgSharp) vector graphics into a widget's
/// background rectangle. Vector content is drawn directly to the GPU device between two
/// flushes of Myra's render context, clipped to a viewport matching the destination bounds.
/// </summary>
public class NvgBrush : IBrush
{
	private readonly bool _edgeAntialiasing;
	private readonly bool _stencilStrokes;

	private NvgContext _nvgContext;

	/// <summary>
	/// Gets whether edge antialiasing is enabled for NvgSharp rendering.
	/// </summary>
	public bool EdgeAntialiasing => _edgeAntialiasing;

	/// <summary>
	/// Gets whether stencil-based strokes are used for NvgSharp rendering.
	/// </summary>
	public bool StencilStrokes => _stencilStrokes;

	/// <summary>
	/// Delegate invoked while the brush is drawing. Receives the active <see cref="NvgContext"/>
	/// and the pixel size of the destination rectangle. Assign this to perform custom drawing.
	/// </summary>
	public Action<NvgContext, Point> PaintHandler;

	/// <summary>
	/// Initializes a new instance of the <see cref="NvgBrush"/> class.
	/// An internal <see cref="NvgContext"/> is created lazily on the first draw call.
	/// </summary>
	/// <param name="edgeAntialiasing">Enables NvgSharp edge antialiasing.</param>
	/// <param name="stencilStrokes">Enables stencil-based stroke rendering (requires a depth-stencil buffer).</param>
	public NvgBrush(bool edgeAntialiasing = true, bool stencilStrokes = true)
	{
		_edgeAntialiasing = edgeAntialiasing;
		_stencilStrokes = stencilStrokes;
	}

	/// <inheritdoc />
	public void Draw(RenderContext context, Rectangle dest, Color color)
	{
		if (dest.Width <= 0 || dest.Height <= 0)
		{
			return;
		}

		if (PaintHandler == null)
		{
			return;
		}

		// Map the local destination rectangle to global (screen) coordinates so the
		// NvgSharp viewport can be positioned inside the widget's layout area.
		var global = context.ToGlobal(dest);

		// Temporarily flush Myra's batch so raw GPU rendering can be issued
		context.End();

		var device = MyraEnvironment.GraphicsDevice;
		var oldViewport = device.Viewport;

		try
		{
			device.Viewport = new Viewport(global.X, global.Y, global.Width, global.Height);

			if (_nvgContext == null)
			{
				_nvgContext = new NvgContext(device, _edgeAntialiasing, _stencilStrokes);
			}

			_nvgContext.ResetState();
			PaintHandler(_nvgContext, new Point(global.Width, global.Height));
			_nvgContext.Flush();
		}
		finally
		{
			device.Viewport = oldViewport;

			// Restart Myra's batch
			context.Begin();
		}
	}
}