using System;
using System.Collections.Generic;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Myra;
using Myra.Graphics2D;
using Color = Microsoft.Xna.Framework.Color;

namespace Myra.Samples;

/// <summary>
/// An <see cref="IBrush"/> that renders Matrix-style "digital rain": bright green digits
/// continuously falling through the destination rectangle. Each glyph is drawn with
/// <see cref="RenderContext.DrawString"/>, using fonts taken from
/// <see cref="DefaultAssets.DebugFontSystem"/> at a diverse range of sizes.
/// </summary>
public class MatrixBrush : IBrush
{
	private const string Digits = "0123456789";

	private static readonly Color BodyColor = new Color(0, 255, 96);
	private static readonly Color HeadColor = new Color(205, 255, 205);

	private sealed class Stream
	{
		public char[] Chars = Array.Empty<char>();
		public SpriteFontBase Font;
		public float CellSize; // glyph size and cell stride for this column
		public float X;        // left edge of this column in local coordinates
		public float Head;     // position of the leading glyph in cells
		public float Speed;    // glyphs per second
		public int Length;     // number of glyphs in the stream
	}

	private readonly Random _random;
	private readonly List<Stream> _streams = new List<Stream>();
	private readonly Dictionary<int, SpriteFontBase> _fontCache = new Dictionary<int, SpriteFontBase>();

	private DateTime _lastTime = DateTime.UtcNow;
	private int _columns = -1;

	/// <summary>
	/// Gets or sets the smallest font size (in pixels) used by any rain column.
	/// </summary>
	public float MinFontSize { get; set; } = 14f;

	/// <summary>
	/// Gets or sets the largest font size (in pixels) used by any rain column.
	/// </summary>
	public float MaxFontSize { get; set; } = 40f;

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

		var avgCell = (MinFontSize + MaxFontSize) * 0.5f;
		var columns = Math.Max(1, (int)(dest.Width / avgCell));

		EnsureStreams(columns);
		Advance((float)(DateTime.UtcNow - _lastTime).TotalSeconds, dest.Height);
		_lastTime = DateTime.UtcNow;

		var tint = color.A / 255f;

		foreach (var stream in _streams)
		{
			var heightInCells = dest.Height / stream.CellSize;
			var glyphSize = stream.Font.MeasureString("0");
			var offsetX = (stream.CellSize - glyphSize.X) * 0.5f;
			var offsetY = (stream.CellSize - glyphSize.Y) * 0.5f;

			for (var i = 0; i < stream.Length; i++)
			{
				var row = stream.Head - i;
				if (row + stream.Length - 1 < 0)
				{
					continue;
				}

				var alpha = i == 0 ? 255 : Math.Max(0, 255 - (int)(255f * i / stream.Length));
				var glyphColor = i == 0 ? HeadColor : BodyColor;

				context.DrawString(stream.Font, stream.Chars[i].ToString(),
					new Vector2(dest.X + stream.X + offsetX, dest.Y + row * stream.CellSize + offsetY),
					WithAlpha(glyphColor, (byte)(alpha * tint)), Vector2.One);
			}
		}
	}

	/// <summary>
	/// Gets a <see cref="SpriteFontBase"/> of the requested size, reusing previously
	/// requested sizes from the debug font system.
	/// </summary>
	private SpriteFontBase GetFont(int size)
	{
		if (!_fontCache.TryGetValue(size, out var font))
		{
			font = DefaultAssets.DebugFontSystem.GetFont(size);
			_fontCache[size] = font;
		}

		return font;
	}

	/// <summary>
	/// Recreates the streams when the number of columns changes (e.g. the destination width or
	/// the font size range was modified).
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
			StartStream(stream, i);
			_streams.Add(stream);
		}
	}

	/// <summary>
	/// Advances the falling glyphs based on the elapsed wall-clock time. Streams are recycled
	/// once they have fully left the bottom of the destination rectangle.
	/// </summary>
	private void Advance(float elapsed, float height)
	{
		if (elapsed <= 0)
		{
			return;
		}

		for (var i = 0; i < _streams.Count; i++)
		{
			var stream = _streams[i];
			var heightInCells = height / stream.CellSize;

			stream.Head += stream.Speed * elapsed;

			if (stream.Head - stream.Length + 1 > heightInCells)
			{
				StartStream(stream, i);
				continue;
			}

			// Twinkle: occasionally swap a random glyph
			if (_random.NextDouble() < 0.2)
			{
				stream.Chars[_random.Next(stream.Length)] = RandomDigit();
			}
		}
	}

	private void StartStream(Stream stream, int index)
	{
		stream.Length = _random.Next(6, 18);
		stream.Speed = 5f + (float)_random.NextDouble() * 14f;
		stream.CellSize = _random.Next((int)MinFontSize, (int)MaxFontSize + 1);
		stream.Font = GetFont((int)stream.CellSize);
		stream.Chars = NewStream(stream.Length);
		// Begin above the viewport so the stream scrolls in from the top
		stream.Head = -(stream.Length * 0.5f + (float)_random.NextDouble() * stream.Length * 0.5f);

		// Column offset relative to the destination origin, based on the widths of the
		// columns to the left (their CellSize values at the time this stream started)
		stream.X = 0;
		for (var i = 0; i < index && i < _streams.Count; i++)
		{
			stream.X += _streams[i].CellSize;
		}
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