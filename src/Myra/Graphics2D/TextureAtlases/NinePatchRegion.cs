using System;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#elif STRIDE
using Stride.Core.Mathematics;
using Texture2D = Stride.Graphics.Texture;
#else
using System.Drawing;
using Texture2D = System.Object;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.TextureAtlases
{
	/// <summary>
	/// Represents a nine-patch texture region that can be stretched while maintaining corner and edge appearance.
	/// </summary>
	public class NinePatchRegion : TextureRegion
	{
		private readonly Thickness _info;

		private readonly Rectangle? _topLeft,
			_topCenter,
			_topRight,
			_centerLeft,
			_center,
			_centerRight,
			_bottomLeft,
			_bottomCenter,
			_bottomRight;

		/// <summary>
		/// Gets the thickness information that defines the nine-patch layout.
		/// </summary>
		public Thickness Info
		{
			get { return _info; }
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NinePatchRegion"/> class with the specified texture, bounds, and patch information.
		/// </summary>
		/// <param name="texture">The texture to use.</param>
		/// <param name="bounds">The bounds of the region within the texture.</param>
		/// <param name="info">The thickness information that defines the nine-patch layout.</param>
		public NinePatchRegion(Texture2D texture, Rectangle bounds, Thickness info) : base(texture, bounds)
		{
			_info = info;

			var centerWidth = bounds.Width - info.Left - info.Right;
			var centerHeight = bounds.Height - info.Top - info.Bottom;

			var y = bounds.Y;
			if (info.Top > 0)
			{
				if (info.Left > 0)
				{
					_topLeft = new Rectangle(bounds.X, y, info.Left, info.Top);
				}

				if (centerWidth > 0)
				{
					_topCenter = new Rectangle(bounds.X + info.Left,
							y,
							centerWidth,
							info.Top);
				}

				if (info.Right > 0)
				{
					_topRight = new Rectangle(bounds.X + info.Left + centerWidth, y, info.Right, info.Top);
				}
			}

			y += info.Top;
			if (centerHeight > 0)
			{
				if (info.Left > 0)
				{
					_centerLeft = new Rectangle(bounds.X, y, info.Left, centerHeight);
				}

				if (centerWidth > 0)
				{
					_center = new Rectangle(bounds.X + info.Left, y, centerWidth, centerHeight);
				}

				if (info.Right > 0)
				{
					_centerRight = new Rectangle(bounds.X + info.Left + centerWidth, y, info.Right, centerHeight);
				}
			}

			y += centerHeight;
			if (info.Bottom > 0)
			{
				if (info.Left > 0)
				{
					_bottomLeft = new Rectangle(bounds.X, y, info.Left, info.Bottom);
				}

				if (centerWidth > 0)
				{
					_bottomCenter = new Rectangle(bounds.X + info.Left, y, centerWidth, info.Bottom);
				}

				if (info.Right > 0)
				{
					_bottomRight = new Rectangle(bounds.X + info.Left + centerWidth, y, info.Right, info.Bottom);
				}
			}
		}

		/// <summary>
		/// Draws the nine-patch region to the specified render context at the given destination, scaling the center while maintaining corner and edge appearance.
		/// </summary>
		/// <param name="context">The render context to draw to.</param>
		/// <param name="dest">The destination rectangle where the region will be drawn.</param>
		/// <param name="color">The color to blend with the texture.</param>
		public override void Draw(RenderContext context, Rectangle dest, Color color)
		{
			var y = dest.Y;

			var textureFiltering = Filter;

			var left = Math.Min(_info.Left, dest.Width);
			var top = Math.Min(_info.Top, dest.Height);
			var right = Math.Min(_info.Right, dest.Width);
			var bottom = Math.Min(_info.Bottom, dest.Height);

			var centerWidth = dest.Width - left - right;
			if (centerWidth < 0)
			{
				centerWidth = 0;
			}

			var centerHeight = dest.Height - top - bottom;
			if (centerHeight < 0)
			{
				centerHeight = 0;
			}

			if (_topLeft != null)
			{
				context.Draw(Texture, new Rectangle(dest.X, y, left, top), _topLeft.Value, color, textureFiltering: textureFiltering);
			}

			if (_topCenter != null && centerWidth > 0)
			{
				context.Draw(Texture, new Rectangle(dest.X + left, y, centerWidth, top), _topCenter.Value, color, textureFiltering: textureFiltering);
			}

			if (_topRight != null)
			{
				context.Draw(Texture, new Rectangle(dest.X + Info.Left + centerWidth, y, right, top), _topRight.Value, color, textureFiltering: textureFiltering);
			}

			y += top;
			if (_centerLeft != null && centerHeight > 0)
			{
				context.Draw(Texture, new Rectangle(dest.X, y, left, centerHeight), _centerLeft.Value, color, textureFiltering: textureFiltering);
			}

			if (_center != null && centerWidth > 0 && centerHeight > 0)
			{
				context.Draw(Texture, new Rectangle(dest.X + left, y, centerWidth, centerHeight), _center.Value, color, textureFiltering: textureFiltering);
			}

			if (_centerRight != null && centerHeight > 0)
			{
				context.Draw(Texture, new Rectangle(dest.X + Info.Left + centerWidth, y, right, centerHeight), _centerRight.Value, color, textureFiltering: textureFiltering);
			}

			y += centerHeight;
			if (_bottomLeft != null)
			{
				context.Draw(Texture, new Rectangle(dest.X, y, left, bottom), _bottomLeft.Value, color, textureFiltering: textureFiltering);
			}

			if (_bottomCenter != null && centerWidth > 0)
			{
				context.Draw(Texture, new Rectangle(dest.X + left, y, centerWidth, bottom), _bottomCenter.Value, color, textureFiltering: textureFiltering);
			}

			if (_bottomRight != null)
			{
				context.Draw(Texture, new Rectangle(dest.X + Info.Left + centerWidth, y, right, bottom), _bottomRight.Value, color, textureFiltering: textureFiltering);
			}
		}
	}
}