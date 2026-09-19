using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using StbImageSharp;
using Color = Microsoft.Xna.Framework.Color;

namespace Myra.Samples;

/// <summary>
/// An <see cref="IBrush"/> that renders an animated GIF. Frames are decoded up-front with
/// StbImageSharp into <see cref="Texture2D"/> frames and advanced automatically over time.
/// </summary>
public class AnimatedGifBrush : IBrush, IDisposable
{
	private sealed class Frame
	{
		public Texture2D Texture;
		public float DelayInSeconds;
	}

	private readonly List<Frame> _frames = new List<Frame>();
	private readonly float _defaultDelayInSeconds;

	private int _currentFrame;
	private double _accumulator;
	private DateTime _lastTime = DateTime.UtcNow;

	/// <summary>
	/// Gets the width of the GIF in pixels (from the first frame).
	/// </summary>
	public int Width => _frames.Count > 0 ? _frames[0].Texture.Width : 0;

	/// <summary>
	/// Gets the height of the GIF in pixels (from the first frame).
	/// </summary>
	public int Height => _frames.Count > 0 ? _frames[0].Texture.Height : 0;

	/// <summary>
	/// Gets the number of decoded animation frames.
	/// </summary>
	public int FrameCount => _frames.Count;

	/// <summary>
	/// Initializes a new instance of the <see cref="AnimatedGifBrush"/> class.
	/// </summary>
	/// <param name="device">The graphics device used to create the frame textures.</param>
	/// <param name="stream">A stream containing the animated GIF data.</param>
	/// <param name="defaultDelayInSeconds">
	/// The frame delay used when the GIF does not specify one (defaults to 0.1).
	/// </param>
	public AnimatedGifBrush(GraphicsDevice device, Stream stream, float defaultDelayInSeconds = 0.1f)
	{
		if (stream == null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		_defaultDelayInSeconds = defaultDelayInSeconds;

		foreach (var frame in ImageResult.AnimatedGifFramesFromStream(stream, ColorComponents.RedGreenBlueAlpha))
		{
			var texture = new Texture2D(device, frame.Width, frame.Height);
			texture.SetData(frame.Data);

			_frames.Add(new Frame
			{
				Texture = texture,
				DelayInSeconds = frame.DelayInMs > 0 ? frame.DelayInMs / 1000f : defaultDelayInSeconds
			});
		}

		_lastTime = DateTime.UtcNow;
	}

	/// <summary>
	/// Advances the animation based on the elapsed wall-clock time since the last draw call.
	/// </summary>
	private void Advance()
	{
		if (_frames.Count < 2)
		{
			return;
		}

		var now = DateTime.UtcNow;
		var elapsed = (now - _lastTime).TotalSeconds;
		_lastTime = now;

		if (elapsed <= 0)
		{
			return;
		}

		_accumulator += elapsed;
		while (_accumulator >= _frames[_currentFrame].DelayInSeconds)
		{
			_accumulator -= _frames[_currentFrame].DelayInSeconds;
			_currentFrame = (_currentFrame + 1) % _frames.Count;
		}
	}

	/// <inheritdoc />
	public void Draw(RenderContext context, Rectangle dest, Color color)
	{
		if (_frames.Count == 0 || dest.Width <= 0 || dest.Height <= 0)
		{
			return;
		}

		Advance();

		context.Draw(_frames[_currentFrame].Texture, dest, null, color);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		foreach (var frame in _frames)
		{
			frame.Texture?.Dispose();
		}

		_frames.Clear();
	}
}