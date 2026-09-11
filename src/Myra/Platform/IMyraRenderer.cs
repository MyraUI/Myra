using FontStashSharp.Interfaces;
using Myra.Graphics2D;
using System;
using System.Drawing;
using System.Numerics;
using Color = FontStashSharp.FSColor;

namespace Myra.Platform
{
	/// <summary>
	/// Specifies the type of rendering backend to use.
	/// </summary>
	public enum RendererType
	{
		/// <summary>Uses sprite-based rendering.</summary>
		Sprite,
		/// <summary>Uses quad-based rendering.</summary>
		Quad
	}

	/// <summary>
	/// Provides rendering functionality for the Myra UI framework.
	/// </summary>
	public interface IMyraRenderer: IDisposable
	{
		/// <summary>
		/// Gets the texture manager for loading and managing textures.
		/// </summary>
		ITexture2DManager TextureManager { get; }

		/// <summary>
		/// Gets the type of renderer being used.
		/// </summary>
		RendererType RendererType { get; }

		/// <summary>
		/// Scissor Rectangle
		/// </summary>
		Rectangle Scissor { get; set; }

		/// <summary>
		/// Prepares the graphics device for drawing sprites with specified render state options.
		/// </summary>
		/// <param name="textureFiltering"></param>
		void Begin(TextureFiltering textureFiltering);

		/// <summary>
		/// Flushes the sprite batch.
		/// </summary>
		void End();

		/// <summary>
		/// Draws a sprite
		/// </summary>
		/// <param name="texture"></param>
		/// <param name="pos"></param>
		/// <param name="src"></param>
		/// <param name="color"></param>
		/// <param name="rotation"></param>
		/// <param name="scale"></param>
		/// <param name="depth"></param>
		void DrawSprite(object texture, Vector2 pos, Rectangle? src, Color color, float rotation, Vector2 scale, float depth);

		/// <summary>
		/// Draws a textured quad
		/// </summary>
		/// <param name="texture"></param>
		/// <param name="topLeft"></param>
		/// <param name="topRight"></param>
		/// <param name="bottomLeft"></param>
		/// <param name="bottomRight"></param>
		void DrawQuad(object texture, ref VertexPositionColorTexture topLeft, ref VertexPositionColorTexture topRight, ref VertexPositionColorTexture bottomLeft, ref VertexPositionColorTexture bottomRight);
	}
}
