using System;
using System.ComponentModel;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using System.Xml.Serialization;
using Myra.MML;
using Myra.Graphics2D.UI.Properties;
using Myra.Attributes;
using Myra.Events;



#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#elif STRIDE
using Stride.Core.Mathematics;
using Stride.Input;
#else
using System.Numerics;
using System.Drawing;
using Myra.Platform;
using Matrix = System.Numerics.Matrix3x2;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// Specifies the direction(s) in which a widget can be dragged.
	/// </summary>
	[Flags]
	public enum DragDirection
	{
		/// <summary>The widget cannot be dragged.</summary>
		None = 0,
		/// <summary>The widget can be dragged vertically.</summary>
		Vertical = 1,
		/// <summary>The widget can be dragged horizontally.</summary>
		Horizontal = 2,
		/// <summary>The widget can be dragged both vertically and horizontally.</summary>
		Both = Vertical | Horizontal
	}

	/// <summary>
	/// The base class for all UI widgets in the Myra framework.
	/// </summary>
	public partial class Widget : BaseObject, ITransformable
	{
		private MouseCursorType? _mouseCursorType;
		private Vector2? _startPos;
		private Point _startLeftTop;
		private Thickness _margin, _borderThickness, _padding;
		private int _left, _top;
		private int? _minWidth, _minHeight, _maxWidth, _maxHeight, _width, _height;
		private int _zIndex;
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private bool _measureDirty = true;
		private bool _arrangeDirty = true;
		private Desktop _desktop;

		private Point _lastMeasureSize;
		private Point _lastMeasureAvailableSize;

		private Rectangle _containerBounds;
		private Rectangle _layoutBounds;
		private bool _visible;

		private float _opacity = 1.0f;

		private bool _enabled;
		private bool _isKeyboardFocused = false;
		private Vector2 _scale = Vector2.One;
		private Vector2 _transformOrigin = new Vector2(0.5f, 0.5f);
		private float _rotation = 0.0f;
		private bool _transformDirty = true;
		private Transform _transform;

		/// <summary>
		/// Internal use only. (MyraPad)
		/// </summary>
		[DefaultValue(Stylesheet.DefaultStyleName)]
		public string StyleName { get; set; }

		/// <summary>
		/// Gets or sets the left position of the widget in pixels.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(0)]
		public int Left
		{
			get { return _left; }

			set
			{
				if (value == _left)
				{
					return;
				}

				_left = value;
				InvalidateTransform();
				FireLocationChanged();
			}
		}

		/// <summary>
		/// Gets or sets the top position of the widget in pixels.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(0)]
		public int Top
		{
			get { return _top; }

			set
			{
				if (value == _top)
				{
					return;
				}

				_top = value;
				InvalidateTransform();
				FireLocationChanged();
			}
		}

		/// <summary>
		/// Gets or sets the minimum width of the widget in pixels.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(null)]
		public int? MinWidth
		{
			get { return _minWidth; }
			set
			{
				if (value == _minWidth)
				{
					return;
				}

				_minWidth = value;
				InvalidateMeasure();
				FireSizeChanged();
			}
		}

		/// <summary>
		/// Gets or sets the maximum width of the widget in pixels.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(null)]
		public int? MaxWidth
		{
			get { return _maxWidth; }
			set
			{
				if (value == _maxWidth)
				{
					return;
				}

				_maxWidth = value;
				InvalidateMeasure();
				FireSizeChanged();
			}
		}

		/// <summary>
		/// Gets or sets the width of the widget in pixels.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(null)]
		public int? Width
		{
			get
			{
				return _width;
			}

			set
			{
				if (value == _width)
				{
					return;
				}

				_width = value;
				InvalidateMeasure();
				FireSizeChanged();
			}
		}

		/// <summary>
		/// Gets or sets the minimum height of the widget in pixels.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(null)]
		public int? MinHeight
		{
			get { return _minHeight; }
			set
			{
				if (value == _minHeight)
				{
					return;
				}

				_minHeight = value;
				InvalidateMeasure();
				FireSizeChanged();
			}
		}

		/// <summary>
		/// Gets or sets the maximum height of the widget in pixels.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(null)]
		public int? MaxHeight
		{
			get { return _maxHeight; }
			set
			{
				if (value == _maxHeight)
				{
					return;
				}

				_maxHeight = value;
				InvalidateMeasure();
				FireSizeChanged();
			}
		}

		/// <summary>
		/// Gets or sets the height of the widget in pixels.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(null)]
		public int? Height
		{
			get
			{
				return _height;
			}

			set
			{
				if (value == _height)
				{
					return;
				}

				_height = value;
				InvalidateMeasure();
				FireSizeChanged();
			}
		}

		/// <summary>
		/// Gets or sets the outer margin around the widget.
		/// </summary>
		[Category("Layout")]
		[DesignerFolded]
		public Thickness Margin
		{
			get
			{
				return _margin;
			}

			set
			{
				if (_margin == value)
				{
					return;
				}

				_margin = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the brush used to draw the border around the widget.
		/// </summary>
		[Category("Layout")]
		public IBrush Border { get; set; }

		/// <summary>
		/// Gets or sets the thickness of the border around the widget.
		/// </summary>
		[Category("Layout")]
		[DesignerFolded]
		public Thickness BorderThickness
		{
			get
			{
				return _borderThickness;
			}

			set
			{
				if (_borderThickness == value)
				{
					return;
				}

				_borderThickness = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the inner padding inside the widget's borders.
		/// </summary>
		[Category("Layout")]
		[DesignerFolded]
		public Thickness Padding
		{
			get
			{
				return _padding;
			}

			set
			{
				if (_padding == value)
				{
					return;
				}

				_padding = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the widget within its parent container.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(HorizontalAlignment.Left)]
		public virtual HorizontalAlignment HorizontalAlignment
		{
			get { return _horizontalAlignment; }

			set
			{
				if (value == _horizontalAlignment)
				{
					return;
				}

				_horizontalAlignment = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the vertical alignment of the widget within its parent container.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(VerticalAlignment.Top)]
		public virtual VerticalAlignment VerticalAlignment
		{
			get { return _verticalAlignment; }

			set
			{
				if (value == _verticalAlignment)
				{
					return;
				}

				_verticalAlignment = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the widget is enabled and can receive user input.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool Enabled
		{
			get
			{
				return _enabled;
			}

			set
			{
				if (value == _enabled)
				{
					return;
				}

				_enabled = value;

				foreach (var item in ChildrenCopy)
				{
					item.Enabled = value;
				}

				EnabledChanged.Invoke(this, InputEventType.EnabledChanged);
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the widget is visible and should be rendered.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool Visible
		{
			get { return _visible; }

			set
			{
				if (_visible == value)
				{
					return;
				}

				_visible = value;
				LocalMousePosition = null;
				LocalTouchPosition = null;

				OnVisibleChanged();
			}
		}

		/// <summary>
		/// Gets or sets the direction(s) in which the widget can be dragged.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(DragDirection.None)]
		public virtual DragDirection DragDirection { get; set; } = DragDirection.None;

		[XmlIgnore]
		[Browsable(false)]
		internal bool IsDraggable { get => DragDirection != DragDirection.None; }

		/// <summary>
		/// Gets or sets the z-order (depth) of the widget for rendering. Widgets with higher z-index values are rendered on top.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(0)]
		public int ZIndex
		{
			get => _zIndex;
			set
			{
				if (value == _zIndex)
				{
					return;
				}

				_zIndex = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the mouse cursor type displayed when the mouse is over the widget.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(null)]
		public virtual MouseCursorType? MouseCursor
		{
			get => _mouseCursorType;
			set
			{
				if (value == _mouseCursorType)
				{
					return;
				}

				_mouseCursorType = value;
				foreach (var child in Children)
				{
					child.MouseCursor = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the tooltip text to display when the mouse hovers over the widget.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(null)]
		public string Tooltip { get; set; }


		/// <summary>
		/// Gets or sets the scale factor applied to the widget. A value of (1, 1) is the original size.
		/// </summary>
		[Category("Transform")]
		[DefaultValue("1, 1")]
		[DesignerFolded]
		public Vector2 Scale
		{
			get => _scale;
			set
			{
				if (value == _scale)
				{
					return;
				}

				_scale = value;
				InvalidateTransform();
			}
		}

		/// <summary>
		/// Gets or sets the origin point for transformations (scale and rotation), in normalized coordinates where (0, 0) is top-left and (1, 1) is bottom-right.
		/// </summary>
		[Category("Transform")]
		[DefaultValue("0.5, 0.5")]
		[DesignerFolded]
		public Vector2 TransformOrigin
		{
			get => _transformOrigin;
			set
			{
				if (value == _transformOrigin)
				{
					return;
				}

				_transformOrigin = value;
				InvalidateTransform();
			}
		}

		/// <summary>
		/// Gets or sets the rotation angle of the widget in degrees, applied around the TransformOrigin point.
		/// </summary>
		[Category("Transform")]
		[DefaultValue(0.0f)]
		[DesignerFolded]
		public float Rotation
		{
			get => _rotation;

			set
			{
				if (value == _rotation)
				{
					return;
				}

				_rotation = value;
				InvalidateTransform();
			}
		}

		/// <summary>
		/// Gets or sets a widget that should be used as the drag handle for this widget. If set, dragging this handle widget will move the parent widget.
		/// </summary>
		[XmlIgnore]
		[Browsable(false)]
		public Widget DragHandle { get; set; }

		/// <summary>
		/// Determines whether the widget had been placed on Desktop
		/// </summary>
		[XmlIgnore]
		[Browsable(false)]
		public bool IsPlaced
		{
			get
			{
				return Desktop != null;
			}
		}

		/// <summary>
		/// Gets or sets the desktop that this widget is attached to.
		/// </summary>
		[XmlIgnore]
		[Browsable(false)]
		public virtual Desktop Desktop
		{
			get
			{
				return _desktop;
			}

			internal set
			{
				if (_desktop != null && value == null)
				{
					if (_desktop.FocusedKeyboardWidget == this)
					{
						_desktop.FocusedKeyboardWidget = null;
					}

					if (_desktop.Tooltip != null && _desktop.Tooltip.Tag == this)
					{
						_desktop.HideTooltip();
					}
				}

				LocalMousePosition = null;
				LocalTouchPosition = null;

				_desktop = value;

				if (_desktop != null)
				{
					InvalidateMeasure();
				}

				SubscribeOnTouchMoved(IsPlaced && IsDraggable);

				foreach (var child in ChildrenCopy)
				{
					child.Desktop = value;
				}

				OnPlacedChanged();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this widget is modal and blocks interaction with other widgets.
		/// </summary>
		[XmlIgnore]
		[Browsable(false)]
		public bool IsModal { get; set; }

		/// <summary>
		/// Gets or sets the opacity of the widget, where 0.0 is fully transparent and 1.0 is fully opaque.
		/// </summary>
		[Category("Appearance")]
		[DefaultValue(1.0f)]
		[Range(0.0f, 1.0f)]
		public float Opacity
		{
			get
			{
				return _opacity;
			}

			set
			{
				if (value < 0 || value > 1.0f)
				{
					throw new ArgumentOutOfRangeException("value");
				}

				_opacity = value;
			}
		}

		/// <summary>
		/// Dynamic layout expression
		/// </summary>
		[XmlIgnore]
		[Browsable(false)]
		public Layout2D Layout2d { get; set; } = Layout2D.NullLayout;

		/// <summary>
		/// Gets or sets the brush used to draw the background of the widget in its normal state.
		/// </summary>
		[Category("Appearance")]
		public IBrush Background { get; set; }

		/// <summary>
		/// Gets or sets the brush used to draw the background of the widget when the mouse is hovering over it.
		/// </summary>
		[Category("Appearance")]
		public IBrush OverBackground { get; set; }

		/// <summary>
		/// Gets or sets the brush used to draw the background of the widget when it is disabled.
		/// </summary>
		[Category("Appearance")]
		public IBrush DisabledBackground { get; set; }

		/// <summary>
		/// Gets or sets the brush used to draw the background of the widget when it has keyboard focus.
		/// </summary>
		[Category("Appearance")]
		public IBrush FocusedBackground { get; set; }

		/// <summary>
		/// Gets or sets the brush used to draw the border of the widget when the mouse is hovering over it.
		/// </summary>
		[Category("Appearance")]
		public IBrush OverBorder
		{
			get; set;
		}

		/// <summary>
		/// Gets or sets the brush used to draw the border of the widget when it is disabled.
		/// </summary>
		[Category("Appearance")]
		public IBrush DisabledBorder
		{
			get; set;
		}

		/// <summary>
		/// Gets or sets the brush used to draw the border of the widget when it has keyboard focus.
		/// </summary>
		[Category("Appearance")]
		public IBrush FocusedBorder
		{
			get; set;
		}

		/// <summary>
		/// Gets or sets a value indicating whether the widget's content is clipped to its bounds.
		/// </summary>
		[Category("Appearance")]
		[DefaultValue(false)]
		public virtual bool ClipToBounds { get; set; }

		/// <summary>
		/// Gets the parent widget in the widget hierarchy.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Widget Parent { get; internal set; }

		/// <summary>
		/// Gets or sets a custom object associated with the widget for application-specific use.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public object Tag { get; set; }

		/// <summary>
		/// Zero-based bounds
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Rectangle Bounds => new Rectangle(0, 0, _layoutBounds.Width, _layoutBounds.Height);

		/// <summary>
		/// Gets the actual bounds of the widget's content area, excluding margin, border, and padding.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Rectangle ActualBounds => Bounds - _margin - _borderThickness - _padding;

		/// <summary>
		/// Bounds - Margin
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		internal Rectangle BorderBounds => Bounds - _margin;

		/// <summary>
		/// Gets the bounds of the widget's background area, excluding the border.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore] protected Rectangle BackgroundBounds => BorderBounds - _borderThickness;

		/// <summary>
		/// Gets the bounds of the widget's container area.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Rectangle ContainerBounds => _containerBounds;

		/// <summary>
		/// Gets the total width consumed by margin, border, and padding (MBP = Margin + Border + Padding).
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public int MBPWidth => Margin.Left + Margin.Right +
					BorderThickness.Left + BorderThickness.Right +
					Padding.Left + Padding.Right;

		/// <summary>
		/// Gets the total height consumed by margin, border, and padding (MBP = Margin + Border + Padding).
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public int MBPHeight => Margin.Top + Margin.Bottom +
					BorderThickness.Top + BorderThickness.Bottom +
					Padding.Top + Padding.Bottom;

		/// <summary>
		/// Gets or sets a value indicating whether the widget can accept keyboard focus.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public bool AcceptsKeyboardFocus { get; set; }


		/// <summary>
		/// Gets a value indicating whether the widget currently has keyboard focus.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public bool IsKeyboardFocused
		{
			get
			{
				return _isKeyboardFocused;
			}

			internal set
			{
				if (value == _isKeyboardFocused)
				{
					return;
				}

				_isKeyboardFocused = value;
				KeyboardFocusChanged?.Invoke(this, InputEventType.KeyboardFocusChanged);
			}
		}

		internal Transform Transform
		{
			get
			{
				UpdateTransform();

				return _transform;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the over-background element should be drawn for this widget.
		/// </summary>
		protected virtual bool UseOverBackground => IsMouseInside;

		/// <summary>
		/// Gets or sets an action to be invoked before the widget is rendered.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Action<RenderContext> BeforeRender;

		/// <summary>
		/// Gets or sets an action to be invoked after the widget is rendered.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Action<RenderContext> AfterRender;

		/// <summary>
		/// Initializes a new instance of the Widget class.
		/// </summary>
		public Widget()
		{
			Visible = true;
			Enabled = true;
			DragHandle = this;

			Children.CollectionChanged += ChildrenOnCollectionChanged;
		}

		/// <summary>
		/// Gets the current background brush based on the widget's state (disabled, focused, or hovered).
		/// </summary>
		/// <returns>The appropriate background brush for the current state, or null if none is set.</returns>
		public virtual IBrush GetCurrentBackground()
		{
			var result = Background;

			if (!Enabled && DisabledBackground != null)
			{
				result = DisabledBackground;
			}
			else if (Enabled && IsKeyboardFocused && FocusedBackground != null)
			{
				result = FocusedBackground;
			}
			else if (UseOverBackground && OverBackground != null)
			{
				result = OverBackground;
			}

			return result;
		}

		/// <summary>
		/// Gets the current border brush based on the widget's state (disabled, focused, or hovered).
		/// </summary>
		/// <returns>The appropriate border brush for the current state, or null if none is set.</returns>
		public virtual IBrush GetCurrentBorder()
		{
			var result = Border;

			if (!Enabled && DisabledBorder != null)
			{
				result = DisabledBorder;
			}
			else if (Enabled && IsKeyboardFocused && FocusedBorder != null)
			{
				result = FocusedBorder;
			}
			else if (IsMouseInside && OverBorder != null)
			{
				result = OverBorder;
			}

			return result;
		}

		/// <summary>
		/// Brings the widget to the front (top of the z-order) within its parent or desktop.
		/// </summary>
		public void BringToFront()
		{
			if (Desktop == null)
				return;

			var widgets = Parent != null ? Parent.Children : Desktop.Widgets;

			if (widgets[widgets.Count - 1] == this) return;

			widgets.Remove(this);
			widgets.Add(this);
		}

		/// <summary>
		/// Sends the widget to the back (bottom of the z-order) within its parent or desktop.
		/// </summary>
		public void BringToBack()
		{
			var widgets = Parent != null ? Parent.Children : Desktop.Widgets;

			if (widgets[widgets.Count - 1] == this) return;

			widgets.Remove(this);
			widgets.Insert(0, this);
		}

		/// <summary>
		/// Renders the widget and its children to the specified render context.
		/// </summary>
		/// <param name="context">The render context to draw to.</param>
		public void Render(RenderContext context)
		{
			if (!Visible)
			{
				return;
			}

			if (!string.IsNullOrEmpty(Tooltip) && (Desktop.Tooltip == null || Desktop.Tooltip.Tag != this) &&
				_lastMouseMovement != null && (DateTime.Now - _lastMouseMovement.Value).TotalMilliseconds > MyraEnvironment.TooltipDelayInMs)
			{
				var pos = Desktop.MousePosition;
				pos.X += MyraEnvironment.TooltipOffset.X;
				pos.Y += MyraEnvironment.TooltipOffset.Y;
				Desktop.ShowTooltip(this, pos);
				_lastMouseMovement = null;
			}

			UpdateArrange();

			var oldTransform = context.Transform;

			// Apply widget transforms
			context.Transform = Transform;

			Rectangle? oldScissorRectangle = null;
			if (context.Transform.Rotation.IsZero())
			{
				var absoluteBounds = Transform.Apply(Bounds);
				var scissorBounds = Rectangle.Intersect(context.Scissor, absoluteBounds);

				if (scissorBounds.Width == 0 || scissorBounds.Height == 0)
				{
					// Culled by scissor
					context.Transform = oldTransform;
					return;
				}

				if (ClipToBounds)
				{
					oldScissorRectangle = context.Scissor;

					context.Scissor = scissorBounds;
				}
			}

			var oldOpacity = context.Opacity;
			context.AddOpacity(Opacity);

			// Draw Background
			var background = GetCurrentBackground();
			if (background != null)
			{
				background.Draw(context, BackgroundBounds);
			}

			// Draw Borders
			var border = GetCurrentBorder();
			if (border != null)
			{
				var borderBounds = BorderBounds;
				if (BorderThickness.Left > 0)
				{
					border.Draw(context, new Rectangle(borderBounds.X, borderBounds.Y, BorderThickness.Left, borderBounds.Height));
				}

				if (BorderThickness.Top > 0)
				{
					border.Draw(context, new Rectangle(borderBounds.X, borderBounds.Y, borderBounds.Width, BorderThickness.Top));
				}

				if (BorderThickness.Right > 0)
				{
					border.Draw(context, new Rectangle(borderBounds.Right - BorderThickness.Right, borderBounds.Y, BorderThickness.Right, borderBounds.Height));
				}

				if (BorderThickness.Bottom > 0)
				{
					border.Draw(context, new Rectangle(borderBounds.X, borderBounds.Bottom - BorderThickness.Bottom, borderBounds.Width, BorderThickness.Bottom));
				}
			}

			// Internal rendering
			BeforeRender?.Invoke(context);
			InternalRender(context);
			AfterRender?.Invoke(context);

			if (oldScissorRectangle != null)
			{
				// Restore scissor
				context.Scissor = oldScissorRectangle.Value;
			}

			// Optional debug rendering
			if (MyraEnvironment.DrawWidgetsFrames)
			{
				context.DrawRectangle(Bounds, Color.LightGreen);
			}

			if (MyraEnvironment.DrawKeyboardFocusedWidgetFrame && IsKeyboardFocused)
			{
				context.DrawRectangle(Bounds, Color.Red);
			}

			if (MyraEnvironment.DrawMouseHoveredWidgetFrame && IsMouseInside)
			{
				context.DrawRectangle(Bounds, Color.Yellow);
			}

			// Restore context settings
			context.Transform = oldTransform;
			context.Opacity = oldOpacity;
		}

		/// <summary>
		/// Renders the widget's content (called after backgrounds and borders are rendered). Default implementation renders all child widgets.
		/// </summary>
		/// <param name="context">The render context to draw to.</param>
		public virtual void InternalRender(RenderContext context)
		{
			foreach (var child in ChildrenCopy)
			{
				child.Render(context);
			}
		}

		/// <summary>
		/// Measures the widget to determine its desired size based on available space.
		/// </summary>
		/// <param name="availableSize">The available space for the widget.</param>
		/// <returns>The desired size of the widget.</returns>
		public Point Measure(Point availableSize)
		{
			if (!_measureDirty && _lastMeasureAvailableSize == availableSize)
			{
				return _lastMeasureSize;
			}

			Point result;

			// Lerp available size by Width/Height or MaxWidth/MaxHeight
			if (Width != null && availableSize.X > Width.Value)
			{
				availableSize.X = Width.Value;
			}
			else if (MaxWidth != null && availableSize.X > MaxWidth.Value)
			{
				availableSize.X = MaxWidth.Value;
			}

			if (Height != null && availableSize.Y > Height.Value)
			{
				availableSize.Y = Height.Value;
			}
			else if (MaxHeight != null && availableSize.Y > MaxHeight.Value)
			{
				availableSize.Y = MaxHeight.Value;
			}

			availableSize.X -= MBPWidth;
			availableSize.Y -= MBPHeight;

			// Do the actual measure
			// Previously I skipped this step if both Width & Height were set
			// However that raised an issue - custom InternalMeasure stuff(such as in Menu.InternalMeasure) was skipped as well
			// So now InternalMeasure is called every time
			result = InternalMeasure(availableSize);

			result.X += MBPWidth;
			result.Y += MBPHeight;

			var marginX = _margin.Left + _margin.Right;
			var marginY = _margin.Top + _margin.Bottom;

			// Result lerp
			if (Width.HasValue)
			{
				result.X = Width.Value + marginX;
			}
			else
			{
				if (MinWidth.HasValue && result.X < MinWidth.Value)
				{
					result.X = MinWidth.Value + marginX;
				}

				if (MaxWidth.HasValue && result.X > MaxWidth.Value)
				{
					result.X = MaxWidth.Value + marginX;
				}
			}

			if (Height.HasValue)
			{
				result.Y = Height.Value + marginY;
			}
			else
			{
				if (MinHeight.HasValue && result.Y < MinHeight.Value)
				{
					result.Y = MinHeight.Value + marginY;
				}

				if (MaxHeight.HasValue && result.Y > MaxHeight.Value)
				{
					result.Y = MaxHeight.Value + marginY;
				}
			}

			_lastMeasureSize = result;
			_lastMeasureAvailableSize = availableSize;
			_measureDirty = false;

			return result;
		}

		/// <summary>
		/// Arranges the widget within the specified container bounds.
		/// </summary>
		/// <param name="containerBounds">The bounds of the container in which to arrange the widget.</param>
		public void Arrange(Rectangle containerBounds)
		{
			if (!_arrangeDirty && _containerBounds == containerBounds)
			{
				return;
			}

			_arrangeDirty = true;
			_containerBounds = containerBounds;
			UpdateArrange();
		}

		/// <summary>
		/// Updates the widget's arrangement if it is marked as dirty.
		/// </summary>
		public void UpdateArrange()
		{
			if (!_arrangeDirty)
			{
				return;
			}

			Point size;
			if (HorizontalAlignment != HorizontalAlignment.Stretch ||
					VerticalAlignment != VerticalAlignment.Stretch)
			{
				size = Measure(_containerBounds.Size());
			}
			else
			{
				size = _containerBounds.Size();
			}

			if (size.X > _containerBounds.Width)
			{
				size.X = _containerBounds.Width;
			}

			if (size.Y > _containerBounds.Height)
			{
				size.Y = _containerBounds.Height;
			}

			// Resolve possible conflict beetween Alignment set to Streth and Size explicitly set
			var containerSize = _containerBounds.Size();
			if (HorizontalAlignment == HorizontalAlignment.Stretch && Width != null && Width.Value < containerSize.X)
			{
				containerSize.X = Width.Value;
			}

			if (VerticalAlignment == VerticalAlignment.Stretch && Height != null && Height.Value < containerSize.Y)
			{
				containerSize.Y = Height.Value;
			}

			// Align
			var layoutBounds = LayoutUtils.Align(containerSize, size, HorizontalAlignment, VerticalAlignment);
			layoutBounds.Offset(_containerBounds.Location);

			_layoutBounds = layoutBounds;
			InvalidateTransform();

			InternalArrange();
			ArrangeUpdated.Invoke(this, InputEventType.ArrangeUpdated);

			_arrangeDirty = false;
		}

		/// <summary>
		/// Arranges the child widgets within this widget's bounds.
		/// </summary>
		protected virtual void InternalArrange()
		{
			if (ChildrenLayout == null)
			{
				return;
			}

			ChildrenLayout.Arrange(ChildrenCopy, ActualBounds);
		}

		/// <summary>
		/// Measures the widget and its children to determine the required size.
		/// </summary>
		/// <param name="availableSize">The available size for this widget and its children.</param>
		/// <returns>The measured size of this widget.</returns>
		protected virtual Point InternalMeasure(Point availableSize)
		{
			if (ChildrenLayout == null)
			{
				return Mathematics.PointZero;
			}

			return ChildrenLayout.Measure(ChildrenCopy, availableSize);
		}


		/// <summary>
		/// Marks the widget's arrangement as dirty, requiring a recalculation on the next update.
		/// </summary>
		public void InvalidateArrange()
		{
			_arrangeDirty = true;
		}

		/// <summary>
		/// Finds a child widget by its id, throwing an exception if not found.
		/// </summary>
		/// <param name="id">The id of the widget to find.</param>
		/// <returns>The widget with the specified id.</returns>
		/// <exception cref="Exception">Thrown when a widget with the specified id is not found.</exception>
		public Widget EnsureWidgetById(string id)
		{
			var result = FindChildById(id);
			if (result == null)
			{
				throw new Exception(string.Format($"Could not find widget with id {id}"));
			}

			return result;
		}

		internal virtual void InvalidateTransform()
		{
			_transformDirty = true;

			foreach (var child in ChildrenCopy)
			{
				child.InvalidateTransform();
			}
		}

		/// <summary>
		/// Marks the widget's measurement as dirty, requiring a recalculation on the next update. This cascades up to parent widgets.
		/// </summary>
		public virtual void InvalidateMeasure()
		{
			_measureDirty = true;

			InvalidateArrange();

			if (Parent != null)
			{
				Parent.InvalidateMeasure();
			}
			else if (Desktop != null)
			{
				Desktop.InvalidateLayout();
			}
		}

		/// <summary>
		/// Applies the properties from a widget style to this widget.
		/// </summary>
		/// <param name="style">The widget style to apply.</param>
		public void ApplyWidgetStyle(WidgetStyle style)
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

			Border = style.Border;
			OverBorder = style.OverBorder;
			DisabledBorder = style.DisabledBorder;
			FocusedBorder = style.FocusedBorder;

			Margin = style.Margin;
			BorderThickness = style.BorderThickness;
			Padding = style.Padding;
		}

		/// <summary>
		/// Sets the widget's style using a specific stylesheet and style name.
		/// </summary>
		/// <param name="stylesheet">The stylesheet to use for applying the style.</param>
		/// <param name="name">The name of the style to apply.</param>
		public void SetStyle(Stylesheet stylesheet, string name)
		{
			StyleName = name;

			if (StyleName != null)
			{
				InternalSetStyle(stylesheet, StyleName);
			}
		}

		/// <summary>
		/// Sets the widget's style using the current stylesheet and the specified style name.
		/// </summary>
		/// <param name="name">The name of the style to apply from the current stylesheet.</param>
		public void SetStyle(string name)
		{
			SetStyle(Stylesheet.Current, name);
		}

		/// <summary>
		/// Applies a named style from a stylesheet to this widget.
		/// </summary>
		/// <param name="stylesheet">The stylesheet containing the styles to apply.</param>
		/// <param name="name">The name of the style to apply.</param>
		protected virtual void InternalSetStyle(Stylesheet stylesheet, string name)
		{
		}

		/// <summary>
		/// Fires the KeyDown event for the specified key.
		/// </summary>
		/// <param name="k">The key that was pressed.</param>
		protected void FireKeyDown(Keys k)
		{
			KeyDown.Invoke(this, k, InputEventType.KeyDown);
		}

		/// <summary>
		/// Called when a keyboard key is pressed while this widget has focus.
		/// </summary>
		/// <param name="k">The key that was pressed.</param>
		public virtual void OnKeyDown(Keys k)
		{
			FireKeyDown(k);
		}

		/// <summary>
		/// Called when a keyboard key is released while this widget has focus.
		/// </summary>
		/// <param name="k">The key that was released.</param>
		public virtual void OnKeyUp(Keys k)
		{
			KeyUp.Invoke(this, k, InputEventType.KeyUp);
		}

		/// <summary>
		/// Called when a character is entered while this widget has focus.
		/// </summary>
		/// <param name="c">The character that was entered.</param>
		public virtual void OnChar(char c)
		{
			Char.Invoke(this, c, InputEventType.CharInput);
		}

		/// <summary>
		/// Called when the widget's placement (position or size) has changed.
		/// </summary>
		protected virtual void OnPlacedChanged()
		{
			PlacedChanged?.Invoke(this, InputEventType.PlacedChanged);
		}

		/// <summary>
		/// Called when the widget's visibility state changes.
		/// </summary>
		public virtual void OnVisibleChanged()
		{
			InvalidateMeasure();
			VisibleChanged.Invoke(this, InputEventType.VisibleChanged);
		}

		/// <summary>
		/// Called when the widget loses keyboard focus.
		/// </summary>
		public virtual void OnLostKeyboardFocus()
		{
			IsKeyboardFocused = false;
		}

		/// <summary>
		/// Called when the widget receives keyboard focus.
		/// </summary>
		public virtual void OnGotKeyboardFocus()
		{
			IsKeyboardFocused = true;
		}

		/// <summary>
		/// Removes this widget from its parent widget's children collection.
		/// </summary>
		public void RemoveFromParent()
		{
			if (Parent == null)
			{
				return;
			}

			Parent.Children.Remove(this);
		}

		/// <summary>
		/// Removes this widget from the desktop's widgets collection.
		/// </summary>
		public void RemoveFromDesktop()
		{
			Desktop.Widgets.Remove(this);
		}

		private void FireLocationChanged()
		{
			LocationChanged.Invoke(this, InputEventType.LocationChanged);
		}

		private void FireSizeChanged()
		{
			SizeChanged.Invoke(this, InputEventType.SizeChanged);
		}

		/// <summary>
		/// Sets this widget to have keyboard focus.
		/// </summary>
		public void SetKeyboardFocus()
		{
			Desktop.FocusedKeyboardWidget = this;
		}

		private void SubscribeOnTouchMoved(bool subscribe)
		{
			if (Parent != null)
			{
				Parent.TouchMoved -= DesktopOnTouchMoved;
				Parent.TouchUp -= DesktopTouchUp;
			}
			else if (Desktop != null)
			{
				Desktop.TouchMoved -= DesktopOnTouchMoved;
				Desktop.TouchUp -= DesktopTouchUp;
			}

			if (subscribe)
			{
				if (Parent != null)
				{
					Parent.TouchMoved += DesktopOnTouchMoved;
					Parent.TouchUp += DesktopTouchUp;
				}
				else if (Desktop != null)
				{
					Desktop.TouchMoved += DesktopOnTouchMoved;
					Desktop.TouchUp += DesktopTouchUp;
				}
			}
		}

		private void DesktopOnTouchMoved(object sender, MyraEventArgs args)
		{
			if (_startPos == null || !IsDraggable || Desktop == null)
			{
				return;
			}

			var parent = Parent != null ? (ITransformable)Parent : Desktop;
			var newPos = parent.ToLocal(new Vector2(Desktop.TouchPosition.Value.X, Desktop.TouchPosition.Value.Y));
			var delta = newPos - _startPos.Value;

			var newLeft = Left;
			var newTop = Top;
			if (DragDirection.HasFlag(DragDirection.Horizontal))
			{
				newLeft = _startLeftTop.X + (int)delta.X;
			}

			if (DragDirection.HasFlag(DragDirection.Vertical))
			{
				newTop = _startLeftTop.Y + (int)delta.Y;
			}

			var parentBounds = Parent != null ? Parent.Bounds : Desktop.InternalBounds;
			if (newLeft < 0)
			{
				newLeft = 0;
			}

			if (newLeft + Bounds.Width > parentBounds.Width)
			{
				newLeft = parentBounds.Width - Bounds.Width;
			}

			if (newTop < 0)
			{
				newTop = 0;
			}

			if (newTop + Bounds.Height > parentBounds.Height)
			{
				newTop = parentBounds.Height - Bounds.Height;
			}

			Left = newLeft;
			Top = newTop;
		}

		/// <summary>
		/// Converts a position from the widget's local coordinates to global (screen) coordinates.
		/// </summary>
		/// <param name="pos">The position in local coordinates.</param>
		/// <returns>The position in global coordinates.</returns>
		public Vector2 ToGlobal(Vector2 pos)
		{
			UpdateTransform();

			return Transform.Apply(pos);
		}

		/// <summary>
		/// Converts a position from the widget's local coordinates to global (screen) coordinates.
		/// </summary>
		/// <param name="pos">The position in local coordinates.</param>
		/// <returns>The position in global coordinates.</returns>
		public Point ToGlobal(Point pos)
		{
			UpdateTransform();

			return Transform.Apply(pos);
		}

		/// <summary>
		/// Converts a position from global (screen) coordinates to the widget's local coordinates.
		/// </summary>
		/// <param name="pos">The position in global coordinates.</param>
		/// <returns>The position in local coordinates.</returns>
		public Vector2 ToLocal(Vector2 pos)
		{
			UpdateTransform();

			return Transform.InverseApply(pos);
		}

		/// <summary>
		/// Converts a position from global (screen) coordinates to the widget's local coordinates.
		/// </summary>
		/// <param name="pos">The position in global coordinates.</param>
		/// <returns>The position in local coordinates.</returns>
		public Point ToLocal(Point pos) => ToLocal(new Vector2(pos.X, pos.Y)).ToPoint();

		/// <summary>
		/// Determines whether the specified global position is within the widget's bounds.
		/// </summary>
		/// <param name="globalPos">The position in global coordinates.</param>
		/// <returns>True if the position is within the widget's bounds; otherwise, false.</returns>
		public bool ContainsGlobalPoint(Point globalPos)
		{
			var localPos = ToLocal(globalPos);
			return BorderBounds.Contains(localPos);
		}

		private void UpdateTransform()
		{
			if (!_transformDirty)
			{
				return;
			}

			var p = new Point(_layoutBounds.X + Left, _layoutBounds.Y + Top);

			var localTransform = new Transform(p.ToVector2(),
				TransformOrigin * _layoutBounds.Size().ToVector2(),
				Scale,
				Rotation * (float)Math.PI / 180);

			if (Parent != null)
			{
				var transform = Parent.Transform;
				transform.AddTransform(ref localTransform);
				_transform = transform;
			}
			else if (Desktop != null)
			{
				var transform = Desktop.Transform;
				transform.AddTransform(ref localTransform);
				_transform = transform;
			}
			else
			{
				_transform = localTransform;
			}

			_transformDirty = false;
		}

		private void DesktopTouchUp(object sender, MyraEventArgs args)
		{
			_startPos = null;
		}

		/// <summary>
		/// Performs hit testing to determine which widget at the given global position should receive input events.
		/// </summary>
		/// <param name="p">The position in global coordinates to test.</param>
		/// <returns>The widget at the specified position, or null if no widget is there or this widget is not visible.</returns>
		public virtual Widget HitTest(Point p)
		{
			if (Desktop == null || !Visible || !ContainsGlobalPoint(p))
			{
				return null;
			}

			Widget result = null;
			for (var i = _childrenCopy.Count - 1; i >= 0; i--)
			{
				var child = _childrenCopy[i];
				result = child.HitTest(p);
				if (result != null)
				{
					break;
				}
			}

			var localPos = ToLocal(p);
			if (result == null && !InputFallsThrough(localPos))
			{
				result = this;
			}

			return result;
		}

		/// <summary>
		/// Determines whether input at the specified local position should fall through to widgets behind this widget.
		/// </summary>
		/// <param name="localPos">The position in the widget's local coordinates.</param>
		/// <returns>True if input should fall through; false if this widget should handle it.</returns>
		public virtual bool InputFallsThrough(Point localPos) => false;

		/// <summary>
		/// Creates a deep copy of this widget with all its properties and attached properties.
		/// </summary>
		/// <returns>A new widget instance that is a copy of this widget.</returns>
		public Widget Clone()
		{
			// Firstly try to use parameterless constructor
			var type = GetType();
			var constructor = type.GetConstructor(Type.EmptyTypes);

			Widget result;
			if (constructor != null)
			{
				result = (Widget)constructor.Invoke(new object[0]);
			}
			else
			{
				// Then string constructor
				result = (Widget)Activator.CreateInstance(GetType(), (string)null);
			}

			result.CopyFrom(this);

			// Copy attached properties
			foreach (var pair in AttachedPropertiesValues)
			{
				result.AttachedPropertiesValues[pair.Key] = pair.Value;
			}

			return result;
		}

		/// <summary>
		/// Copies all properties from another widget to this widget.
		/// </summary>
		/// <param name="w">The widget to copy properties from.</param>
		protected internal virtual void CopyFrom(Widget w)
		{
			StyleName = w.StyleName;
			Left = w.Left;
			Top = w.Top;
			MinWidth = w.MinWidth;
			MaxWidth = w.MaxWidth;
			Width = w.Width;
			MinHeight = w.MinHeight;
			MaxHeight = w.MaxHeight;
			Height = w.Height;
			Margin = w.Margin;
			Border = w.Border;
			BorderThickness = w.BorderThickness;
			Padding = w.Padding;
			HorizontalAlignment = w.HorizontalAlignment;
			VerticalAlignment = w.VerticalAlignment;
			Enabled = w.Enabled;
			Visible = w.Visible;
			DragDirection = w.DragDirection;
			ZIndex = w.ZIndex;
			MouseCursor = w.MouseCursor;
			Tooltip = w.Tooltip;
			Scale = w.Scale;
			TransformOrigin = w.TransformOrigin;
			Rotation = w.Rotation;
			DragHandle = w.DragHandle;
			IsModal = w.IsModal;
			Opacity = w.Opacity;
			Background = w.Background;
			OverBackground = w.OverBackground;
			DisabledBackground = w.DisabledBackground;
			FocusedBackground = w.FocusedBackground;
			OverBorder = w.OverBorder;
			DisabledBorder = w.DisabledBorder;
			FocusedBorder = w.FocusedBorder;
			ClipToBounds = w.ClipToBounds;
			Tag = w.Tag;
			AcceptsKeyboardFocus = w.AcceptsKeyboardFocus;
			BeforeRender = w.BeforeRender;
			AfterRender = w.AfterRender;
		}
	}
}