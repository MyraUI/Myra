using System;
using System.ComponentModel;
using Myra.Graphics2D.UI.Styles;
using System.Xml.Serialization;
using Myra.Events;
using Myra.Attributes;
using System.Collections.Generic;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#elif STRIDE
using Stride.Core.Mathematics;
using Stride.Input;
#else
using System.Drawing;
using Myra.Platform;
#endif

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// A combo view widget that displays a list of widget items in a dropdown menu and supports selection.
	/// </summary>
	public class ComboView : Widget, IContainer
	{
		private readonly ToggleButton _button;
		private readonly ListView _listView = new ListView(null);

		/// <summary>
		/// Gets or sets the maximum height of the dropdown list in pixels.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(300)]
		public int? DropdownMaximumHeight
		{
			get
			{
				return _listView.MaxHeight;
			}

			set
			{
				_listView.MaxHeight = value;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the dropdown list is currently visible.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public bool IsExpanded => _button.IsPressed;

		/// <summary>
		/// Gets or sets the desktop that manages this combo view and its dropdown menu.
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
					Desktop.ContextMenuClosed -= DesktopOnContextMenuClosed;
				}

				base.Desktop = value;

				if (Desktop != null)
				{
					Desktop.ContextMenuClosed += DesktopOnContextMenuClosed;
				}
			}
		}

		/// <summary>
		/// Gets the list view that displays the dropdown items.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public ListView ListView => _listView;

		/// <summary>
		/// Gets the collection of items in the combo view.
		/// </summary>
		[Content]
		[Browsable(false)]
		public IList<Widget> Widgets => _listView.Widgets;

		/// <summary>
		/// Gets or sets the currently selected item.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Widget SelectedItem
		{
			get => _listView.SelectedItem;
			set => _listView.SelectedItem = value;
		}

		/// <summary>
		/// Gets or sets whether items can be selected individually or in multiple selections.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(SelectionMode.Single)]
		public SelectionMode SelectionMode
		{
			get => _listView.SelectionMode;
			set => _listView.SelectionMode = value;
		}

		/// <summary>
		/// Gets or sets the index of the currently selected item, or null if no item is selected.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public int? SelectedIndex
		{
			get => _listView.SelectedIndex;
			set => _listView.SelectedIndex = value;
		}

		/// <summary>
		/// Occurs when the selected item changes.
		/// </summary>
		public event MyraEventHandler SelectedIndexChanged
		{
			add
			{
				_listView.SelectedIndexChanged += value;
			}

			remove
			{
				_listView.SelectedIndexChanged -= value;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ComboView"/> class.
		/// </summary>
		/// <param name="styleName">The name of the style to apply to the combo view.</param>
		public ComboView(string styleName = Stylesheet.DefaultStyleName)
		{
			_button = new ToggleButton(null)
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					Text = string.Empty
				}
			};

			ChildrenLayout = new SingleItemLayout<ToggleButton>(this)
			{
				Child = _button
			};

			AcceptsKeyboardFocus = true;

			_button.PressedChanged += InternalChild_PressedChanged;

			_listView._parentCombo = this;

			if (MyraEnvironment.EventHandlingModel == EventHandlingStrategy.EventBubbling)
				_button.TouchDown += InternalStopPropagation;

			HorizontalAlignment = HorizontalAlignment.Left;
			VerticalAlignment = VerticalAlignment.Top;

			DropdownMaximumHeight = 300;

			SetStyle(styleName);
		}

		private void InternalStopPropagation(object sender, MyraEventArgs e)
		{
			e.StopPropagation();
		}

		private void DesktopOnContextMenuClosed(object sender, GenericEventArgs<Widget> genericEventArgs)
		{
			// Unpress the button only if mouse is outside
			// As if it is inside, then it'll get unpressed naturally
			if (!IsMouseInside)
			{
				_button.IsPressed = false;
			}
		}

		private void InternalChild_PressedChanged(object sender, MyraEventArgs e)
		{
			if (_listView.Widgets.Count == 0)
			{
				return;
			}

			if (_button.IsPressed)
			{
				if (_listView.SelectedIndex == null && Widgets.Count > 0)
				{
					_listView.SelectedIndex = 0;
				}

				_listView.Width = BorderBounds.Width;
				var pos = ToGlobal(new Point(0, Bounds.Height));
				Desktop.ShowContextMenu(_listView, pos);
			}
		}

		internal void UpdateSelectedItem()
		{
			_button.Content = SelectedItem.Clone();
		}

		/// <summary>
		/// Applies the specified style to the combo view and its dropdown list.
		/// </summary>
		/// <param name="style">The style to apply.</param>
		public void ApplyComboViewStyle(ComboBoxStyle style)
		{
			if (style.ListBoxStyle != null)
			{
				var dropdownMaximumHeight = DropdownMaximumHeight;
				_listView.ApplyListBoxStyle(style.ListBoxStyle);
				DropdownMaximumHeight = dropdownMaximumHeight;
			}

			_button.ApplyButtonStyle(style);
		}

		/// <summary>
		/// Measures the size required for the combo view, considering the dropdown list width.
		/// </summary>
		/// <param name="availableSize">The available size for the combo view.</param>
		/// <returns>The measured size needed for the combo view.</returns>
		protected override Point InternalMeasure(Point availableSize)
		{
			// Measure by the longest string
			var result = base.InternalMeasure(availableSize);

			// Temporary remove width, so it wont be used in the measure
			var oldWidth = _listView.Width;
			_listView.Width = null;

			// Make visible, otherwise Measure will return zero
			var wasVisible = _listView.Visible;
			_listView.Visible = true;

			var listResult = _listView.Measure(new Point(10000, 10000));
			if (listResult.X > result.X)
			{
				result.X = listResult.X;
			}

			// Revert ListBox settings
			_listView.Width = oldWidth;
			_listView.Visible = wasVisible;

			// Add some x space
			result.X += 32;

			return result;
		}

		/// <summary>
		/// Arranges the combo view and its dropdown list within the measured bounds.
		/// </summary>
		protected override void InternalArrange()
		{
			base.InternalArrange();

			_listView.Width = BorderBounds.Width;
		}

		/// <summary>
		/// Handles keyboard input and delegates it to the dropdown list.
		/// </summary>
		/// <param name="k">The key being pressed.</param>
		public override void OnKeyDown(Keys k)
		{
			base.OnKeyDown(k);

			_listView.OnKeyDown(k);
		}

		/// <summary>
		/// Applies a named combo view style from the stylesheet to the combo view.
		/// </summary>
		/// <param name="stylesheet">The stylesheet containing the style.</param>
		/// <param name="name">The name of the combo view style to apply.</param>
		protected override void InternalSetStyle(Stylesheet stylesheet, string name)
		{
			ApplyComboViewStyle(stylesheet.ComboBoxStyles.SafelyGetStyle(name));
		}

		/// <summary>
		/// Copies the style and items from another combo view.
		/// </summary>
		/// <param name="w">The source combo view to copy from.</param>
		protected internal override void CopyFrom(Widget w)
		{
			base.CopyFrom(w);

			var comboView = (ComboView)w;
			SelectionMode = comboView.SelectionMode;
			DropdownMaximumHeight = comboView.DropdownMaximumHeight;

			foreach (var child in comboView.Widgets)
			{
				Widgets.Add(child.Clone());
			}
		}
	}
}
