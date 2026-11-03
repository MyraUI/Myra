using System.ComponentModel;

namespace Myra.Graphics2D.UI.Styles
{
	/// <summary>
	/// Base style class that defines common visual properties for widgets.
	/// </summary>
	public class WidgetStyle
	{
		/// <summary>
		/// Gets or sets the style identifier.
		/// </summary>
		[Category("Layout")]
		public string Id { get; set; }

		/// <summary>
		/// Gets or sets the width of the widget, or null for auto-sizing.
		/// </summary>
		[Category("Layout")]
		public Dimension? Width { get; set; }

		/// <summary>
		/// Gets or sets the height of the widget, or null for auto-sizing.
		/// </summary>
		[Category("Layout")]
		public Dimension? Height { get; set; }

		/// <summary>
		/// Gets or sets the minimum width of the widget in pixels, or null for no minimum.
		/// </summary>
		[Category("Layout")]
		public int? MinWidth { get; set; }

		/// <summary>
		/// Gets or sets the minimum height of the widget in pixels, or null for no minimum.
		/// </summary>
		[Category("Layout")]
		public int? MinHeight { get; set; }

		/// <summary>
		/// Gets or sets the maximum width of the widget in pixels, or null for no maximum.
		/// </summary>
		[Category("Layout")]
		public int? MaxWidth { get; set; }

		/// <summary>
		/// Gets or sets the maximum height of the widget in pixels, or null for no maximum.
		/// </summary>
		[Category("Layout")]
		public int? MaxHeight { get; set; }

		/// <summary>
		/// Gets or sets the brush used for the widget's background.
		/// </summary>
		[Category("Appearance/Background")]
		public IBrush Background { get; set; }

		/// <summary>
		/// Gets or sets the brush used for the widget's background when the mouse is over it.
		/// </summary>
		[Category("Appearance/Background")]
		public IBrush OverBackground { get; set; }

		/// <summary>
		/// Gets or sets the brush used for the widget's background when it is disabled.
		/// </summary>
		[Category("Appearance/Background")]
		public IBrush DisabledBackground { get; set; }

		/// <summary>
		/// Gets or sets the brush used for the widget's background when it has focus.
		/// </summary>
		[Category("Appearance/Background")]
		public IBrush FocusedBackground { get; set; }

		/// <summary>
		/// Gets or sets the brush used for the widget's background when it is pressed.
		/// </summary>
		[Category("Appearance/Background")]
		public IBrush PressedBackground { get; set; }

		/// <summary>
		/// Gets or sets the brush used for the widget's border.
		/// </summary>
		[Category("Appearance/Border")]
		public IBrush Border { get; set; }

		/// <summary>
		/// Gets or sets the brush used for the widget's border when the mouse is over it.
		/// </summary>
		[Category("Appearance/Border")]
		public IBrush OverBorder { get; set; }

		/// <summary>
		/// Gets or sets the brush used for the widget's border when it is disabled.
		/// </summary>
		[Category("Appearance/Border")]
		public IBrush DisabledBorder { get; set; }

		/// <summary>
		/// Gets or sets the brush used for the widget's border when it has focus.
		/// </summary>
		[Category("Appearance/Border")]
		public IBrush FocusedBorder { get; set; }

		/// <summary>
		/// Gets or sets the brush used for the widget's border when it is pressed.
		/// </summary>
		[Category("Appearance/Border")]
		public IBrush PressedBorder { get; set; }

		/// <summary>
		/// Gets or sets the space outside the widget's border.
		/// </summary>
		[Category("Layout")]
		public Thickness Margin { get; set; }

		/// <summary>
		/// Gets or sets the thickness of the widget's border.
		/// </summary>
		[Category("Layout")]
		public Thickness BorderThickness { get; set; }

		/// <summary>
		/// Gets or sets the space inside the widget's border but outside its content.
		/// </summary>
		[Category("Layout")]
		public Thickness Padding { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="WidgetStyle"/> class.
		/// </summary>
		public WidgetStyle()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WidgetStyle"/> class by copying properties from another style.
		/// </summary>
		/// <param name="style">The source style to copy from.</param>
		public WidgetStyle(WidgetStyle style)
		{
			Width = style.Width;
			Height = style.Height;
			MinWidth = style.MinWidth;
			MinHeight = style.MinHeight;
			MaxWidth = style.MaxWidth;
			MaxHeight = style.MaxHeight;

			Background = style.Background;
			OverBackground = style.OverBackground;
			DisabledBackground = style.DisabledBackground;
			FocusedBackground = style.FocusedBackground;
			PressedBackground = style.PressedBackground;

			Border = style.Border;
			OverBorder = style.OverBorder;
			DisabledBorder = style.DisabledBorder;
			FocusedBorder = style.FocusedBorder;
			PressedBorder = style.PressedBorder;

			Margin = style.Margin;
			BorderThickness = style.BorderThickness;
			Padding = style.Padding;
		}

		/// <summary>
		/// Creates a deep copy of this style.
		/// </summary>
		/// <returns>A new WidgetStyle instance with the same properties.</returns>
		public virtual WidgetStyle Clone()
		{
			return new WidgetStyle(this);
		}
	}
}
