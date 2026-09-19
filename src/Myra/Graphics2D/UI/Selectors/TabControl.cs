using Myra.Events;
using Myra.Graphics2D.UI.Styles;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Collections;

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// Specifies the position of the tab selector buttons in a tab control.
	/// </summary>
	public enum TabSelectorPosition
	{
		/// <summary>Tab selector buttons are positioned at the top.</summary>
		Top,
		/// <summary>Tab selector buttons are positioned on the right.</summary>
		Right,
		/// <summary>Tab selector buttons are positioned at the bottom.</summary>
		Bottom,
		/// <summary>Tab selector buttons are positioned on the left.</summary>
		Left
	}

	/// <summary>
	/// A tab control widget that displays multiple tab items with selector buttons and swappable content panels.
	/// </summary>
	public class TabControl : Selector<Grid, TabItem>
	{
		private Grid _gridButtons;
		private Panel _panelContent;
		private TabSelectorPosition _selectorPosition;

		/// <summary>
		/// Gets or sets the style applied to the tab control.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public TabControlStyle TabControlStyle { get; set; }

		/// <summary>
		/// Gets or sets the selection mode for tabs.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public override SelectionMode SelectionMode { get => base.SelectionMode; set => base.SelectionMode = value; }

		/// <summary>
		/// Gets or sets the horizontal alignment of the tab control within its parent container.
		/// </summary>
		[DefaultValue(HorizontalAlignment.Left)]
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
		/// Gets or sets the vertical alignment of the tab control within its parent container.
		/// </summary>
		[DefaultValue(VerticalAlignment.Top)]
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
		/// Gets or sets the position of the tab selector buttons (top, bottom, left, or right).
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(TabSelectorPosition.Top)]
		public TabSelectorPosition TabSelectorPosition
		{
			get
			{
				return _selectorPosition;
			}
			set
			{
				if (value == _selectorPosition)
				{
					return;
				}

				_selectorPosition = value;
				UpdateSelectorPosition();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether each tab has a close button.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(false)]
		public bool CloseableTabs { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether content extending beyond the tab control bounds is clipped.
		/// </summary>
		[DefaultValue(true)]
		public override bool ClipToBounds { get => base.ClipToBounds; set => base.ClipToBounds = value; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TabControl"/> class with the specified stylesheet and style.
		/// </summary>
		/// <param name="stylesheet">The stylesheet to use for applying the style.</param>
		/// <param name="styleName">The name of the style to apply to the tab control.</param>
		public TabControl(Stylesheet stylesheet, string styleName = Stylesheet.DefaultStyleName) : base(new Grid())
		{
			HorizontalAlignment = HorizontalAlignment.Left;
			VerticalAlignment = VerticalAlignment.Top;

			_gridButtons = new Grid();
			_panelContent = new Panel();

			_selectorPosition = TabSelectorPosition.Top;
			_gridButtons.DefaultColumnProportion = Proportion.Auto;
			_gridButtons.DefaultRowProportion = Proportion.Auto;

			InternalChild.DefaultColumnProportion = Proportion.Fill;
			InternalChild.DefaultRowProportion = Proportion.Fill;

			InternalChild.Widgets.Add(_gridButtons);
			InternalChild.Widgets.Add(_panelContent);

			UpdateSelectorPosition();

			ClipToBounds = true;

			SetStyle(stylesheet, styleName);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TabControl"/> class.
		/// </summary>
		/// <param name="styleName">The name of the style to apply to the tab control.</param>
		public TabControl(string styleName = Stylesheet.DefaultStyleName) : this(Stylesheet.Current, styleName)
		{
		}

		private void ItemOnChanged(object sender, MyraEventArgs eventArgs)
		{
			var item = (TabItem)sender;

			var label = item.LabelWidget;
			label.Text = item.Text;
			label.TextColor = item.Color ?? TabControlStyle.TabItemStyle.LabelStyle.TextColor;

			if (SelectedItem == item)
			{
				UpdateContent();
			}

			InvalidateMeasure();
		}

		private void UpdateSelectorPosition()
		{
			switch (_selectorPosition)
			{
				case TabSelectorPosition.Top:
					Grid.SetColumn(_gridButtons, 0);
					Grid.SetRow(_gridButtons, 0);

					Grid.SetColumn(_panelContent, 0);
					Grid.SetRow(_panelContent, 1);

					InternalChild.ColumnsProportions.Clear();
					InternalChild.RowsProportions.Clear();
					InternalChild.RowsProportions.Add(Proportion.Auto);
					InternalChild.RowsProportions.Add(Proportion.Fill);
					break;

				case TabSelectorPosition.Right:
					Grid.SetColumn(_gridButtons, 1);
					Grid.SetRow(_gridButtons, 0);

					Grid.SetColumn(_panelContent, 0);
					Grid.SetRow(_panelContent, 0);

					InternalChild.ColumnsProportions.Clear();
					InternalChild.ColumnsProportions.Add(Proportion.Fill);
					InternalChild.ColumnsProportions.Add(Proportion.Auto);
					InternalChild.RowsProportions.Clear();
					break;

				case TabSelectorPosition.Bottom:
					Grid.SetColumn(_gridButtons, 0);
					Grid.SetRow(_gridButtons, 1);

					Grid.SetColumn(_panelContent, 0);
					Grid.SetRow(_panelContent, 0);


					InternalChild.ColumnsProportions.Clear();
					InternalChild.RowsProportions.Clear();
					InternalChild.RowsProportions.Add(Proportion.Fill);
					InternalChild.RowsProportions.Add(Proportion.Auto);
					break;

				case TabSelectorPosition.Left:
					Grid.SetColumn(_gridButtons, 0);
					Grid.SetRow(_gridButtons, 0);

					Grid.SetColumn(_panelContent, 1);
					Grid.SetRow(_panelContent, 0);

					InternalChild.ColumnsProportions.Clear();
					InternalChild.ColumnsProportions.Add(Proportion.Auto);
					InternalChild.ColumnsProportions.Add(Proportion.Fill);
					InternalChild.RowsProportions.Clear();
					break;
			}

			UpdateButtonsGrid();
		}

		private void UpdateButtonsGrid()
		{
			bool tabSelectorIsLeftOrRight = TabSelectorPosition == TabSelectorPosition.Left ||
											TabSelectorPosition == TabSelectorPosition.Right;
			for (var i = 0; i < _gridButtons.Widgets.Count; ++i)
			{
				var widget = _gridButtons.Widgets[i];
				if (tabSelectorIsLeftOrRight)
				{
					Grid.SetColumn(widget, 0);
					Grid.SetRow(widget, i);
				}
				else
				{
					Grid.SetColumn(widget, i);
					Grid.SetRow(widget, 0);
				}
			}
		}

		/// <summary>
		/// Inserts a tab item at the specified index in the tab control.
		/// </summary>
		/// <param name="item">The tab item to insert.</param>
		/// <param name="index">The zero-based index where the item should be inserted.</param>
		protected override void InsertItem(TabItem item, int index)
		{
			item.Changed += ItemOnChanged;

			var image = new Image
			{
				Renderable = item.Image
			};

			var label = new Label(null)
			{
				Text = item.Text,
				TextColor = item.Color ?? TabControlStyle.TabItemStyle.LabelStyle.TextColor,
			};

			label.ApplyLabelStyle(TabControlStyle.TabItemStyle.LabelStyle);

			var panel = new HorizontalStackPanel
			{
				Spacing = item.ImageTextSpacing,
				VerticalAlignment = VerticalAlignment.Stretch
			};

			panel.Widgets.Add(image);
			panel.Widgets.Add(label);

			var button = new ListViewButton
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				Height = item.Height.HasValue ? Dimension.Pixel(item.Height.Value) : Dimension.Auto,
				Content = panel,
				ButtonsContainer = _gridButtons
			};

			button.ApplyButtonStyle(TabControlStyle.TabItemStyle);

			button.Click += ButtonOnClick;

			item.Button = button;

			if (!CloseableTabs)
			{
				button.Tag = item;
				_gridButtons.Widgets.Insert(index, button);
			}
			else
			{
				var topItemPanel = new HorizontalStackPanel();
				topItemPanel.Tag = item;

				topItemPanel.Widgets.Add(button);
				StackPanel.SetProportionType(button, ProportionType.Fill);

				var closeButton = new Button
				{
					Content = new Image(),
					HorizontalAlignment = HorizontalAlignment.Right
				};

				closeButton.Click += (s, e) => Items.Remove(item);

				var style = TabControlStyle;
				if (style.CloseButtonStyle != null)
				{
					closeButton.ApplyButtonStyle(style.CloseButtonStyle);
					if (style.CloseButtonStyle.ImageStyle != null)
					{
						var closeImage = (Image)closeButton.Content;
						closeImage.ApplyImageStyle(style.CloseButtonStyle.ImageStyle);
					}
				}

				topItemPanel.Widgets.Add(closeButton);
				_gridButtons.Widgets.Insert(index, topItemPanel);
			}

			UpdateButtonsGrid();

			if (Items.Count == 1)
			{
				// Select first item
				SelectedItem = item;
			}
		}

		private int GetButtonIndex(ListViewButton button)
		{
			var index = -1;
			for (var i = 0; i < _gridButtons.Widgets.Count; ++i)
			{
				var widget = _gridButtons.Widgets[i];
				if (widget == button || widget.FindChild<ListViewButton>() == button)
				{
					index = i;
					break;
				}
			}

			return index;
		}

		/// <summary>
		/// Removes the specified tab item from the tab control.
		/// </summary>
		/// <param name="item">The tab item to remove.</param>
		protected override void RemoveItem(TabItem item)
		{
			item.Changed -= ItemOnChanged;

			var index = GetButtonIndex(item.Button);
			if (index < 0)
			{
				return;
			}

			_gridButtons.Widgets.RemoveAt(index);
			if (SelectedItem == item)
			{
				SelectedItem = null;
			}

			UpdateButtonsGrid();
		}

		private void UpdateContent()
		{
			_panelContent.Widgets.Clear();
			if (SelectedItem != null && SelectedItem.Content != null)
			{
				_panelContent.Widgets.Add(SelectedItem.Content);
			}
		}

		/// <summary>
		/// Called when the selected tab item changes. Updates the displayed content.
		/// </summary>
		protected override void OnSelectedItemChanged()
		{
			base.OnSelectedItemChanged();
			UpdateContent();
		}

		/// <summary>
		/// Clears all tab items from the tab control.
		/// </summary>
		protected override void Reset()
		{
			while (_gridButtons.Widgets.Count > 0)
			{
				RemoveItem((TabItem)_gridButtons.Widgets[0].Tag);
			}
		}

		private void ButtonOnClick(object sender, MyraEventArgs eventArgs)
		{
			var button = (ListViewButton)sender;
			var index = GetButtonIndex(button);
			if (index < 0)
			{
				return;
			}

			SelectedIndex = index;
		}

		internal override IDictionary GetStylesDictionary(Stylesheet stylesheet) => stylesheet.TabControlStyles;

		/// <summary>
		/// Applies the specified widget style to this tab control.
		/// </summary>
		/// <param name="style">The widget style to apply.</param>
		protected override void ApplyStyle(WidgetStyle style)
		{
			base.ApplyStyle(style);

			var tabControlStyle = (TabControlStyle)style;
			TabControlStyle = new TabControlStyle(tabControlStyle);

			TabSelectorPosition = tabControlStyle.TabSelectorPosition;
			InternalChild.RowSpacing = tabControlStyle.HeaderSpacing;
			_gridButtons.ColumnSpacing = tabControlStyle.ButtonSpacing;

			_panelContent.ApplyWidgetStyle(tabControlStyle.ContentStyle);

			foreach (var item in Items)
			{
				item.Button.ApplyButtonStyle(tabControlStyle.TabItemStyle);

				var label = item.LabelWidget;
				label.ApplyLabelStyle(tabControlStyle.TabItemStyle.LabelStyle);
			}
		}

		/// <summary>
		/// Copies the tab control properties and items from another tab control.
		/// </summary>
		/// <param name="w">The source tab control to copy from.</param>
		protected internal override void CopyFrom(Widget w)
		{
			base.CopyFrom(w);

			var tabControl = (TabControl)w;

			TabControlStyle = tabControl.TabControlStyle;
			TabSelectorPosition = tabControl.TabSelectorPosition;

			foreach (var item in tabControl.Items)
			{
				Items.Add(item.Clone());
			}
		}
	}
}
