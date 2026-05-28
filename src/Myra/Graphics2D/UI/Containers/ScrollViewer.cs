using System;
using System.ComponentModel;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using System.Xml.Serialization;
using Myra.Attributes;
using Myra.Events;


#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
#endif

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// A container that displays its content with horizontal and/or vertical scrollbars for navigation when content exceeds available space.
	/// </summary>
	public class ScrollViewer : ContentControl
	{
		private readonly SingleItemLayout<Widget> _layout;
		private Orientation _scrollbarOrientation;
		internal bool _horizontalScrollingOn, _verticalScrollingOn;
		private bool _showHorizontalScrollBar, _showVerticalScrollBar;
		internal Rectangle _horizontalScrollbarFrame, _horizontalScrollbarThumb;
		internal Rectangle _verticalScrollbarFrame, _verticalScrollbarThumb;
		private int? _startBoundsPos;
		private int _thumbMaximumX, _thumbMaximumY;

		[Browsable(false)]
		[XmlIgnore]
		internal int VerticalThumbWidth => (_verticalScrollingOn && ShowVerticalScrollBar) ? _verticalScrollbarThumb.Width : 0;

		[Browsable(false)]
		[XmlIgnore]
		internal int HorizontalThumbHeight => (_horizontalScrollingOn && ShowHorizontalScrollBar) ? _horizontalScrollbarThumb.Height : 0;

		/// <summary>
		/// Gets the maximum scroll position for horizontal and vertical scrolling.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Point ScrollMaximum
		{
			get
			{
				if (Content == null)
				{
					return Mathematics.PointZero;
				}

				var bounds = ActualBounds;
				var result = new Point(Content.Bounds.Width - bounds.Width + VerticalThumbWidth,
								 Content.Bounds.Height - bounds.Height + HorizontalThumbHeight);

				if (result.X < 0)
				{
					result.X = 0;
				}

				if (result.Y < 0)
				{
					result.Y = 0;
				}

				return result;

			}
		}

		/// <summary>
		/// Gets or sets the current scroll position for both horizontal and vertical scrolling.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Point ScrollPosition
		{
			get
			{
				if (Content == null)
				{
					return Mathematics.PointZero;
				}

				return new Point(-Content.Left, -Content.Top);
			}
			set
			{
				if (Content == null)
				{
					return;
				}

				Content.Left = -value.X;
				Content.Top = -value.Y;
			}
		}

		internal Point ThumbPosition
		{
			get
			{
				var sp = ScrollPosition;
				var m = ScrollMaximum;

				var result = Mathematics.PointZero;
				if (m.X > 0)
				{
					result.X = sp.X * _thumbMaximumX / m.X;
				}

				if (m.Y > 0)
				{
					result.Y = sp.Y * _thumbMaximumY / m.Y;
				}

				return result;
			}
		}

		/// <summary>
		/// Gets or sets the image used for the horizontal scrollbar background.
		/// </summary>
		[Category("Appearance")]
		public IImage HorizontalScrollBackground
		{
			get; set;
		}

		/// <summary>
		/// Gets or sets the image used for the horizontal scrollbar knob (thumb).
		/// </summary>
		[Category("Appearance")]
		public IImage HorizontalScrollKnob
		{
			get; set;
		}

		/// <summary>
		/// Gets or sets the image used for the vertical scrollbar background.
		/// </summary>
		[Category("Appearance")]
		public IImage VerticalScrollBackground
		{
			get; set;
		}

		/// <summary>
		/// Gets or sets the image used for the vertical scrollbar knob (thumb).
		/// </summary>
		[Category("Appearance")]
		public IImage VerticalScrollKnob
		{
			get; set;
		}

		/// <summary>
		/// Gets or sets the multiplier for mouse wheel scrolling speed.
		/// </summary>
		[Category("Appearance")]
		[DefaultValue(10)]
		public int ScrollMultiplier { get; set; } = 10;

		/// <summary>
		/// Gets or sets the widget to display in the scroll viewer.
		/// </summary>
		[Browsable(false)]
		[Content]
		public override Widget Content
		{
			get => _layout.Child;

			set
			{
				_layout.Child = value;
				ResetScroll();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the horizontal scrollbar is visible.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool ShowHorizontalScrollBar
		{
			get
			{
				return _showHorizontalScrollBar;
			}

			set
			{
				if (value == _showHorizontalScrollBar)
				{
					return;
				}

				_showHorizontalScrollBar = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the vertical scrollbar is visible.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool ShowVerticalScrollBar
		{
			get
			{
				return _showVerticalScrollBar;
			}

			set
			{
				if (value == _showVerticalScrollBar)
				{
					return;
				}

				_showVerticalScrollBar = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the scroll viewer within its parent container.
		/// </summary>
		[DefaultValue(HorizontalAlignment.Stretch)]
		public override HorizontalAlignment HorizontalAlignment
		{
			get
			{
				return base.HorizontalAlignment;
			}
			set
			{
				base.HorizontalAlignment = value;
			}
		}

		/// <summary>
		/// Gets or sets the vertical alignment of the scroll viewer within its parent container.
		/// </summary>
		[DefaultValue(VerticalAlignment.Stretch)]
		public override VerticalAlignment VerticalAlignment
		{
			get
			{
				return base.VerticalAlignment;
			}
			set
			{
				base.VerticalAlignment = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether content extending beyond the scroll viewer bounds is clipped.
		/// </summary>
		[DefaultValue(true)]
		public override bool ClipToBounds
		{
			get
			{
				return base.ClipToBounds;
			}
			set
			{
				base.ClipToBounds = value;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this scroll viewer accepts mouse wheel input for scrolling.
		/// </summary>
		protected internal override bool AcceptsMouseWheel => _verticalScrollingOn;

		/// <summary>
		/// Gets or sets the desktop that manages this scroll viewer.
		/// </summary>
		public override Desktop Desktop
		{
			get
			{
				return base.Desktop;
			}

			internal set
			{
				if (Desktop != null)
				{
					Desktop.TouchMoved -= DesktopTouchMoved;
					Desktop.TouchUp -= DesktopTouchUp;
				}

				base.Desktop = value;

				if (Desktop != null)
				{
					Desktop.TouchMoved += DesktopTouchMoved;
					Desktop.TouchUp += DesktopTouchUp;
				}
			}
		}

		private int HorizontalScrollbarHeight
		{
			get
			{
				var result = 0;
				if (HorizontalScrollBackground != null)
				{
					result = HorizontalScrollBackground.Size.Y;
				}

				if (HorizontalScrollKnob != null && HorizontalScrollKnob.Size.Y > result)
				{
					result = HorizontalScrollKnob.Size.Y;
				}

				return result;
			}
		}

		private int VerticalScrollbarWidth
		{
			get
			{
				var result = 0;
				if (VerticalScrollBackground != null)
				{
					result = VerticalScrollBackground.Size.X;
				}

				if (VerticalScrollKnob != null && VerticalScrollKnob.Size.X > result)
				{
					result = VerticalScrollKnob.Size.X;
				}

				return result;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ScrollViewer"/> class.
		/// </summary>
		/// <param name="styleName">The name of the style to apply to the scroll viewer.</param>
		public ScrollViewer(string styleName = Stylesheet.DefaultStyleName)
		{
			_layout = new SingleItemLayout<Widget>(this);
			ChildrenLayout = _layout;

			ClipToBounds = true;
			_horizontalScrollingOn = _verticalScrollingOn = false;

			ShowVerticalScrollBar = ShowHorizontalScrollBar = true;

			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;

			SetStyle(styleName);
		}

		private void MoveThumb(int delta)
		{
			var scrollPosition = ScrollPosition;

			var maximum = ScrollMaximum;
			if (_scrollbarOrientation == Orientation.Horizontal)
			{
				var newPos = delta + scrollPosition.X;
				if (newPos < 0)
				{
					newPos = 0;
				}

				if (newPos > maximum.X)
				{
					newPos = maximum.X;
				}

				scrollPosition.X = newPos;
			}
			else
			{
				var newPos = delta + scrollPosition.Y;

				if (newPos < 0)
				{
					newPos = 0;
				}

				if (newPos > maximum.Y)
				{
					newPos = maximum.Y;
				}

				scrollPosition.Y = newPos;
			}

			ScrollPosition = scrollPosition;
		}

		/// <summary>
		/// Handles touch up events on the scroll viewer.
		/// </summary>
		public override void OnTouchUp()
		{
			base.OnTouchUp();

			_startBoundsPos = null;
		}

		/// <summary>
		/// Handles touch down events on the scroll viewer, including scrollbar interaction.
		/// </summary>
		public override void OnTouchDown()
		{
			base.OnTouchDown();

			if (Desktop == null)
			{
				return;
			}

			var touchPosition = ToLocal(Desktop.TouchPosition.Value);

			var r = _verticalScrollbarThumb;
			var thumbPosition = ThumbPosition;
			r.Y += thumbPosition.Y;
			if (ShowVerticalScrollBar && _verticalScrollingOn && r.Contains(touchPosition))
			{
				_startBoundsPos = Desktop.TouchPosition.Value.Y;
				_scrollbarOrientation = Orientation.Vertical;
			}

			r = _horizontalScrollbarThumb;
			r.X += thumbPosition.X;
			if (ShowHorizontalScrollBar && _horizontalScrollingOn && r.Contains(touchPosition))
			{
				_startBoundsPos = Desktop.TouchPosition.Value.X;
				_scrollbarOrientation = Orientation.Horizontal;
			}
		}

		/// <summary>
		/// Handles mouse wheel scrolling for vertical scrolling.
		/// </summary>
		/// <param name="delta">The mouse wheel delta value.</param>
		public override void OnMouseWheel(float delta)
		{
			base.OnMouseWheel(delta);

			if (!_verticalScrollingOn)
			{
				return;
			}

			var step = ScrollMultiplier * ScrollMaximum.Y / _thumbMaximumY;
			if (delta < 0)
			{
				_scrollbarOrientation = Orientation.Vertical;
				MoveThumb(step);
			}
			else if (delta > 0)
			{
				_scrollbarOrientation = Orientation.Vertical;
				MoveThumb(-step);
			}
		}

		/// <summary>
		/// Renders the scroll viewer content and scrollbars.
		/// </summary>
		/// <param name="context">The render context used for drawing.</param>
		public override void InternalRender(RenderContext context)
		{
			if (Content == null || !Content.Visible)
			{
				return;
			}

			// Render child
			base.InternalRender(context);

			var thumbPosition = ThumbPosition;
			if (_horizontalScrollingOn && ShowHorizontalScrollBar)
			{
				if (HorizontalScrollBackground != null)
				{
					HorizontalScrollBackground.Draw(context, _horizontalScrollbarFrame);
				}

				var r = _horizontalScrollbarThumb;
				r.X += thumbPosition.X;
				HorizontalScrollKnob.Draw(context, r);
			}

			if (_verticalScrollingOn && ShowVerticalScrollBar)
			{
				if (VerticalScrollBackground != null)
				{
					VerticalScrollBackground.Draw(context, _verticalScrollbarFrame);
				}

				var r = _verticalScrollbarThumb;
				r.Y += thumbPosition.Y;
				VerticalScrollKnob.Draw(context, r);
			}
		}

		/// <summary>
		/// Applies the specified style to the scroll viewer.
		/// </summary>
		/// <param name="style">The style to apply.</param>
		public void ApplyScrollViewerStyle(ScrollViewerStyle style)
		{
			HorizontalScrollBackground = style.HorizontalScrollBackground;
			HorizontalScrollKnob = style.HorizontalScrollKnob;
			VerticalScrollBackground = style.VerticalScrollBackground;
			VerticalScrollKnob = style.VerticalScrollKnob;

			ApplyWidgetStyle(style);
		}

		/// <summary>
		/// Measures the size required for the scroll viewer and its content.
		/// </summary>
		/// <param name="availableSize">The available size for measurement.</param>
		/// <returns>The measured size including scrollbars if needed.</returns>
		protected override Point InternalMeasure(Point availableSize)
		{
			if (Content == null)
			{
				return Mathematics.PointZero;
			}

			var measureSize = Content.Measure(availableSize);

			var horizontalScrollbarVisible = ShowHorizontalScrollBar && measureSize.X > availableSize.X;
			var verticalScrollbarVisible = ShowVerticalScrollBar && measureSize.Y > availableSize.Y;
			if (horizontalScrollbarVisible || verticalScrollbarVisible)
			{
				if (horizontalScrollbarVisible)
				{
					measureSize.Y += HorizontalScrollbarHeight;
				}

				if (verticalScrollbarVisible)
				{
					measureSize.X += VerticalScrollbarWidth;
				}
			}

			return measureSize;
		}

		/// <summary>
		/// Arranges the scroll viewer and its content, including scrollbars.
		/// </summary>
		protected override void InternalArrange()
		{
			// Exit if there's no content to arrange
			if (Content == null)
			{
				return;
			}

			// Get the available space and measure content without scrollbar space constraints
			var bounds = ActualBounds;
			var availableSize = bounds.Size();
			var oldMeasureSize = Content.Measure(availableSize);

			// Determine if scrolling is needed in either direction by comparing content size to available space
			_horizontalScrollingOn = oldMeasureSize.X > bounds.Width;
			_verticalScrollingOn = oldMeasureSize.Y > bounds.Height;

			// If any scrolling is required, recalculate layout accounting for scrollbar dimensions
			if (_horizontalScrollingOn || _verticalScrollingOn)
			{
				var vsWidth = VerticalScrollbarWidth;
				var hsHeight = HorizontalScrollbarHeight;

				// Reduce available vertical space if horizontal scrollbar will be shown
				if (_horizontalScrollingOn && ShowHorizontalScrollBar)
				{
					availableSize.Y -= hsHeight;

					if (availableSize.Y < 0)
					{
						availableSize.Y = 0;
					}
				}

				// Reduce available horizontal space if vertical scrollbar will be shown
				if (_verticalScrollingOn && ShowVerticalScrollBar)
				{
					availableSize.X -= vsWidth;

					if (availableSize.X < 0)
					{
						availableSize.X = 0;
					}
				}

				// Re-measure content with scrollbar space subtracted to get true content dimensions
				var measureSize = Content.Measure(availableSize);
				// Available width for horizontal scrollbar (reduced if vertical scrollbar is shown)
				var bw = bounds.Width - (_verticalScrollingOn && ShowVerticalScrollBar ? vsWidth : 0);

				// Position horizontal scrollbar frame at the bottom of the bounds
				_horizontalScrollbarFrame = new Rectangle(bounds.Left,
					bounds.Bottom - hsHeight,
					bw,
					hsHeight);

				// Calculate horizontal scrollbar thumb width to proportionally represent scrollable content
				// Thumb size = (visible width / content width) * scrollbar width, with minimum knob size
				var mw = measureSize.X;
				if (mw == 0)
				{
					mw = 1;
				}

				_horizontalScrollbarThumb = new Rectangle(bounds.Left,
					bounds.Bottom - hsHeight,
					Math.Max(HorizontalScrollKnob.Size.X, bw * bw / mw),
					HorizontalScrollKnob.Size.Y);

				// Available height for vertical scrollbar (reduced if horizontal scrollbar is shown)
				var bh = bounds.Height - (_horizontalScrollingOn ? hsHeight : 0);

				// Position vertical scrollbar frame on the right edge of the bounds
				_verticalScrollbarFrame = new Rectangle(
					bounds.Left + bounds.Width - vsWidth,
					bounds.Top,
					vsWidth,
					bh);

				// Calculate vertical scrollbar thumb height to proportionally represent scrollable content
				// Thumb size = (visible height / content height) * scrollbar height, with minimum knob size
				var mh = measureSize.Y;
				if (mh == 0)
				{
					mh = 1;
				}

				_verticalScrollbarThumb = new Rectangle(
					bounds.Left + bounds.Width - vsWidth,
					bounds.Top,
					VerticalScrollKnob.Size.X,
					Math.Max(VerticalScrollKnob.Size.Y, bh * bh / mh));

				// Calculate maximum travel distance for scrollbar thumbs (range where thumb can slide)
				_thumbMaximumX = bw - _horizontalScrollbarThumb.Width;
				_thumbMaximumY = bh - _verticalScrollbarThumb.Height;

				// Prevent division by zero when mapping scroll position to thumb position
				if (_thumbMaximumX == 0)
				{
					_thumbMaximumX = 1;
				}

				if (_thumbMaximumY == 0)
				{
					_thumbMaximumY = 1;
				}

				// Set content bounds to allow scrolling: use measured size if scrollable, otherwise constrain to available space
				if (_horizontalScrollingOn && ShowHorizontalScrollBar)
				{
					bounds.Width = measureSize.X;
				}
				else
				{
					bounds.Width = availableSize.X;
				}

				if (_verticalScrollingOn && ShowVerticalScrollBar)
				{
					bounds.Height = measureSize.Y;
				}
				else
				{
					bounds.Height = availableSize.Y;
				}
			}

			// Arrange the content widget with the calculated bounds
			Content.Arrange(bounds);

			// Clamp scroll position to ensure it doesn't exceed the new maximum scrollable distance
			var scrollPosition = ScrollPosition;
			if (scrollPosition.X > ScrollMaximum.X)
			{
				scrollPosition.X = ScrollMaximum.X;
			}
			if (scrollPosition.Y > ScrollMaximum.Y)
			{
				scrollPosition.Y = ScrollMaximum.Y;
			}
			ScrollPosition = scrollPosition;
		}

		/// <summary>
		/// Resets the scroll position to the top-left corner.
		/// </summary>
		public void ResetScroll()
		{
			ScrollPosition = Mathematics.PointZero;
		}

		private void DesktopTouchMoved(object sender, MyraEventArgs args)
		{
			if (!_startBoundsPos.HasValue || Desktop == null)
				return;

			var touchPosition = Desktop.TouchPosition;

			int delta;
			if (_scrollbarOrientation == Orientation.Horizontal)
			{
				delta = (touchPosition.Value.X - _startBoundsPos.Value) * ScrollMaximum.X / _thumbMaximumX;
				_startBoundsPos = touchPosition.Value.X;
			}
			else
			{
				delta = (touchPosition.Value.Y - _startBoundsPos.Value) * ScrollMaximum.Y / _thumbMaximumY;
				_startBoundsPos = touchPosition.Value.Y;
			}


			MoveThumb(delta);
		}

		private void DesktopTouchUp(object sender, MyraEventArgs args)
		{
			_startBoundsPos = null;
		}

		/// <summary>
		/// Applies internal styling to the scroll viewer based on the stylesheet.
		/// </summary>
		/// <param name="stylesheet">The stylesheet to apply.</param>
		/// <param name="name">The name of the style.</param>
		protected override void InternalSetStyle(Stylesheet stylesheet, string name)
		{
			ApplyScrollViewerStyle(stylesheet.ScrollViewerStyles.SafelyGetStyle(name));
		}

		/// <summary>
		/// Determines whether input at the specified position falls through to elements behind the scroll viewer.
		/// </summary>
		/// <param name="localPos">The position in the scroll viewer's local coordinates.</param>
		/// <returns>True if the input falls through; otherwise false.</returns>
		public override bool InputFallsThrough(Point localPos)
		{
			if (Background != null)
			{
				return false;
			}

			if (_horizontalScrollingOn && _horizontalScrollbarFrame.Contains(localPos) ||
				_verticalScrollingOn && _verticalScrollbarFrame.Contains(localPos))
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Copies the style and properties from another scroll viewer.
		/// </summary>
		/// <param name="w">The source scroll viewer to copy from.</param>
		protected internal override void CopyFrom(Widget w)
		{
			base.CopyFrom(w);

			var scrollViewer = (ScrollViewer)w;

			HorizontalScrollBackground = scrollViewer.HorizontalScrollBackground;
			HorizontalScrollKnob = scrollViewer.HorizontalScrollKnob;
			VerticalScrollBackground = scrollViewer.VerticalScrollBackground;
			VerticalScrollKnob = scrollViewer.VerticalScrollKnob;
			ShowHorizontalScrollBar = scrollViewer.ShowHorizontalScrollBar;
			ShowVerticalScrollBar = scrollViewer.ShowVerticalScrollBar;
		}
	}
}