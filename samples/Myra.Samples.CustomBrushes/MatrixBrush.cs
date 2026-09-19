using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Myra;
using Myra.Graphics2D;
using Color = Microsoft.Xna.Framework.Color;

namespace Myra.Samples;

/// <summary>
/// An <see cref="IBrush"/> that renders Matrix-style "digital rain": bright green digits
/// continuously falling through the destination rectangle. Each glyph is drawn with
/// <see cref="RenderContext.DrawString"/> using <see cref="DefaultAssets.DebugFont"/>.
/// </summary>
public class MatrixBrush : IBrush
{
	private const string Digits = "0123456789";

	private static readonly Color BodyColor = new Color(0, 255, 96);
	private static readonly Color HeadColor = new Color(205, 255, 205);

	private sealed class Stream
	{
		public char[] Chars = Array.Empty<char>();
		public float Head;   // position of the leading glyph in cells
		public float Speed;  // glyphs per second
		public int Length;   // number of glyphs in the stream
	}

	private readonly Random _random;
	private readonly List<Stream> _streams = new List<Stream>();

	private DateTime _lastTime = DateTime.UtcNow;
	private int _columns = -1;

	/// <summary>
	/// Gets or sets the size of a rain cell (and therefore the glyph size) in pixels.
	/// </summary>
	public float CellSize { get; set; } = 22f;

	/// <summary>
	/// Gets or sets the seed used by the random generator to keep the rain reproducible.
	/// </summary>
	public int Seed { get; set; } = 12345;

	/// <summary>
	/// Initializes a new instance of the <see cref="MatrixBrush"/> class.
	/// </summary>
	public MatrixBrush()
	{
		_random = new Random(Seed);
	}

	/// <inheritdoc />
	public void Draw(RenderContext context, Rectangle dest, Color color)
	{
		if (dest.Width <= 0 || dest.Height <= 0)
		{
			return;
		}

		// Subtle dark backdrop so the rain always reads on any surface
		DefaultAssets.WhiteRegion.Draw(context, dest, new Color(2, 8, 4, 235));

		var font = DefaultAssets.DebugFont;
		var columns = Math.Max(1, (int)(dest.Width / CellSize));
		var heightInCells = dest.Height / CellSize;

		EnsureStreams(columns);
		Advance((float)(DateTime.UtcNow - _lastTime).TotalSeconds, heightInCells);
		_lastTime = DateTime.UtcNow;

		// Fit the glyphs into a cell
		var scale = CellSize / font.LineHeight;
		var glyphSize = font.MeasureString("0") * scale;
		var offsetX = (CellSize - glyphSize.X) * 0.5f;
		var offsetY = (CellSize - glyphSize.Y) * 0.5f;

		var tint = color.A / 255f;

		for (var c = 0; c < _streams.Count; c++)
		{
			var stream = _streams[c];
			var baseX = dest.X + c * CellSize;

			for (var i = 0; i < stream.Length; i++)
			{
				var row = stream.Head - i;
				if (row + stream.Length - 1 < 0)
				{
					continue;
				}

				var alpha = i == 0 ? 255 : Math.Max(0, 255 - (int)(255f * i / stream.Length));
				var glyphColor = i == 0 ? HeadColor : BodyColor;

				context.DrawString(font, stream.Chars[i].ToString(),
					new Vector2(baseX + offsetX, dest.Y + row * CellSize + offsetY),
					WithAlpha(glyphColor, (byte)(alpha * tint)), new Vector2(scale));
			}
		}
	}

	/// <summary>
	/// Recreates the streams when the number of columns changes (e.g. the destination width or
	/// the cell size was modified).
	/// </summary>
	private void EnsureStreams(int columns)
	{
		if (columns == _columns)
		{
			return;
		}

		_columns = columns;
		_streams.Clear();

		for (var i = 0; i < _columns; i++)
		{
			var stream = new Stream();
			StartStream(stream);
			_streams.Add(stream);
		}
	}

	/// <summary>
	/// Advances the falling glyphs based on the elapsed wall-clock time. Streams are recycled
	/// once they have fully left the bottom of the destination rectangle.
	/// </summary>
	private void Advance(float elapsed, float heightInCells)
	{
		if (elapsed <= 0)
		{
			return;
		}

		foreach (var stream in _streams)
		{
			stream.Head += stream.Speed * elapsed;

			if (stream.Head - stream.Length + 1 > heightInCells)
			{
				StartStream(stream);
				continue;
			}

			// Twinkle: occasionally swap a random glyph
			if (_random.NextDouble() < 0.2)
			{
				stream.Chars[_random.Next(stream.Length)] = RandomDigit();
			}
		}
	}

	private void StartStream(Stream stream)
	{
		stream.Length = _random.Next(6, 18);
		stream.Speed = 9f + (float)_random.NextDouble() * 14f;
		stream.Chars = NewStream(stream.Length);
		// Begin above the viewport so the stream scrolls in from the top
		stream.Head = -(stream.Length * 0.5f + (float)_random.NextDouble() * stream.Length * 0.5f);
	}

	private char[] NewStream(int length)
	{
		var chars = new char[length];
		for (var i = 0; i < length; i++)
		{
			chars[i] = RandomDigit();
		}

		return chars;
	}

	private char RandomDigit() => Digits[_random.Next(Digits.Length)];

	private static Color WithAlpha(Color color, byte alpha) => new Color(color.R, color.G, color.B, alpha);
}