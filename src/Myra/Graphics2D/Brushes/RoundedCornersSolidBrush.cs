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
	/// A brush that fills areas with a solid rectangle with rounded corners.
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
	public class RoundedCornersSolidBrush : IBrush, IHasColor, IDisposable
	{
		/// <summary>
		/// The default width and height, in pixels, of the generated texture.
		/// </summary>
		public const int DefaultSize = 64;

		/// <summary>
		/// The default corner radius, in source texture pixels.
		/// </summary>
		public const int DefaultRadius = 16;

		private Color _color = Color.White;
		private int _radius = DefaultRadius;
		private int _size = DefaultSize;

		private NinePatchRegion _region;
		private bool _regionDirty = true;
		private bool _disposed;

		/// <summary>
		/// Gets or sets the color the rounded rectangle is filled with.
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
		/// <see cref="Radius"/>) for high-DPI displays.
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

					_regionDirty = true;
				}
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="RoundedCornersSolidBrush"/> class filled with white.
		/// </summary>
		public RoundedCornersSolidBrush()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="RoundedCornersSolidBrush"/> class.
		/// </summary>
		/// <param name="color">The color the rounded rectangle is filled with.</param>
		public RoundedCornersSolidBrush(Color color)
		{
			_color = color;
		}

		/// <summary>
		/// Draws a solid rectangle with rounded corners to the specified render context at the given destination.
		/// </summary>
		/// <param name="context">The render context to draw to.</param>
		/// <param name="dest">The destination rectangle where the brush will be drawn.</param>
		/// <param name="color">The color to blend with the brush's color.</param>
		/// <exception cref="ObjectDisposedException">The brush was disposed.</exception>
		public void Draw(RenderContext context, Rectangle dest, Color color)
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(RoundedCornersSolidBrush));
			}

			if (dest.Width <= 0 || dest.Height <= 0)
			{
				return;
			}

			if (_regionDirty)
			{
				RoundedCornersPainter.Regenerate(context, ref _region, RoundedCornersPainter.CreateSolid(_size, _radius, _color));
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
