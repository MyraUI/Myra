using Myra.Events;
using Myra.Graphics2D.UI.Styles;
using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using Myra.Utility;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;



#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.UI.Data
{
	/// <summary>
	/// A tabular widget that displays data in rows and columns with support for column resizing,
	/// vertical scrolling, row selection, and customizable styling.
	/// </summary>
	public class DataGrid : Widget
	{
		private enum InvalidateLevelData
		{
			None,
			VisualData,
			SourceData
		}

		private class RowData
		{
			public int Index { get; }
			public object Value { get; }
			public object[] GridValues { get; }

			public RowData(int index, object value, object[] gridValues)
			{
				Index = index;
				Value = value;
				GridValues = gridValues;
			}
		}

		private readonly Grid _grid;
		private readonly SingleItemLayout<Grid> _layout;
		private Rectangle _verticalScrollbarFrame, _verticalScrollbarThumb;
		private int _startRow;
		private DataGridColumnBase[] _columns;
		private int? _startBoundsPos;
		private int _thumbMaximumY;
		private int? _indexColumnWidth = 50;
		private int? _resizingColumnIndex = null;
		private int _resizeStartX;
		private int _resizeOriginalWidth;
		private int _resizeNextWidth;
		private IList _data;
		private ListSortDirection _sortDirection;
		private int? _sortColumn;
		private int? _selectedRowIndex;
		private bool _hasHeader = true, _sortableHeaders = true, _hasFilter = true;
		private StringComparison _filtersStringComparison = StringComparison.CurrentCultureIgnoreCase;
		private int? _fillColumnIndex = null;
		private readonly List<int> _visibleRowsIndices = new List<int>();
		private bool _headerDirty = true;
		private RowData[] _sourceData;
		private RowData[] _visualData;
		private readonly List<Widget> _dataWidgets = new List<Widget>();
		private Point? _lastArrangeSize = null;
		private DataGridStyle _style;
		private HorizontalAlignment _headerHorizontalAlignment = HorizontalAlignment.Left;
		private HorizontalAlignment _cellHorizontalAlignment = HorizontalAlignment.Left;
		private HorizontalAlignment? _indexColumnHeaderHorizontalAlignment = null;
		private HorizontalAlignment? _indexColumnCellHorizontalAlignment = null;

		/// <summary>
		/// Gets the number of data rows visible per page, based on the current layout size.
		/// </summary>
		public int RowsPerPage { get; private set; } = 1;

		/// <summary>
		/// Gets the total number of data rows loaded into the grid.
		/// </summary>
		public int TotalRows
		{
			get
			{
				UpdateData();
				if (_visualData == null)
				{
					return 0;
				}

				return _visualData.Length;
			}
		}

		/// <summary>
		/// Gets or sets the zero-based index of the first visible data row, used for vertical scrolling.
		/// </summary>
		public int StartRow
		{
			get => _startRow;

			set
			{
				if (value == _startRow)
				{
					return;
				}

				_startRow = value;
				Invalidate(false);
			}
		}

		/// <summary>
		/// Gets or sets the array of column definitions that define the grid's structure and data binding.
		/// Setting this property triggers a full rebuild of columns and the grid layout.
		/// </summary>
		public DataGridColumnBase[] Columns
		{
			get => _columns;

			set
			{
				if (value == null || value.Length == 0)
				{
					throw new ArgumentNullException(nameof(value));
				}

				for (var i = 0; i < value.Length; ++i)
				{
					var column = value[i];
					if (column == null)
					{
						throw new ArgumentNullException($"Column at index {i} is null.");
					}

					if (string.IsNullOrEmpty(column.Property))
					{
						throw new ArgumentNullException($"Column property must be defined. Index: {i}");
					}

					if (column.Width <= 0)
					{
						throw new ArgumentOutOfRangeException($"Column width must be a positive value. Index: {i}");
					}
				}

				_columns = value;
				Invalidate(true);
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether grid lines are drawn between cells.
		/// </summary>
		[Category("Appearance")]
		public bool ShowGridLines
		{
			get => _grid.ShowGridLines;
			set => _grid.ShowGridLines = value;
		}

		/// <summary>
		/// Gets or sets the color of the grid lines.
		/// </summary>
		[Category("Appearance")]
		public Color GridLinesColor
		{
			get => _grid.GridLinesColor;
			set => _grid.GridLinesColor = value;
		}

		/// <summary>
		/// Gets or sets the spacing in pixels between grid columns.
		/// </summary>
		[Category("Appearance")]
		public int ColumnSpacing
		{
			get => _grid.ColumnSpacing;
			set => _grid.ColumnSpacing = value;
		}

		/// <summary>
		/// Gets or sets the spacing in pixels between grid rows.
		/// </summary>
		[Category("Appearance")]
		public int RowSpacing
		{
			get => _grid.RowSpacing;
			set => _grid.RowSpacing = value;
		}

		/// <summary>
		/// Gets or sets the brush used to draw the background of selected rows.
		/// </summary>
		[Category("Appearance")]
		public IBrush SelectionBackground
		{
			get => _grid.SelectionBackground;
			set => _grid.SelectionBackground = value;
		}

		/// <summary>
		/// Gets or sets the brush used to draw the background of hovered rows.
		/// </summary>
		[Category("Appearance")]
		public IBrush SelectionHoverBackground
		{
			get => _grid.SelectionHoverBackground;
			set => _grid.SelectionHoverBackground = value;
		}

		/// <summary>
		/// Gets or sets the selection mode for the grid (None, Row, Column, or Cell).
		/// </summary>
		[Category("Behavior")]
		public GridSelectionMode GridSelectionMode
		{
			get => _grid.GridSelectionMode;
			set => _grid.GridSelectionMode = value;
		}

		/// <summary>
		/// Gets or sets a value indicating whether the hover index can be cleared when the pointer leaves the grid.
		/// </summary>
		[Category("Behavior")]
		public bool HoverIndexCanBeNull
		{
			get => _grid.HoverIndexCanBeNull;
			set => _grid.HoverIndexCanBeNull = value;

		}

		/// <summary>
		/// Gets or sets a value indicating whether clicking an already-selected row deselects it.
		/// </summary>
		[Category("Behavior")]
		public bool CanSelectNothing
		{
			get => _grid.CanSelectNothing;
			set => _grid.CanSelectNothing = value;
		}

		/// <summary>
		/// Gets or sets the zero-based index of the selected data row, adjusted for the header row.
		/// Returns <c>null</c> when no row is selected.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public int? SelectedRowIndex
		{
			get => _selectedRowIndex;

			set
			{
				_selectedRowIndex = value;

				if (value == null)
				{
					_grid.SelectedRowIndex = null;
				}
				else
				{
					for (var i = 0; i < _visibleRowsIndices.Count; ++i)
					{
						if (value == _visibleRowsIndices[i])
						{
							_grid.SelectedRowIndex = i + RowShift;
							break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Gets the data object associated with the currently selected row, or <c>null</c> if none is selected.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public object SelectedItem
		{
			get
			{
				if (SelectedRowIndex == null || _data == null)
				{
					return null;
				}

				return _data[SelectedRowIndex.Value];
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether columns can be resized by dragging the boundary between header cells.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool ResizableColumns { get; set; } = true;

		/// <summary>
		/// Gets or sets the width in pixels of the hit-test area around a column boundary for initiating a resize drag.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(4)]
		public int ColumnResizeHandleWidth { get; set; } = 4;

		/// <summary>
		/// Gets or sets the minimum width in pixels a column can be resized to.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(20)]
		public int MinColumnWidth { get; set; } = 20;

		/// <summary>
		/// Gets or sets a value indicating whether the header row is displayed.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool HasHeader
		{
			get => _hasHeader;

			set
			{
				if (value == _hasHeader)
				{
					return;
				}

				_hasHeader = value;
				Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the width of the row-number index column. Defaults to <c>50</c>.
		/// Set to <c>null</c> to hide the index column.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(50)]
		public int? IndexColumnWidth
		{
			get => _indexColumnWidth;

			set
			{
				if (value != null && value.Value <= 0)
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				if (value == _indexColumnWidth)
				{
					return;
				}

				_indexColumnWidth = value;
				Invalidate(true);
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the index column header text.
		/// When <c>null</c>, the DataGrid's <see cref="HeaderHorizontalAlignment"/> is used instead.
		/// </summary>
		public HorizontalAlignment? IndexColumnHeaderHorizontalAlignment
		{
			get => _indexColumnHeaderHorizontalAlignment;

			set
			{
				if (value == _indexColumnHeaderHorizontalAlignment)
				{
					return;
				}

				_indexColumnHeaderHorizontalAlignment = value;
				Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the index column cell numbers.
		/// When <c>null</c>, the DataGrid's <see cref="CellHorizontalAlignment"/> is used instead.
		/// </summary>
		public HorizontalAlignment? IndexColumnCellHorizontalAlignment
		{
			get => _indexColumnCellHorizontalAlignment;

			set
			{
				if (value == _indexColumnCellHorizontalAlignment)
				{
					return;
				}

				_indexColumnCellHorizontalAlignment = value;
				Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the number of rows to scroll per mouse wheel tick or touch drag step.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(10)]
		public int ScrollMultiplier { get; set; } = 10;

		/// <summary>
		/// Gets or sets a value indicating whether clicking a header cell sorts the column.
		/// When <c>true</c> (default), header cells are clickable buttons that toggle sorting.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool SortableHeaders
		{
			get => _sortableHeaders;

			set
			{
				if (value == _sortableHeaders)
				{
					return;
				}

				_sortableHeaders = value;
				Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the sort direction applied to <see cref="SortColumn"/>.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public ListSortDirection SortDirection
		{
			get => _sortDirection;

			set
			{
				if (value == _sortDirection)
				{
					return;
				}

				_sortDirection = value;
				Invalidate(true, InvalidateLevelData.VisualData);
			}
		}

		/// <summary>
		/// Gets or sets the zero-based index of the column currently used for sorting, or <c>null</c> when no sort is applied.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public int? SortColumn
		{
			get => _sortColumn;

			set
			{
				if (value == _sortColumn)
				{
					return;
				}

				_sortColumn = value;
				Invalidate(true, InvalidateLevelData.VisualData);
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether filter rows are displayed below the header, allowing per-column text filtering.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool HasFilter
		{
			get => _hasFilter;

			set
			{
				if (value == _hasFilter)
				{
					return;
				}

				_hasFilter = value;
				Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the string comparison mode used when matching filter text against cell values.
		/// Defaults to <see cref="StringComparison.CurrentCultureIgnoreCase"/>.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(StringComparison.CurrentCultureIgnoreCase)]
		public StringComparison FilterStringComparison
		{
			get => _filtersStringComparison;

			set
			{
				if (value == _filtersStringComparison)
				{
					return;
				}

				_filtersStringComparison = value;
				Invalidate(false, InvalidateLevelData.VisualData);
			}
		}

		/// <summary>
		/// Gets or sets the zero-based index of the column that fills the remaining horizontal space,
		/// or <c>null</c> to append a fill column after all data columns.
		/// </summary>
		[Category("Behavior")]
		public int? FillColumnIndex
		{
			get => _fillColumnIndex;

			set
			{
				if (value == _fillColumnIndex)
				{
					return;
				}

				_fillColumnIndex = value;
				Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the collection of data objects displayed as rows.
		/// Property values are resolved by name via reflection when the collection is assigned.
		/// </summary>
		public IList Data
		{
			get => _data;

			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}

				_data = value;
				Invalidate(false, InvalidateLevelData.SourceData);
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the default horizontal alignment of header text. Defaults to <see cref="HorizontalAlignment.Left"/>.
		/// Individual columns can override this via <see cref="DataGridColumnBase.HeaderHorizontalAlignment"/>.
		/// </summary>
		[DefaultValue(HorizontalAlignment.Left)]
		public HorizontalAlignment HeaderHorizontalAlignment
		{
			get => _headerHorizontalAlignment;

			set
			{
				if (value == _headerHorizontalAlignment)
				{
					return;
				}

				_headerHorizontalAlignment = value;
				Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the default horizontal alignment of cell content. Defaults to <see cref="HorizontalAlignment.Left"/>.
		/// Individual columns can override this via <see cref="DataGridColumnBase.CellHorizontalAlignment"/>.
		/// </summary>
		[DefaultValue(HorizontalAlignment.Left)]
		public HorizontalAlignment CellHorizontalAlignment
		{
			get => _cellHorizontalAlignment;

			set
			{
				if (value == _cellHorizontalAlignment)
				{
					return;
				}

				_cellHorizontalAlignment = value;
				Invalidate(true);
			}
		}

		/// <summary>
		/// Gets the cloned <see cref="DataGridStyle"/> currently applied to this DataGrid.
		/// </summary>
		public DataGridStyle Style => _style;

		/// <inheritdoc/>
		[DefaultValue(HorizontalAlignment.Stretch)]
		public override HorizontalAlignment HorizontalAlignment
		{
			get { return base.HorizontalAlignment; }
			set { base.HorizontalAlignment = value; }
		}

		/// <inheritdoc/>
		[DefaultValue(VerticalAlignment.Stretch)]
		public override VerticalAlignment VerticalAlignment
		{
			get { return base.VerticalAlignment; }
			set { base.VerticalAlignment = value; }
		}

		/// <inheritdoc/>
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

		/// <inheritdoc/>
		protected internal override bool AcceptsMouseWheel => VerticalScrollingOn;

		/// <inheritdoc/>
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

		private bool VerticalScrollingOn => RowsPerPage < TotalRows;

		private IImage VerticalScrollBackground => _style.VerticalScrollBackground;

		private IImage VerticalScrollKnob => _style.VerticalScrollKnob;

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

		private int ColumnShift
		{
			get
			{
				var result = 0;

				if (IndexColumnWidth != null)
				{
					++result;
				}

				return result;
			}
		}

		private int RowShift
		{
			get
			{
				var result = 0;

				if (HasHeader)
				{
					++result;
				}

				if (HasFilter)
				{
					++result;
				}

				return result;
			}
		}

		/// <summary>
		/// Occurs when the selected row index changes.
		/// </summary>
		public event MyraEventHandler SelectedIndexChanged
		{
			add => _grid.SelectedIndexChanged += value;
			remove => _grid.SelectedIndexChanged -= value;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DataGrid"/> class with the specified stylesheet and style name.
		/// </summary>
		/// <param name="stylesheet">The stylesheet to use for applying styles.</param>
		/// <param name="styleName">The name of the style to apply from the stylesheet.</param>
		public DataGrid(Stylesheet stylesheet, string styleName = Stylesheet.DefaultStyleName)
		{
			if (stylesheet == null || styleName == null)
			{
				throw new Exception("Style can't be null for DataGrid");
			}

			ClipToBounds = true;
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;

			_grid = new Grid
			{
				DefaultRowProportion = Proportion.Auto
			};

			_grid.HoverIndexChanged += OnGridHoverIndexChanged;
			_grid.SelectedIndexChanged += OnGridSelectedIndexChanged;

			_layout = new SingleItemLayout<Grid>(this)
			{
				Child = _grid
			};

			ChildrenLayout = _layout;

			SetStyle(stylesheet, styleName);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DataGrid"/> class using the current stylesheet.
		/// </summary>
		/// <param name="styleName">The name of the style to apply from the current stylesheet.</param>
		public DataGrid(string styleName = Stylesheet.DefaultStyleName) : this(Stylesheet.Current, styleName)
		{
		}

		private void RebuildColumns()
		{
			_grid.ColumnsProportions.Clear();

			if (IndexColumnWidth != null)
			{
				_grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, IndexColumnWidth.Value));
			}

			for (var i = 0; i < _columns.Length; ++i)
			{
				if (i != FillColumnIndex)
				{
					_grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, _columns[i].Width));
				}
				else
				{
					_grid.ColumnsProportions.Add(Proportion.Fill);
				}
			}

			if (FillColumnIndex == null)
			{
				_grid.ColumnsProportions.Add(Proportion.Fill);
			}
		}

		private void RebuildHeader()
		{
			if (!HasHeader)
			{
				return;
			}

			if (Style.HeaderStyle == null)
			{
				throw new Exception("HeaderStyle is null");
			}

			if (Style.HeaderStyle.LabelStyle == null)
			{
				throw new Exception("HeaderStyle.LabelStyle is null");
			}

			if (IndexColumnWidth != null)
			{
				var indexHeaderCell = new Label(null)
				{
					Text = "#",
					ClipToBounds = true,
					HorizontalAlignment = IndexColumnHeaderHorizontalAlignment ?? HeaderHorizontalAlignment
				};
				indexHeaderCell.ApplyLabelStyle(Style.HeaderStyle.LabelStyle);
				indexHeaderCell.Padding = Style.HeaderStyle.ContentPadding;

				Grid.SetRow(indexHeaderCell, 0);
				Grid.SetColumn(indexHeaderCell, 0);
				_grid.Widgets.Add(indexHeaderCell);
			}

			for (var i = 0; i < Columns.Length; ++i)
			{
				var column = Columns[i];

				Widget cellContent;

				var header = string.IsNullOrEmpty(column.Header) ? column.Property : column.Header;
				if (i == SortColumn)
				{
					var panel = new HorizontalStackPanel
					{
						Spacing = Style.HeaderStyle.SortImageTextSpacing,
						Padding = Style.HeaderStyle.ContentPadding
					};

					var label = new Label(null)
					{
						Text = header
					};

					label.ApplyLabelStyle(Style.HeaderStyle.LabelStyle);
					panel.Widgets.Add(label);

					var image = new Image()
					{
						Renderable = SortDirection == ListSortDirection.Ascending ? Style.HeaderStyle.SortAscendingImage : Style.HeaderStyle.SortDescendingImage,
						VerticalAlignment = VerticalAlignment.Center
					};
					panel.Widgets.Add(image);

					cellContent = panel;
				}
				else
				{
					var label = new Label(null)
					{
						Text = header
					};
					label.ApplyLabelStyle(Style.HeaderStyle.LabelStyle);
					label.Padding = Style.HeaderStyle.ContentPadding;

					cellContent = label;
				}

				cellContent.HorizontalAlignment = column.HeaderHorizontalAlignment ?? HeaderHorizontalAlignment;

				var headerButton = new Button(null)
				{
					Content = cellContent,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					ClipToBounds = true,
					Tag = i
				};
				headerButton.ApplyButtonStyle(Style.HeaderStyle);

				if (SortableHeaders && column.Flags.HasFlag(DataGridColumnFlags.CanSort) && column.HasSorting)
				{
					headerButton.Click += HeaderCell_Click;
				}
				else
				{
					headerButton.ReadOnly = true;
				}

				Grid.SetRow(headerButton, 0);
				Grid.SetColumn(headerButton, i + ColumnShift);
				_grid.Widgets.Add(headerButton);
			}
		}

		private void HeaderCell_Click(object sender, MyraEventArgs e)
		{
			var button = (Button)sender;
			var i = (int)button.Tag;

			if (i == SortColumn)
			{
				if (SortDirection == ListSortDirection.Ascending)
				{
					SortDirection = ListSortDirection.Descending;
				}
				else
				{
					SortDirection = ListSortDirection.Ascending;
				}
			}
			else
			{
				SortColumn = i;
				SortDirection = ListSortDirection.Ascending;
			}
		}

		private void RebuildFilters()
		{
			if (!HasFilter)
			{
				return;
			}

			if (Style.FilterStyle == null)
			{
				throw new Exception("FilterStyle is null");
			}

			var row = HasHeader ? 1 : 0;
			for (var i = 0; i < Columns.Length; ++i)
			{
				var column = Columns[i];
				if (!column.Flags.HasFlag(DataGridColumnFlags.CanFilter) || !column.HasFilter)
				{
					continue;
				}

				var filterCell = new TextBox(null)
				{
					HorizontalAlignment = HorizontalAlignment.Stretch,
					Text = column.Filter
				};

				filterCell.ApplyTextBoxStyle(Style.FilterStyle);

				filterCell.TextChangedByUser += (s, a) =>
				{
					column.Filter = filterCell.Text;
					Invalidate(false, InvalidateLevelData.VisualData);
					StartRow = 0;
				};

				Grid.SetRow(filterCell, row);
				Grid.SetColumn(filterCell, i + ColumnShift);
				_grid.Widgets.Add(filterCell);
			}
		}

		private void UpdateSourceDataRow(int row)
		{
			var item = _data[row];
			var type = item.GetType();

			var gridValues = new object[Columns.Length];
			for (var col = 0; col < Columns.Length; ++col)
			{
				var column = Columns[col];
				if (string.IsNullOrEmpty(column.Property))
				{
					throw new Exception("Column property must be defined. Index: " + col);
				}

				var property = type.GetProperty(column.Property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (property == null)
				{
					throw new Exception($"Property not found: {column.Property} in type {type.FullName}");
				}

				gridValues[col] = property.GetValue(item);
			}

			_sourceData[row] = new RowData(row, item, gridValues);
		}

		private void UpdateVisualData()
		{
			if (_visualData != null || _sourceData == null)
			{
				// Visual data is already up to date or source data is not available yet
				return;
			}

			var hasFilters = (from col in Columns where !string.IsNullOrEmpty(col.Filter) select col).Any();
			if (SortColumn == null && !hasFilters)
			{
				_visualData = _sourceData;
				return;
			}

			if (!hasFilters)
			{
				_visualData = new RowData[_sourceData.Length];
				for (var i = 0; i < _visualData.Length; ++i)
				{
					_visualData[i] = _sourceData[i];
				}
			}
			else
			{
				// Filter
				var vd = new List<RowData>();
				for (var i = 0; i < _sourceData.Length; ++i)
				{
					var s = _sourceData[i];

					var add = true;
					for (var j = 0; j < Columns.Length; ++j)
					{
						var col = Columns[j];
						if (string.IsNullOrEmpty(col.Filter))
						{
							continue;
						}

						var val = s.GridValues[j].ToString();

						var stringComparison = col.FilterStringComparison ?? FilterStringComparison;
						if (val.IndexOf(col.Filter, stringComparison) == -1)
						{
							add = false;
							break;
						}
					}

					if (add)
					{
						vd.Add(s);
					}
				}

				_visualData = vd.ToArray();
			}

			if (SortColumn != null)
			{
				// Sort
				var columnIndex = SortColumn.Value;
				Array.Sort(_visualData, (a, b) =>
				{
					var compare = ((IComparable)a.GridValues[columnIndex]).CompareTo(b.GridValues[columnIndex]);

					return SortDirection == ListSortDirection.Ascending ? compare : -compare;
				});
			}
		}

		private void UpdateData()
		{
			if (_data == null)
			{
				// No data to process
				return;
			}

			if (_sourceData == null)
			{
				_sourceData = new RowData[_data.Count];
				for (var row = 0; row < _data.Count; ++row)
				{
					UpdateSourceDataRow(row);
				}
			}

			UpdateVisualData();
		}

		private void Update()
		{
			var bounds = ActualBounds;
			var size = new Point(bounds.Width, bounds.Height);

			try
			{
				if (size == _lastArrangeSize)
				{
					return;
				}

				if (Columns == null)
				{
					if (Data != null)
					{
						throw new Exception("DataGrid: Columns aren't set, while Data is set.");
					}
					else
					{
						return;
					}
				}

				SuppressInvalidateMeasure = true;

				var oldSelectedRowIndex = SelectedRowIndex;

				if (_headerDirty)
				{
					// Full rebuild is required
					RebuildColumns();

					_grid.Widgets.Clear();
					RebuildHeader();
					RebuildFilters();
					_headerDirty = false;
				}
				else
				{
					// Only data widgets needs to be rebuilt
					foreach (var widget in _dataWidgets)
					{
						_grid.Widgets.Remove(widget);
					}

					_dataWidgets.Clear();
				}

				_grid.Width = Dimension.Auto;

				if (size.X == 0 || size.Y == 0 || _data == null)
				{
					return;
				}

				UpdateData();

				_dataWidgets.Clear();
				_visibleRowsIndices.Clear();

				var fits = true;
				for (var row = StartRow; row < _visualData.Length; ++row)
				{
					var count = _visibleRowsIndices.Count;
					var gridRow = count + RowShift;

					if (IndexColumnWidth != null)
					{
						if (Style.TextCellStyle == null)
						{
							throw new Exception("TextCellStyle is null");
						}

						var cell = new Label(null)
						{
							Text = row.ToString(),
							ClipToBounds = true,
							HorizontalAlignment = IndexColumnCellHorizontalAlignment ?? CellHorizontalAlignment
						};
						cell.ApplyLabelStyle(Style.TextCellStyle);

						Grid.SetRow(cell, gridRow);
						Grid.SetColumn(cell, 0);
						_grid.Widgets.Add(cell);
						_dataWidgets.Add(cell);
					}

					var rowData = _visualData[row];
					for (var col = 0; col < Columns.Length; ++col)
					{
						var column = Columns[col];
						var value = rowData.GridValues[col];
						
						var cell = column.CreateWidget(value, Style);
						if (cell == null)
						{
							continue;
						}

						cell.HorizontalAlignment = column.CellHorizontalAlignment ?? CellHorizontalAlignment;

						cell.ClipToBounds = true;

						Grid.SetRow(cell, gridRow);
						Grid.SetColumn(cell, col + ColumnShift);
						_grid.Widgets.Add(cell);
						_dataWidgets.Add(cell);
					}

					_visibleRowsIndices.Add(rowData.Index);

					var sz = _grid.Measure(size);
					if (sz.Y > size.Y)
					{
						fits = false;
						break;
					}
				}

				RowsPerPage = _visibleRowsIndices.Count;
				if (!fits)
				{
					--RowsPerPage;
				}

				if (VerticalScrollingOn)
				{
					var vsWidth = VerticalScrollbarWidth;

					var bh = bounds.Height;
					_verticalScrollbarFrame = new Rectangle(
						bounds.Left + bounds.Width - vsWidth,
						bounds.Top,
						vsWidth,
						bh);

					var thumbHeight = Math.Max(VerticalScrollKnob.Size.Y, RowsPerPage * bh / TotalRows);
					_verticalScrollbarThumb = new Rectangle(
						bounds.Left + bounds.Width - vsWidth,
						bounds.Top,
						VerticalScrollKnob.Size.X,
						thumbHeight);

					_thumbMaximumY = bh - thumbHeight;
					if (_thumbMaximumY == 0)
					{
						_thumbMaximumY = 1;
					}

					_grid.Width = size.X - vsWidth;
				}

				SelectedRowIndex = oldSelectedRowIndex;
			}
			finally
			{
				_lastArrangeSize = size;
				SuppressInvalidateMeasure = false;
			}
		}

		/// <inheritdoc/>
		protected override void InternalArrange()
		{
			base.InternalArrange();

			Update();
		}

		/// <inheritdoc/>
		protected override Point InternalMeasure(Point availableSize)
		{
			var result = Mathematics.PointZero;
			if (Columns == null)
			{
				return result;
			}

			if (IndexColumnWidth != null)
			{
				result.X += IndexColumnWidth.Value;

				if (Columns.Length > 0)
				{
					result.X += ColumnSpacing;
				}
			}

			for (var i = 0; i < Columns.Length; ++i)
			{
				var column = Columns[i];
				result.X += column.Width;

				if (i < Columns.Length - 1)
				{
					result.X += ColumnSpacing;
				}
			}

			if (VerticalScrollingOn)
			{
				result.X += VerticalScrollbarWidth;
			}

			return result;
		}

		/// <inheritdoc/>
		public override void InternalRender(RenderContext context)
		{
			base.InternalRender(context);

			if (!VerticalScrollingOn)
			{
				return;
			}

			if (VerticalScrollBackground != null)
			{
				VerticalScrollBackground.Draw(context, _verticalScrollbarFrame);
			}

			if (VerticalScrollKnob != null)
			{
				var r = _verticalScrollbarThumb;

				var bh = ActualBounds.Height;
				r.Y += StartRow * bh / TotalRows;
				VerticalScrollKnob.Draw(context, r);
			}
		}

		/// <inheritdoc/>
		public override void OnMouseWheel(float delta)
		{
			base.OnMouseWheel(delta);

			if (!VerticalScrollingOn)
			{
				return;
			}

			var step = ScrollMultiplier;
			if (delta > 0)
			{
				step = -step;
			}

			var newStartRow = StartRow + step;
			newStartRow = Mathematics.Clamp(newStartRow, 0, TotalRows - RowsPerPage);

			StartRow = newStartRow;
		}

		/// <inheritdoc/>
		public override void OnTouchDown()
		{
			base.OnTouchDown();

			if (Desktop == null)
			{
				return;
			}

			var touchPosition = ToLocal(Desktop.TouchPosition.Value);

			if (VerticalScrollingOn)
			{
				var bh = ActualBounds.Height;

				var r = _verticalScrollbarThumb;
				r.Y += (StartRow * bh / TotalRows);
				if (r.Contains(touchPosition))
				{
					_startBoundsPos = Desktop.TouchPosition.Value.Y;
				}
				else if (_verticalScrollbarFrame.Contains(touchPosition))
				{
					var fraction = (float)(touchPosition.Y - _verticalScrollbarFrame.Top) / bh;
					var targetRow = (int)(fraction * TotalRows);
					targetRow = Mathematics.Clamp(targetRow, 0, TotalRows - RowsPerPage);
					StartRow = targetRow;

					_startBoundsPos = Desktop.TouchPosition.Value.Y;
				}
			}

			if (ResizableColumns)
			{
				var localPos = LocalTouchPosition;
				if (localPos != null && IsInHeaderRow(localPos.Value))
				{
					var boundaryIndex = GetColumnBoundaryIndex(localPos.Value);
					if (boundaryIndex != null)
					{
						_resizingColumnIndex = boundaryIndex.Value;
						_resizeStartX = Desktop.TouchPosition.Value.X;
						_resizeOriginalWidth = _grid.ColWidths[boundaryIndex.Value];

						if (boundaryIndex.Value < _grid.ColWidths.Count - 1)
						{
							_resizeNextWidth = _grid.ColWidths[boundaryIndex.Value + 1];
						}

						Desktop.TouchMoved += DesktopColumnResizeMoved;
					}
				}
			}
		}

		/// <inheritdoc/>
		public override void OnTouchUp()
		{
			base.OnTouchUp();

			_startBoundsPos = null;

			if (_resizingColumnIndex != null)
			{
				_resizingColumnIndex = null;

				if (Desktop != null)
				{
					Desktop.TouchMoved -= DesktopColumnResizeMoved;
				}
			}
		}

		private void DesktopTouchMoved(object sender, MyraEventArgs args)
		{
			if (!_startBoundsPos.HasValue || Desktop == null || !VerticalScrollingOn)
			{
				return;
			}

			var touchPosition = Desktop.TouchPosition;
			var bh = ActualBounds.Height;

			var delta = (touchPosition.Value.Y - _startBoundsPos.Value) * TotalRows / bh;
			_startBoundsPos = touchPosition.Value.Y;

			var newStartRow = StartRow + delta;
			newStartRow = Mathematics.Clamp(newStartRow, 0, TotalRows - RowsPerPage);

			StartRow = newStartRow;
		}

		private void DesktopTouchUp(object sender, MyraEventArgs args)
		{
			_startBoundsPos = null;
		}

		internal override IDictionary GetStylesDictionary(Stylesheet stylesheet) => stylesheet.DataGridStyles;

		/// <inheritdoc/>
		protected override void ApplyStyle(WidgetStyle style)
		{
			base.ApplyStyle(style);

			var dataGridStyle = (DataGridStyle)style;

			_style = (DataGridStyle)dataGridStyle.Clone();
			ShowGridLines = dataGridStyle.ShowGridLines;
			GridLinesColor = dataGridStyle.GridLinesColor;
			ColumnSpacing = dataGridStyle.ColumnSpacing;
			RowSpacing = dataGridStyle.RowSpacing;
			SelectionBackground = dataGridStyle.SelectionBackground;
			SelectionHoverBackground = dataGridStyle.SelectionHoverBackground;
			GridSelectionMode = dataGridStyle.GridSelectionMode;
			HoverIndexCanBeNull = dataGridStyle.HoverIndexCanBeNull;
			CanSelectNothing = dataGridStyle.CanSelectNothing;
		}

		private bool IsInHeaderRow(Point localPos)
		{
			if (_grid.RowHeights.Count == 0)
			{
				return false;
			}

			return localPos.Y >= 0 && localPos.Y < _grid.RowHeights[0];
		}

		private void OnGridHoverIndexChanged(object sender, MyraEventArgs args)
		{
			if (_grid.HoverRowIndex < RowShift)
			{
				_grid.HoverRowIndex = null;
			}
		}

		private void OnGridSelectedIndexChanged(object sender, MyraEventArgs args)
		{
			if (_grid.SelectedRowIndex < RowShift)
			{
				_grid.SelectedRowIndex = null;
			}
			else if (_grid.SelectedRowIndex != null)
			{
				var val = _grid.SelectedRowIndex.Value - RowShift;

				if (val >= 0 && val < _visibleRowsIndices.Count)
				{
					_selectedRowIndex = _visibleRowsIndices[val];
				}
			}
		}

		private int? GetColumnBoundaryIndex(Point localPos)
		{
			var bounds = ActualBounds;
			var offsetX = bounds.Left;

			for (var i = 1; i < _grid.CellLocationsX.Count; ++i)
			{
				var boundaryX = offsetX + _grid.CellLocationsX[i] - _grid.ColumnSpacing / 2;
				if (Math.Abs(localPos.X - boundaryX) < ColumnResizeHandleWidth)
				{
					return i - 1;
				}
			}

			return null;
		}

		/// <inheritdoc/>
		public override void OnMouseMoved()
		{
			base.OnMouseMoved();

			if (Desktop == null)
			{
				return;
			}

			var localPos = LocalMousePosition;
			if (localPos == null)
			{
				return;
			}

			if (ResizableColumns)
			{
				if (_resizingColumnIndex != null)
				{
					MyraEnvironment.MouseCursorType = MouseCursorType.SizeWE;
				}
				else if (IsInHeaderRow(localPos.Value) && GetColumnBoundaryIndex(localPos.Value) >= 0)
				{
					MyraEnvironment.MouseCursorType = MouseCursorType.SizeWE;
				}
				else
				{
					MyraEnvironment.MouseCursorType = MouseCursor ?? MyraEnvironment.DefaultMouseCursorType;
				}
			}
		}

		/// <inheritdoc/>
		public override void OnMouseLeft()
		{
			base.OnMouseLeft();

			if (ResizableColumns)
			{
				MyraEnvironment.MouseCursorType = MouseCursor ?? MyraEnvironment.DefaultMouseCursorType;
			}
		}

		/// <summary>
		/// Re-evaluates the source values for the given data row and marks the visual
		/// data as needing a refresh. Call this after modifying a row's bound properties
		/// to reflect the changes in the grid display.
		/// </summary>
		/// <param name="rowIndex">The zero-based index of the row in the <see cref="Data"/> collection.</param>
		public void InvalidateDataRow(int rowIndex)
		{
			if (_sourceData == null)
			{
				// Full rebuild is scheduled anyway
				return;
			}

			UpdateSourceDataRow(rowIndex);
			Invalidate(false, InvalidateLevelData.VisualData);
		}

		private void Invalidate(bool headerDirty, InvalidateLevelData level = InvalidateLevelData.None)
		{
			if (headerDirty)
			{
				_headerDirty = true;
				SelectedRowIndex = null;
			}

			if (level >= InvalidateLevelData.VisualData)
			{
				_visualData = null;
			}

			if (level >= InvalidateLevelData.SourceData)
			{
				_sourceData = null;
			}

			_lastArrangeSize = null;
			InvalidateArrange();
		}

		private void DesktopColumnResizeMoved(object sender, MyraEventArgs args)
		{
			if (_resizingColumnIndex == null || Desktop == null)
			{
				return;
			}

			var deltaX = Desktop.TouchPosition.Value.X - _resizeStartX;

			var rci = _resizingColumnIndex.Value;

			if (FillColumnIndex == null)
			{
				var newWidth = _resizeOriginalWidth + deltaX;
				if (newWidth < MinColumnWidth)
				{
					newWidth = MinColumnWidth;
				}

				_grid.ColumnsProportions[rci].Value = newWidth;

				if (rci >= ColumnShift)
				{
					Columns[rci - ColumnShift].Width = newWidth;
				}
			}
			else
			{
				// We need to make sure sum of the resized column and next remains the same
				var totalWidth = _resizeOriginalWidth + _resizeNextWidth;
				var newWidth = _resizeOriginalWidth + deltaX;
				newWidth = Mathematics.Clamp(newWidth, MinColumnWidth, totalWidth - MinColumnWidth);
				_grid.ColumnsProportions[rci].Value = newWidth;
				if (rci >= ColumnShift)
				{
					Columns[rci - ColumnShift].Width = newWidth;
				}

				if (rci < _grid.ColumnsProportions.Count - 1)
				{
					_grid.ColumnsProportions[rci + 1].Value = totalWidth - newWidth;
					Columns[rci + 1 - ColumnShift].Width = totalWidth - newWidth;
				}
			}
		}
	}
}