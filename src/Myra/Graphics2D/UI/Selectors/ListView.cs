using System;
using System.ComponentModel;
using System.Xml.Serialization;
using Myra.Graphics2D.UI.Styles;
using Myra.Attributes;
using System.Collections;
using System.Collections.Generic;
using Myra.Utility;
using System.Reflection;
using Myra.Events;


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
	/// A list view widget that displays a scrollable collection of items and supports single or multiple selection.
	/// </summary>
	public class ListView : Widget, IContainer
	{
		private class WidgetsEnumerator : IEnumerator<Widget>, IEnumerator
		{
			private readonly ListView _listView;
			private int _index = -1;

			public Widget Current => _listView.GetChildByIndex(_index);

			object IEnumerator.Current => _listView.GetChildByIndex(_index);

			public WidgetsEnumerator(ListView listView)
			{
				_listView = listView;
			}

			public void Dispose()
			{
			}

			public bool MoveNext()
			{
				++_index;

				return _index < _listView.ChildrenCount;
			}

			public void Reset()
			{
				_index = -1;
			}
		}

		private class WidgetsCollection : IList<Widget>, IList
		{
			private readonly ListView _listView;
			private Object _syncRoot;

			public object this[int index]
			{
				get => _listView.GetChildByIndex(index);
				set => _listView.SetChildByIndex(index, (Widget)value);
			}

			public bool IsFixedSize => false;

			public bool IsReadOnly => false;

			public int Count => Container.Widgets.Count;

			public bool IsSynchronized => false;

			// Synchronization root for this object.
			public virtual Object SyncRoot
			{
				get
				{
					if (_syncRoot == null)
					{
						System.Threading.Interlocked.CompareExchange(ref _syncRoot, new Object(), null);
					}
					return _syncRoot;
				}
			}

			Widget IList<Widget>.this[int index]
			{
				get => _listView.GetChildByIndex(index);
				set => _listView.SetChildByIndex(index, value);
			}

			private VerticalStackPanel Container => _listView._box;


			public WidgetsCollection(ListView listView)
			{
				_listView = listView;
			}

			public int Add(object value)
			{
				((IList<Widget>)this).Add((Widget)value);
				return _listView.ChildrenCount - 1;
			}

			public void Clear()
			{
				Container.Widgets.Clear();
			}

			public bool Contains(object value) => Find((Widget)value) != null;

			public void CopyTo(Array array, int index)
			{
				throw new NotImplementedException();
			}

			public IEnumerator GetEnumerator() => new WidgetsEnumerator(_listView);

			public int IndexOf(object value)
			{
				var widget = Find((Widget)value);
				if (widget == null)
				{
					return -1;
				}

				return Container.Widgets.IndexOf(widget);
			}

			public void Insert(int index, object value)
			{
				((IList<Widget>)this).Insert(index, (Widget)value);
			}

			public void Remove(object value)
			{
				((IList<Widget>)this).Remove((Widget)value);
			}

			public void RemoveAt(int index)
			{
				var isSelected = _listView.SelectedItem == Unwrap(Container.Widgets[index]);
				Container.Widgets.RemoveAt(index);

				if (isSelected)
				{
					_listView.SelectedItem = null;
				}
			}

			public int IndexOf(Widget item)
			{
				var widget = Find(item);
				if (widget == null)
				{
					return -1;
				}

				return Container.Widgets.IndexOf(widget);
			}

			public void Insert(int index, Widget item)
			{
				var button = Wrap(item);
				Container.Widgets.Insert(index, button);
			}

			public void Add(Widget item)
			{
				var button = Wrap(item);
				Container.Widgets.Add(button);
			}

			public bool Contains(Widget item) => Find(item) != null;

			public void CopyTo(Widget[] array, int arrayIndex)
			{
				throw new NotImplementedException();
			}

			public bool Remove(Widget item)
			{
				var widget = Find(item);
				if (widget == null)
				{
					return false;
				}

				var isSelected = _listView.SelectedItem == Unwrap(widget);
				Container.Widgets.Remove(widget);

				if (isSelected)
				{
					_listView.SelectedItem = null;
				}

				return true;
			}

			IEnumerator<Widget> IEnumerable<Widget>.GetEnumerator() => new WidgetsEnumerator(_listView);

			public Widget Wrap(Widget w)
			{
				if (w is SeparatorWidget)
				{
					return w;
				}

				var button = new ListViewButton()
				{
					Content = w,
					HorizontalAlignment = HorizontalAlignment.Stretch
				};

				button.Click += _listView.ButtonOnClick;

				button.ApplyButtonStyle(_listView.ListBoxStyle.ListItemStyle);

				return button;
			}

			public Widget Unwrap(Widget w)
			{
				var asButton = w as ListViewButton;
				if (asButton != null)
				{
					return asButton.Content;
				}

				return w;
			}

			private Widget Find(Widget item)
			{
				foreach (var w in Container.Widgets)
				{
					if (item == w)
					{
						return w;
					}

					var asButton = w as ListViewButton;
					if (asButton != null && asButton.Content == item)
					{
						return w;
					}
				}

				return null;
			}
		}

		private readonly ScrollViewer _scrollViewer;
		private readonly VerticalStackPanel _box;
		private readonly WidgetsCollection _widgets;
		private Widget _selectedItem;

		internal ComboView _parentCombo;

		/// <summary>
		/// Gets or sets the style applied to the list view.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public ListBoxStyle ListBoxStyle { get; set; }

		/// <summary>
		/// Gets or sets whether items can be selected individually or in multiple selections.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(SelectionMode.Single)]
		public SelectionMode SelectionMode { get; set; }

		/// <summary>
		/// Gets the scroll viewer that manages scrolling for the list view items.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public ScrollViewer ScrollViewer => _scrollViewer;

		/// <summary>
		/// Gets the collection of items in the list view.
		/// </summary>
		[Content]
		[Browsable(false)]
		public IList<Widget> Widgets => _widgets;

		/// <summary>
		/// Gets or sets the zero-based index of the currently selected item, or null if no item is selected.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public int? SelectedIndex
		{
			get
			{
				if (_selectedItem == null)
				{
					return null;
				}

				var result = Widgets.IndexOf(_selectedItem);
				if (result == -1)
				{
					return null;
				}

				return result;
			}

			set
			{
				if (value == null || value.Value < 0 || value.Value >= Widgets.Count)
				{
					SelectedItem = null;
					return;
				}

				SelectedItem = Widgets[value.Value];
			}
		}

		/// <summary>
		/// Gets or sets the currently selected item widget, or null if no item is selected.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Widget SelectedItem
		{
			get
			{
				return _selectedItem;
			}

			set
			{
				if (value == _selectedItem)
				{
					return;
				}

				_selectedItem = value;

				if (_selectedItem != null)
				{
					var asButton = _selectedItem.Parent as ListViewButton;
					if (asButton != null)
					{
						asButton.IsPressed = true;
					}
				}

				SelectedIndexChanged.Invoke(this, InputEventType.SelectedIndexChanged);
				OnSelectedItemChanged();
			}
		}

		private int ChildrenCount => _box.Children.Count;

		/// <summary>
		/// Occurs when the selected item changes.
		/// </summary>
		public event MyraEventHandler SelectedIndexChanged;

		/// <summary>
		/// Initializes a new instance of the <see cref="ListView"/> class.
		/// </summary>
		/// <param name="styleName">The name of the style to apply to the list view.</param>
		public ListView(string styleName = Stylesheet.DefaultStyleName)
		{
			_scrollViewer = new ScrollViewer();
			ChildrenLayout = new SingleItemLayout<ScrollViewer>(this)
			{
				Child = _scrollViewer
			};

			AcceptsKeyboardFocus = true;

			_box = new VerticalStackPanel();
			_scrollViewer.Content = _box;

			_widgets = new WidgetsCollection(this);

			SetStyle(styleName);
		}

		private void ButtonOnClick(object sender, MyraEventArgs eventArgs)
		{
			var button = (ListViewButton)sender;
			if (!button.IsPressed)
			{
				return;
			}

			if (SelectionMode == SelectionMode.Single)
			{
				SelectedItem = button.Content;
			}

			ComboHideDropdown();
		}

		private void ComboHideDropdown()
		{
			if (Desktop != null && Desktop.ContextMenu == this)
			{
				Desktop.HideContextMenu();
			}
		}

		/// <summary>
		/// Raises the selected item changed event and updates the parent combo box if present.
		/// </summary>
		protected void OnSelectedItemChanged()
		{
			if (_parentCombo != null)
			{
				_parentCombo.UpdateSelectedItem();
			}
		}

		/// <summary>
		/// Handles keyboard input for navigation (Up, Down, Enter keys).
		/// </summary>
		/// <param name="k">The key being pressed.</param>
		public override void OnKeyDown(Keys k)
		{
			base.OnKeyDown(k);

			switch (k)
			{
				case Keys.Up:
					{
						int selectedIndex;
						if (SelectedIndex == null)
						{
							selectedIndex = Widgets.Count - 1;
						}
						else
						{
							selectedIndex = SelectedIndex.Value - 1;
						}

						while (selectedIndex > 0 && _box.Widgets[selectedIndex] is SeparatorWidget)
						{
							--selectedIndex;
						}

						if (selectedIndex >= 0)
						{
							SelectedIndex = selectedIndex;
							UpdateScrolling();
						}
					}

					break;
				case Keys.Down:
					{
						int selectedIndex;
						if (SelectedIndex == null)
						{
							selectedIndex = 0;
						}
						else
						{
							selectedIndex = SelectedIndex.Value + 1;
						}

						while (selectedIndex < ChildrenCount && _box.Widgets[selectedIndex] is SeparatorWidget)
						{
							++selectedIndex;
						}

						if (selectedIndex < ChildrenCount)
						{
							SelectedIndex = selectedIndex;
							UpdateScrolling();
						}
					}

					break;
				case Keys.Enter:
					ComboHideDropdown();
					break;
			}
		}

		private void UpdateScrolling()
		{
			if (SelectedItem == null)
			{
				return;
			}

			_scrollViewer.UpdateArrange();

			// Determine item position within ListBox
			var widget = SelectedItem;
			var p = _box.ToLocal(widget.ToGlobal(widget.Bounds.Location));

			var lineHeight = ListBoxStyle.ListItemStyle.LabelStyle.Font.LineHeight;

			var sp = _scrollViewer.ScrollPosition;

			var sz = new Point(_scrollViewer.Bounds.Width, _scrollViewer.Bounds.Height);
			if (p.Y < sp.Y)
			{
				sp.Y = p.Y;
			}
			else if (p.Y + lineHeight > sp.Y + sz.Y)
			{
				sp.Y = p.Y + lineHeight - sz.Y;
			}

			_scrollViewer.ScrollPosition = sp;
		}

		/// <summary>
		/// Handles mouse wheel scrolling for the list view.
		/// </summary>
		/// <param name="delta">The mouse wheel delta value.</param>
		public override void OnMouseWheel(float delta)
		{
			base.OnMouseWheel(delta);

			_scrollViewer.OnMouseWheel(delta);
		}

		/// <summary>
		/// Applies the specified style to the list view and its items.
		/// </summary>
		/// <param name="style">The style to apply.</param>
		public void ApplyListBoxStyle(ListBoxStyle style)
		{
			ApplyWidgetStyle(style);

			ListBoxStyle = style;

			foreach (var item in Widgets)
			{
				var asButton = item.Parent as ListViewButton;
				if (asButton != null)
				{
					asButton.ApplyButtonStyle(style.ListItemStyle);
				}
			}
		}

		/// <summary>
		/// Applies a named list view style from the stylesheet to the list view.
		/// </summary>
		/// <param name="stylesheet">The stylesheet containing the style.</param>
		/// <param name="name">The name of the list view style to apply.</param>
		protected override void InternalSetStyle(Stylesheet stylesheet, string name)
		{
			ApplyListBoxStyle(stylesheet.ListBoxStyles.SafelyGetStyle(name));
		}

		private Widget GetChildByIndex(int index)
		{
			var w = _widgets.Unwrap(_box.Children[index]);
			return w;
		}

		private void SetChildByIndex(int index, Widget widget)
		{
			if (widget is SeparatorWidget)
			{
				// Separators don't need to be wrapped inside buttons
				_box.Children[index] = widget;
				return;
			}

			// Other widgets do
			var w = _box.Children[index];
			var asButton = w as ListViewButton;
			if (asButton != null)
			{
				asButton.Content = widget;
			}
			else
			{
				_box.Children[index] = _widgets.Wrap(widget);
			}
		}

		/// <summary>
		/// Copies the style and items from another list view.
		/// </summary>
		/// <param name="w">The source list view to copy from.</param>
		protected internal override void CopyFrom(Widget w)
		{
			base.CopyFrom(w);

			var listView = (ListView)w;
			ListBoxStyle = listView.ListBoxStyle;
			SelectionMode = listView.SelectionMode;

			foreach (var child in listView.Widgets)
			{
				Widgets.Add(child.Clone());
			}
		}
	}
}