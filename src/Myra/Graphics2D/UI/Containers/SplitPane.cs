using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Myra.Graphics2D.UI.Styles;
using System.Xml.Serialization;
using System.Collections.ObjectModel;
using Myra.Attributes;
using Myra.Events;

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// An abstract base class for split pane containers that divide space between child widgets with moveable splitter handles.
	/// </summary>
	public abstract class SplitPane : Container
	{
		private readonly ObservableCollection<Widget> _widgets = new ObservableCollection<Widget>();
		private readonly GridLayout _layout = new GridLayout();
		private readonly List<Button> _handles = new List<Button>();
		private Button _handleDown;
		private int? _mouseCoord;
		private int _handlesSize;

		/// <summary>
		/// Gets the collection of child widgets in the split pane.
		/// </summary>
		[Content]
		[Browsable(false)]
		public override IList<Widget> Widgets => _widgets;

		/// <summary>
		/// Gets the orientation (horizontal or vertical) of the split pane.
		/// </summary>
		[XmlIgnore]
		[Browsable(false)]
		public abstract Orientation Orientation { get; }

		/// <summary>
		/// Gets or sets the style applied to the splitter handle buttons.
		/// </summary>
		[XmlIgnore]
		[Browsable(false)]
		public SplitPanelButtonStyle HandleStyle { get; private set; }

		/// <summary>
		/// Occurs when the splitter positions change.
		/// </summary>
		public event MyraEventHandler ProportionsChanged;

		/// <summary>
		/// Initializes a new instance of the <see cref="SplitPane"/> class.
		/// </summary>
		/// <param name="styleName">The name of the style to apply to the split pane.</param>
		protected SplitPane(string styleName)
		{
			ChildrenLayout = _layout;
			_widgets.CollectionChanged += WidgetsOnCollectionChanged;

			SetStyle(styleName);
		}

		/// <summary>
		/// Gets the proportion (size ratio) of the widget at the specified index.
		/// </summary>
		/// <param name="widgetIndex">The index of the widget.</param>
		/// <returns>The proportion value, or 0 if the index is out of range.</returns>
		public float GetProportion(int widgetIndex)
		{
			if (widgetIndex < 0 || widgetIndex >= Widgets.Count)
			{
				return 0.0f;
			}

			var result = Orientation == Orientation.Horizontal
				? _layout.ColumnsProportions[widgetIndex * 2].Value
				: _layout.RowsProportions[widgetIndex * 2].Value;

			return result;
		}

		/// <summary>
		/// Handles touch/mouse movement for dragging splitter handles to resize sections.
		/// </summary>
		public override void OnTouchMoved()
		{
			base.OnTouchMoved();

			if (Desktop == null || _mouseCoord == null)
			{
				return;
			}

			var bounds = Bounds;
			if (bounds.Width == 0)
			{
				return;
			}

			var handleIndex = Children.IndexOf(_handleDown);
			Proportion firstProportion, secondProportion;
			float fp;

			var position = ToLocal(Desktop.TouchPosition.Value);
			if (Orientation == Orientation.Horizontal)
			{
				var firstWidth = position.X - _mouseCoord.Value;

				for (var i = 0; i < handleIndex - 1; ++i)
				{
					firstWidth -= _layout.GetColumnWidth(i);
				}

				fp = (float)Widgets.Count * firstWidth / (bounds.Width - _handlesSize);

				firstProportion = _layout.ColumnsProportions[handleIndex - 1];
				secondProportion = _layout.ColumnsProportions[handleIndex + 1];
			}
			else
			{
				var firstHeight = position.Y - _mouseCoord.Value;

				for (var i = 0; i < handleIndex - 1; ++i)
				{
					firstHeight -= _layout.GetRowHeight(i);
				}

				fp = (float)Widgets.Count * firstHeight / (bounds.Height - _handlesSize);

				firstProportion = _layout.RowsProportions[handleIndex - 1];
				secondProportion = _layout.RowsProportions[handleIndex + 1];
			}

			var fp2 = firstProportion.Value + secondProportion.Value - fp;
			if (fp >= 0 && fp2 >= 0)
			{
				firstProportion.Value = fp;
				secondProportion.Value = fp2;
				FireProportionsChanged();
			}

			InvalidateArrange();
		}

		private void FireProportionsChanged()
		{
			var ev = ProportionsChanged;
			if (ev != null)
			{
				ev(this, new MyraEventArgs(InputEventType.ProportionChanged));
			}
		}

		private void HandleOnPressedChanged(object sender, MyraEventArgs args)
		{
			var handle = (Button)sender;

			if (!handle.IsPressed)
			{
				_handleDown = null;
				_mouseCoord = null;

				MyraEnvironment.SetMouseCursorFromWidget = true;
				MyraEnvironment.MouseCursorType = MouseCursor ?? MyraEnvironment.DefaultMouseCursorType;
			}
			else if (Desktop != null)
			{
				_handleDown = (Button)sender;

				var handleGlobalPos = _handleDown.ToGlobal(_handleDown.Bounds.Location);
				_mouseCoord = Orientation == Orientation.Horizontal
					? Desktop.TouchPosition.Value.X - handleGlobalPos.X
					: Desktop.TouchPosition.Value.Y - handleGlobalPos.Y;

				MyraEnvironment.SetMouseCursorFromWidget = false;
				if (Orientation == Orientation.Horizontal)
				{
					MyraEnvironment.MouseCursorType = MouseCursorType.SizeWE;
				}
				else
				{
					MyraEnvironment.MouseCursorType = MouseCursorType.SizeNS;
				}
			}
		}

		private void WidgetsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
		{
			Reset();
		}

		private void GetProportions(int leftWidgetIndex,
			out Proportion leftProportion,
			out Proportion rightProportion,
			out float total)
		{
			var allProps = Orientation == Orientation.Horizontal
				? _layout.ColumnsProportions : _layout.RowsProportions;

			total = 0;
			for (var i = 0; i < allProps.Count; i += 2)
			{
				total += allProps[i].Value;
			}

			var baseIndex = leftWidgetIndex * 2;
			leftProportion = allProps[baseIndex];
			rightProportion = allProps[baseIndex + 2];
		}

		/// <summary>
		/// Gets the position of the splitter between two widgets as a proportion (0.0 to 1.0).
		/// </summary>
		/// <param name="leftWidgetIndex">The index of the widget to the left/top of the splitter.</param>
		/// <returns>The splitter position as a proportion of total space.</returns>
		public float GetSplitterPosition(int leftWidgetIndex)
		{
			float total;
			Proportion leftProportion, rightProportion;
			GetProportions(leftWidgetIndex, out leftProportion, out rightProportion, out total);

			return leftProportion.Value / total;
		}

		/// <summary>
		/// Sets the position of the splitter between two widgets.
		/// </summary>
		/// <param name="leftWidgetIndex">The index of the widget to the left/top of the splitter.</param>
		/// <param name="proportion">The splitter position as a proportion (0.0 to 1.0).</param>
		public void SetSplitterPosition(int leftWidgetIndex, float proportion)
		{
			float total;
			Proportion leftProportion, rightProportion;
			GetProportions(leftWidgetIndex, out leftProportion, out rightProportion, out total);

			var fp = proportion * total;
			var fp2 = leftProportion.Value + rightProportion.Value - fp;
			leftProportion.Value = fp;
			rightProportion.Value = fp2;
		}

		/// <summary>
		/// Reinitializes the split pane layout based on the current widgets and handle style.
		/// </summary>
		public void Reset()
		{
			// Clear all existing layout data to rebuild from scratch
			Children.Clear();
			_handles.Clear();
			_handlesSize = 0;

			_layout.ColumnsProportions.Clear();
			_layout.RowsProportions.Clear();

			var i = 0;

			// Determine splitter handle size: explicit value takes precedence, otherwise derive from background image
			var handleSize = 0;

			if (HandleStyle.HandleSize != null)
			{
				handleSize = HandleStyle.HandleSize.Value;
			}
			else
			{
				var asImage = HandleStyle.Background as IImage;
				if (asImage != null)
				{
					// Use width for horizontal split, height for vertical
					handleSize = Orientation == Orientation.Horizontal
						? asImage.Size.X
						: asImage.Size.Y;
				}
			}

			// Build layout: widgets are interspersed with splitter handles
			// Grid layout alternates: widget (at position 0,2,4...), handle (at position 1,3,5...)
			foreach (var w in Widgets)
			{
				Proportion proportion;

				// Create and position splitter handle between widgets (skip for first widget)
				if (i > 0)
				{
					// Create splitter handle button with drag-to-resize functionality
					var handle = new Button(null)
					{
						ReleaseOnTouchLeft = false
					};

					// Set appropriate mouse cursor and stretch handle to fill available space
					if (Orientation == Orientation.Horizontal)
					{
						handle.MouseCursor = MouseCursorType.SizeWE;
						handle.VerticalAlignment = VerticalAlignment.Stretch;
					}
					else
					{
						handle.MouseCursor = MouseCursorType.SizeNS;
						handle.HorizontalAlignment = HorizontalAlignment.Stretch;
					}

					handle.ApplyButtonStyle(HandleStyle);

					// Listen for mouse/touch drag events on the handle
					handle.PressedChanged += HandleOnPressedChanged;

					// Handles use Auto proportion (take only needed space based on size)
					proportion = new Proportion(ProportionType.Auto);

					if (Orientation == Orientation.Horizontal)
					{
						_handlesSize += handleSize;
						Grid.SetColumn(handle, i * 2 - 1);
						_layout.ColumnsProportions.Add(proportion);
					}
					else
					{
						_handlesSize += handleSize;
						Grid.SetRow(handle, i * 2 - 1);
						_layout.RowsProportions.Add(proportion);
					}

					Children.Add(handle);
					_handles.Add(handle);
				}

				// Create proportion for widget: intermediate widgets use Part (proportional share),
				// last widget uses Fill (takes remaining space) to ensure pane is fully utilized
				proportion = i < Widgets.Count - 1
					? new Proportion(ProportionType.Part, 1.0f)
					: new Proportion(ProportionType.Fill, 1.0f);

				// Add widget to grid at appropriate column/row position
				if (Orientation == Orientation.Horizontal)
				{
					Grid.SetColumn(w, i * 2);
					_layout.ColumnsProportions.Add(proportion);
				}
				else
				{
					Grid.SetRow(w, i * 2);
					_layout.RowsProportions.Add(proportion);
				}

				Children.Add(w);

				++i;
			}

			// Set fixed dimensions for all splitter handles based on orientation
			foreach (var h in _handles)
			{
				if (Orientation == Orientation.Horizontal)
				{
					h.Width = handleSize;
					h.Height = null;  // Let height stretch to fill available space
				}
				else
				{
					h.Width = null;  // Let width stretch to fill available space
					h.Height = handleSize;
				}
			}

			// Notify listeners that the pane layout has been reset
			FireProportionsChanged();
		}

		/// <summary>
		/// Applies the specified style to the split pane and its splitter handles.
		/// </summary>
		/// <param name="style">The style to apply.</param>
		public void ApplySplitPaneStyle(SplitPaneStyle style)
		{
			ApplyWidgetStyle(style);

			HandleStyle = style.HandleStyle;
			Reset();
		}

		/// <summary>
		/// Copies the style and properties from another split pane.
		/// </summary>
		/// <param name="w">The source split pane to copy from.</param>
		protected internal override void CopyFrom(Widget w)
		{
			base.CopyFrom(w);

			var splitPane = (SplitPane)w;
			HandleStyle = splitPane.HandleStyle;
		}
	}
}