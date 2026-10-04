using System;
using Myra.Graphics2D.TextureAtlases;
using Myra.MML;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.Brushes
{
	/// <summary>
	/// A brush that fills areas with a rectangle with rounded corners and a border around an
	/// interior, which is either filled with a separate color or left empty.
	/// </summary>
	/// <remarks>
	/// The brush is backed by a procedurally generated nine-patch texture, therefore the corners
	/// keep their shape at any destination size. The texture is created lazily on the first
	/// <see cref="Draw"/> call and regenerated whenever any of the brush properties change.
	/// <para>
	/// Myra must be set up before the brush is drawn (MyraEnvironment.Game for MonoGame/FNA/Stride
	/// or MyraEnvironment.Platform for the platform-agnostic builds).
	/// </para>
	/// </remarks>
	public class RoundedCornersHollowBrush : IBrush, IHasColor, IDisposable
	{
		/// <summary>
		/// The default width and height, in pixels, of the generated texture.
		/// </summary>
		public const int DefaultSize = 64;

		/// <summary>
		/// The default corner radius, in source texture pixels.
		/// </summary>
		public const int DefaultRadius = 16;

		/// <summary>
		/// The default border width, in source texture pixels.
		/// </summary>
		public const int DefaultBorderWidth = 4;

		private Color _color = Color.White;
		private Color _fillColor = Color.Transparent;
		private int _borderWidth = DefaultBorderWidth;
		private int _radius = DefaultRadius;
		private int _size = DefaultSize;

		private NinePatchRegion _region;
		private bool _regionDirty = true;
		private bool _disposed;

		/// <summary>
		/// Gets or sets the color of the border.
		/// </summary>
		public Color Color
		{
			get { return _color; }
			set
			{
				if (_color != value)
				{
					_color = value;
					_regionDirty = true;
				}
			}
		}

		/// <summary>
		/// Gets or sets the color of the interior. Set it to a transparent color to leave the
		/// interior empty.
		/// </summary>
		public Color FillColor
		{
			get { return _fillColor; }
			set
			{
				if (_fillColor != value)
				{
					_fillColor = value;
					_regionDirty = true;
				}
			}
		}

		/// <summary>
		/// Gets or sets the thickness of the border, in source texture pixels.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The value is negative or greater than half of <see cref="Size"/>.
		/// </exception>
		public int BorderWidth
		{
			get { return _borderWidth; }
			set
			{
				RoundedCornersPainter.ValidateBorderWidth(value, _size, nameof(BorderWidth));

				if (_borderWidth != value)
				{
					_borderWidth = value;
					_regionDirty = true;
				}
			}
		}

		/// <summary>
		/// Gets or sets the corner radius, in source texture pixels.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The value is negative or greater than half of <see cref="Size"/>.
		/// </exception>
		public int Radius
		{
			get { return _radius; }
			set
			{
				RoundedCornersPainter.ValidateRadius(value, _size, nameof(Radius));

				if (_radius != value)
				{
					_radius = value;
					_regionDirty = true;
				}
			}
		}

		/// <summary>
		/// Gets or sets the width and height, in pixels, of the generated texture. Larger values
		/// provide smoother, higher-resolution corners. Increase it (along with
		/// <see cref="Radius"/> and <see cref="BorderWidth"/>) for high-DPI displays.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
		public int Size
		{
			get { return _size; }
			set
			{
				RoundedCornersPainter.ValidateSize(value, nameof(Size));

				if (_size != value)
				{
					_size = value;

					if (_radius > _size / 2)
					{
						_radius = _size / 2;
					}

					if (_borderWidth > _size / 2)
					{
						_borderWidth = _size / 2;
					}

					_regionDirty = true;
				}
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="RoundedCornersHollowBrush"/> class with a
		/// white border of <see cref="DefaultBorderWidth"/> pixels and an empty interior.
		/// </summary>
		public RoundedCornersHollowBrush()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="RoundedCornersHollowBrush"/> class.
		/// </summary>
		/// <param name="color">The color of the border.</param>
		/// <param name="fillColor">
		/// The color the interior is filled with. Use a transparent color to leave the interior empty.
		/// </param>
		/// <param name="borderWidth">The thickness of the border, in source texture pixels.</param>
		public RoundedCornersHollowBrush(Color color, Color fillColor, int borderWidth = DefaultBorderWidth)
		{
			_color = color;
			_fillColor = fillColor;
			_borderWidth = borderWidth;
		}

		/// <summary>
		/// Draws a rectangle with rounded corners and a border to the specified render context at
		/// the given destination.
		/// </summary>
		/// <param name="context">The render context to draw to.</param>
		/// <param name="dest">The destination rectangle where the brush will be drawn.</param>
		/// <param name="color">The color to blend with the brush's colors.</param>
		/// <exception cref="ObjectDisposedException">The brush was disposed.</exception>
		public void Draw(RenderContext context, Rectangle dest, Color color)
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(RoundedCornersHollowBrush));
			}

			if (dest.Width <= 0 || dest.Height <= 0)
			{
				return;
			}

			if (_regionDirty)
			{
				RoundedCornersPainter.Regenerate(context, ref _region,
					RoundedCornersPainter.CreateHollow(_size, _radius, _borderWidth, _color, _fillColor));
				_regionDirty = false;
			}

			_region.Draw(context, dest, color);
		}

		/// <summary>
		/// Releases the generated texture.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Releases the generated texture.
		/// </summary>
		/// <param name="disposing">true when called from <see cref="Dispose()"/>.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			RoundedCornersPainter.Dispose(_region);

			_region = null;
			_regionDirty = true;
		}
	}
}
