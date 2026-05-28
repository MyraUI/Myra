using Myra.Attributes;
using Myra.Events;
using Myra.Utility;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// An abstract base class for selector widgets that manage selection of items from a collection.
	/// </summary>
	/// <typeparam name="WidgetType">The type of widget to display the items.</typeparam>
	/// <typeparam name="ItemType">The type of items that can be selected.</typeparam>
	public abstract class SelectorBase<WidgetType, ItemType> : Widget, ISelectorT<ItemType>
		where WidgetType : Widget
		where ItemType : class, ISelectorItem
	{
		private readonly SingleItemLayout<WidgetType> _layout;

		/// <summary>
		/// Gets or sets the selection mode (single or multiple items).
		/// </summary>
		[DefaultValue(SelectionMode.Single)]
		public abstract SelectionMode SelectionMode { get; set; }

		/// <summary>
		/// Gets the collection of items available for selection.
		/// </summary>
		[Browsable(false)]
		[Content]
		public abstract ObservableCollection<ItemType> Items { get; }

		/// <summary>
		/// Gets or sets the index of the currently selected item, or null if no item is selected.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public abstract int? SelectedIndex { get; set; }

		/// <summary>
		/// Gets or sets the currently selected item, or null if no item is selected.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public abstract ItemType SelectedItem { get; set; }

		/// <summary>
		/// Gets the internal child widget that displays the items.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		protected WidgetType InternalChild => _layout.Child;

		/// <summary>
		/// Occurs when the selected item or selected index changes.
		/// </summary>
		public abstract event MyraEventHandler SelectedIndexChanged;

		/// <summary>
		/// Initializes a new instance of the <see cref="SelectorBase{WidgetType, ItemType}"/> class.
		/// </summary>
		/// <param name="widget">The widget to display and manage the items.</param>
		protected SelectorBase(WidgetType widget)
		{
			_layout = new SingleItemLayout<WidgetType>(this)
			{
				Child = widget
			};
			ChildrenLayout = _layout;
		}
	}

	/// <summary>
	/// An abstract implementation of a selector widget that manages a collection of selectable items.
	/// </summary>
	/// <typeparam name="WidgetType">The type of widget to display the items.</typeparam>
	/// <typeparam name="ItemType">The type of items that can be selected.</typeparam>
	public abstract class Selector<WidgetType, ItemType> : SelectorBase<WidgetType, ItemType>
		where WidgetType : Widget
		where ItemType : class, ISelectorItem
	{
		private ItemType _selectedItem;

		/// <summary>
		/// Gets or sets the selection mode (single or multiple items).
		/// </summary>
		public override SelectionMode SelectionMode { get; set; }

		/// <summary>
		/// Gets the collection of selectable items.
		/// </summary>
		public override ObservableCollection<ItemType> Items { get; } = new ObservableCollection<ItemType>();

		/// <summary>
		/// Gets or sets the zero-based index of the currently selected item, or null if no item is selected.
		/// </summary>
		public override int? SelectedIndex
		{
			get
			{
				if (_selectedItem == null)
				{
					return null;
				}

				return Items.IndexOf(_selectedItem);
			}

			set
			{
				if (value == null || value.Value < 0 || value.Value >= Items.Count)
				{
					SelectedItem = default(ItemType);
					return;
				}

				SelectedItem = Items[value.Value];
			}
		}

		/// <summary>
		/// Gets or sets the currently selected item, or null if no item is selected.
		/// </summary>
		public override ItemType SelectedItem
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

				if (SelectionMode == SelectionMode.Single && _selectedItem != null)
				{
					_selectedItem.IsSelected = false;
				}

				_selectedItem = value;

				if (_selectedItem != null)
				{
					_selectedItem.IsSelected = true;
				}

				FireSelectedIndexChanged();
				OnSelectedItemChanged();
			}
		}

		/// <summary>
		/// Occurs when the selected item or selected index changes.
		/// </summary>
		public override event MyraEventHandler SelectedIndexChanged;

		/// <summary>
		/// Occurs when the items collection is modified.
		/// </summary>
		public event MyraEventHandler ItemsCollectionChanged;

		/// <summary>
		/// Initializes a new instance of the <see cref="Selector{WidgetType, ItemType}"/> class.
		/// </summary>
		/// <param name="widget">The widget to display and manage the items.</param>
		protected Selector(WidgetType widget) : base(widget)
		{
			widget.HorizontalAlignment = HorizontalAlignment.Stretch;
			widget.VerticalAlignment = VerticalAlignment.Stretch;

			HorizontalAlignment = HorizontalAlignment.Left;
			VerticalAlignment = VerticalAlignment.Top;

			Items.CollectionChanged += ItemsOnCollectionChanged;
		}

		private void ItemsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
		{
			switch (args.Action)
			{
				case NotifyCollectionChangedAction.Add:
					{
						var index = args.NewStartingIndex;
						foreach (ItemType item in args.NewItems)
						{
							InsertItem(item, index);
							++index;
						}
						break;
					}

				case NotifyCollectionChangedAction.Remove:
					{
						foreach (ItemType item in args.OldItems)
						{
							RemoveItem(item);
						}
						break;
					}

				case NotifyCollectionChangedAction.Reset:
					{
						Reset();
						break;
					}
			}

			OnItemCollectionChanged();
			InvalidateMeasure();
		}

		/// <summary>
		/// Called when the items collection changes. Raises the ItemsCollectionChanged event.
		/// </summary>
		protected virtual void OnItemCollectionChanged()
		{
			ItemsCollectionChanged.Invoke(this, InputEventType.ValueChanged);
		}

		/// <summary>
		/// Called when the selected item changes. Derived classes can override to perform custom behavior.
		/// </summary>
		protected virtual void OnSelectedItemChanged()
		{
		}

		private void FireSelectedIndexChanged()
		{
			SelectedIndexChanged.Invoke(this, InputEventType.ValueChanged);
		}

		/// <summary>
		/// Clears all items from the selector. Derived classes must implement this method.
		/// </summary>
		protected abstract void Reset();
		/// <summary>
		/// Inserts an item at the specified index. Derived classes must implement this method.
		/// </summary>
		/// <param name="item">The item to insert.</param>
		/// <param name="index">The zero-based index where the item should be inserted.</param>
		protected abstract void InsertItem(ItemType item, int index);
		/// <summary>
		/// Removes the specified item from the selector. Derived classes must implement this method.
		/// </summary>
		/// <param name="item">The item to remove.</param>
		protected abstract void RemoveItem(ItemType item);
	}
}