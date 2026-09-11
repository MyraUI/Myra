using System.ComponentModel;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using Myra.Attributes;


#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// Specifies how an image is resized to fit available space.
	/// </summary>
	public enum ImageResizeMode
	{
		/// <summary>
		/// Stretch the image to fill available space without preserving aspect ratio.
		/// </summary>
		Stretch,

		/// <summary>
		/// Resize the image while maintaining its aspect ratio.
		/// </summary>
		KeepAspectRatio
	}

	/// <summary>
	/// An image widget that displays a texture region with optional resize modes.
	/// </summary>
	public class Image : Widget
	{
		private IImage[] _renderables = new IImage[WidgetVisualStateTotal];

		/// <summary>
		/// Gets or sets the renderable to display in the image widget's normal state.
		/// </summary>
		[Category("Appearance/Renderable")]
		[StylePropertyPath("Image")]
		public IImage Renderable
		{
			get
			{
				return _renderables[WidgetVisualStateNormal];
			}

			set
			{
				if (value == _renderables[WidgetVisualStateNormal])
				{
					return;
				}

				_renderables[WidgetVisualStateNormal] = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the renderable to display when the image widget is disabled.
		/// </summary>
		[Category("Appearance/Renderable")]
		[StylePropertyPath("DisabledImage")]
		public IImage DisabledRenderable
		{
			get
			{
				return _renderables[WidgetVisualStateDisabled];
			}

			set
			{
				if (value == _renderables[WidgetVisualStateDisabled])
				{
					return;
				}

				_renderables[WidgetVisualStateDisabled] = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the renderable to display when the mouse is over the image widget.
		/// </summary>
		[Category("Appearance/Renderable")]
		[StylePropertyPath("OverImage")]
		public IImage OverRenderable
		{
			get
			{
				return _renderables[WidgetVisualStateOver];
			}

			set
			{
				if (value == _renderables[WidgetVisualStateOver])
				{
					return;
				}

				_renderables[WidgetVisualStateOver] = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the renderable to display when the image widget has focus.
		/// </summary>
		[Category("Appearance/Renderable")]
		[StylePropertyPath("FocusedImage")]
		public IImage FocusedRenderable
		{
			get
			{
				return _renderables[WidgetVisualStateFocused];
			}

			set
			{
				if (value == _renderables[WidgetVisualStateFocused])
				{
					return;
				}

				_renderables[WidgetVisualStateFocused] = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the renderable to display when the image widget is pressed.
		/// </summary>
		[Category("Appearance/Renderable")]
		[StylePropertyPath("PressedImage")]
		public IImage PressedRenderable
		{
			get
			{
				return _renderables[WidgetVisualStatePressed];
			}

			set
			{
				if (value == _renderables[WidgetVisualStatePressed])
				{
					return;
				}

				_renderables[WidgetVisualStatePressed] = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the color multiplier applied when rendering the image.
		/// </summary>
		[Category("Appearance")]
		[DefaultValue("#FFFFFFFF")]
		public Color Color { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the texture filtering override for this image, or null to use the global default set by <see cref="MyraEnvironment.ImageTextureFiltering"/>.
		/// </summary>
		[Category("Appearance")]
		[DefaultValue(null)]
		public TextureFiltering? TextureFiltering { get; set; }

		/// <summary>
		/// Gets or sets how the image is resized to fit available space.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(ImageResizeMode.Stretch)]
		public ImageResizeMode ResizeMode { get; set; }

		/// <summary>
		/// Measures the size required for the image, considering all image states (normal, over, pressed).
		/// </summary>
		/// <param name="availableSize">The available size for the image.</param>
		/// <returns>The measured size needed for the image.</returns>
		protected override Point InternalMeasure(Point availableSize)
		{
			var result = Mathematics.PointZero;

			for (var i = 0; i < WidgetVisualStateTotal; ++i)
			{
				if (_renderables[i] != null)
				{
					var sz = _renderables[i].Size;

					if (sz.X > result.X)
					{
						result.X = sz.X;
					}

					if (sz.Y > result.Y)
					{
						result.Y = sz.Y;
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Renders the image with the appropriate state-based image (normal, over, or pressed).
		/// </summary>
		/// <param name="context">The render context to draw with.</param>
		public override void InternalRender(RenderContext context)
		{
			var image = GetCurrentVisual(_renderables);
			if (image == null)
			{
				return;
			}

			var bounds = ActualBounds;
			if (ResizeMode == ImageResizeMode.KeepAspectRatio)
			{
				var aspect = (float)image.Size.X / image.Size.Y;
				bounds.Height = (int)(bounds.Width * aspect);
			}

			var oldTextureFiltering = context.ImageTextureFiltering;
			if (TextureFiltering != null)
			{
				context.ImageTextureFiltering = TextureFiltering.Value;
			}
			image.Draw(context, bounds, Color);

			if (TextureFiltering != null)
			{
				context.ImageTextureFiltering = oldTextureFiltering;
			}
		}

		/// <summary>
		/// Applies the specified widget style to this image.
		/// </summary>
		/// <param name="style">The widget style to apply.</param>
		protected override void ApplyStyle(WidgetStyle style)
		{
			base.ApplyStyle(style);

			var imageStyle = (ImageStyle)style;
			Renderable = imageStyle.Image;
			DisabledRenderable = imageStyle.DisabledImage;
			FocusedRenderable = imageStyle.FocusedImage;
			OverRenderable = imageStyle.OverImage;
			PressedRenderable = imageStyle.PressedImage;
		}

		/// <summary>
		/// Applies the specified pressable image style to this image.
		/// </summary>
		/// <param name="style">The pressable image style to apply.</param>
		public void ApplyImageStyle(ImageStyle style) => ApplyStyle(style);

		/// <summary>
		/// Copies all properties from another widget to this image.
		/// </summary>
		/// <param name="w">The widget to copy properties from.</param>
		protected internal override void CopyFrom(Widget w)
		{
			base.CopyFrom(w);

			var image = (Image)w;

			Color = image.Color;
			ResizeMode = image.ResizeMode;

			for (var i = 0; i < WidgetVisualStateTotal; ++i)
			{
				_renderables[i] = image._renderables[i];
			}
		}
	}
}