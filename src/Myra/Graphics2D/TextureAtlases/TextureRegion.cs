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
	/// Represents a rectangular region within a texture.
	/// </summary>
	public class TextureRegion : IImage
	{
		private readonly Rectangle _bounds;

		/// <summary>
		/// Gets or sets the name of the texture region.
		/// </summary>
		public string Name { get; set; }

#if MONOGAME || FNA || STRIDE
		private readonly Texture2D _texture;
		/// <summary>
		/// Gets the texture that contains this region.
		/// </summary>
		public Texture2D Texture
		{
			get { return _texture; }
		}
#else
		private readonly object _texture;
		/// <summary>
		/// Gets the texture that contains this region.
		/// </summary>
		public object Texture
		{
			get { return _texture; }
		}
#endif

		/// <summary>
		/// Gets the bounds of the texture region.
		/// </summary>
		public Rectangle Bounds
		{
			get { return _bounds; }
		}

		/// <summary>
		/// Gets the size of the texture region.
		/// </summary>
		public Point Size
		{
			get
			{
				return new Point(Bounds.Width, Bounds.Height);
			}
		}

		/// <summary>
		/// Gets or sets the texture filtering mode to use when drawing this region.
		/// When null, the filtering from the render context is used.
		/// </summary>
		public TextureFiltering? Filter { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TextureRegion"/> class with the specified texture and bounds.
		/// </summary>
		/// <param name="texture">The texture to use.</param>
		/// <param name="bounds">The bounds of the region within the texture.</param>
		/// <exception cref="ArgumentNullException"><paramref name="texture"/> is null.</exception>
		public TextureRegion(Texture2D texture, Rectangle bounds)
		{
			if (texture == null)
			{
				throw new ArgumentNullException("texture");
			}

			_texture = texture;
			_bounds = bounds;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TextureRegion"/> class based on another region with an offset bounds.
		/// </summary>
		/// <param name="region">The source texture region.</param>
		/// <param name="bounds">The new bounds relative to the source region.</param>
		/// <exception cref="ArgumentNullException"><paramref name="region"/> is null.</exception>
		public TextureRegion(TextureRegion region, Rectangle bounds)
		{
			if (region == null)
			{
				throw new ArgumentNullException("region");
			}

			_texture = region.Texture;
			bounds.Offset(region.Bounds.Location);
			_bounds = bounds;
		}

#if MONOGAME || FNA || STRIDE
		/// <summary>
		/// Initializes a new instance of the <see cref="TextureRegion"/> class that covers the whole texture.
		/// </summary>
		/// <param name="texture">The texture to use.</param>
		public TextureRegion(Texture2D texture) : this(texture, new Rectangle(0, 0, texture.Width, texture.Height))
		{
		}
#else
		/// <summary>
		/// Initializes a new instance of the <see cref="TextureRegion"/> class that covers the whole texture.
		/// </summary>
		/// <param name="texture">The texture to use.</param>
		public TextureRegion(Texture2D texture) : this(texture, new Rectangle(0, 0,
			MyraEnvironment.Platform.Renderer.TextureManager.GetTextureSize(texture).X,
			MyraEnvironment.Platform.Renderer.TextureManager.GetTextureSize(texture).Y))
		{
		}
#endif

		/// <summary>
		/// Draws the texture region to the specified render context at the given destination.
		/// </summary>
		/// <param name="context">The render context to draw to.</param>
		/// <param name="dest">The destination rectangle where the region will be drawn.</param>
		/// <param name="color">The color to blend with the texture.</param>
		public virtual void Draw(RenderContext context, Rectangle dest, Color color)
		{
			context.Draw(Texture, dest, Bounds, color, textureFiltering: Filter);
		}

		/// <summary>
		/// Returns the name of the texture region.
		/// </summary>
		/// <returns>The region name.</returns>
		public override string ToString() => Name;
	}
}