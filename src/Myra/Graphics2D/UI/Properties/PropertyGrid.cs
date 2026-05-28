using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Myra.Graphics2D.UI.ColorPicker;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using System.Xml.Serialization;
using Myra.MML;
using Myra.Graphics2D.UI.File;
using System.IO;
using Myra.Attributes;
using FontStashSharp;
using FontStashSharp.RichText;
using Myra.Graphics2D.Brushes;
using AssetManagementBase;
using Myra.Events;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#elif STRIDE
using Stride.Core.Mathematics;
using Texture2D = Stride.Graphics.Texture;
#else
using System.Drawing;
using SolidBrush = Myra.Graphics2D.Brushes.SolidBrush;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.UI.Properties
{
	/// <summary>
	/// A widget that displays and allows editing of object properties in a grid layout.
	/// Uses reflection to discover properties and fields, creates appropriate editors for each type,
	/// and organizes them by category with support for nested objects and filtering.
	/// </summary>
	public class PropertyGrid : Widget
	{
		private const string DefaultCategoryName = "Miscellaneous";

		// Nested class: represents a collapsible category group containing related properties
		private class SubGrid : Widget
		{
			private readonly GridLayout _layout = new GridLayout();

			private readonly ToggleButton _mark;
			private readonly PropertyGrid _propertyGrid;

			public ToggleButton Mark
			{
				get { return _mark; }
			}

			public PropertyGrid PropertyGrid
			{
				get { return _propertyGrid; }
			}

			public Rectangle HeaderBounds
			{
				get
				{
					var headerBounds = new Rectangle(0, 0, ActualBounds.Width, _layout.GetRowHeight(0));

					return headerBounds;
				}
			}

			[Browsable(false)]
			[XmlIgnore]
			public bool IsEmpty
			{
				get
				{
					return _propertyGrid.IsEmpty;
				}
			}

			// Constructs a collapsible category group with a toggle button, label, and nested property grid
			public SubGrid(PropertyGrid parent, object value, string header, string category, string filter, Record parentProperty)
			{
				ChildrenLayout = _layout;

				_layout.ColumnSpacing = 4;
				_layout.RowSpacing = 4;

				// Two columns: expand/collapse toggle (Auto) and category label (Fill)
				_layout.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
				_layout.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
				_layout.RowsProportions.Add(new Proportion(ProportionType.Auto));
				_layout.RowsProportions.Add(new Proportion(ProportionType.Auto));

				// Create nested PropertyGrid to display this category's properties
				_propertyGrid = new PropertyGrid(parent.PropertyGridStyle, category, parentProperty, parent)
				{
					Object = value,
					Filter = filter,
					HorizontalAlignment = HorizontalAlignment.Stretch,
				};
				Grid.SetColumn(_propertyGrid, 1);
				Grid.SetRow(_propertyGrid, 1);

				// Create expand/collapse toggle button with icon
				var markImage = new Image();
				var imageStyle = parent.PropertyGridStyle.MarkStyle.ImageStyle;
				if (imageStyle != null)
				{
					markImage.ApplyPressableImageStyle(imageStyle);
				}

				_mark = new ToggleButton(null)
				{
					HorizontalAlignment = HorizontalAlignment.Left,
					VerticalAlignment = VerticalAlignment.Center,
					Content = markImage
				};

				Children.Add(_mark);

				// Handle expansion/collapse: show/hide nested property grid and track expanded state
				_mark.PressedChanged += (sender, args) =>
				{
					if (_mark.IsPressed)
					{
						Children.Add(_propertyGrid);
						parent._expandedCategories.Add(category);
					}
					else
					{
						Children.Remove(_propertyGrid);
						parent._expandedCategories.Remove(category);
					}
				};

				// Check if category should start expanded (defaults to true unless explicitly folded)
				var expanded = true;
				if (parentProperty != null && parentProperty.FindAttribute<DesignerFoldedAttribute>() != null)
				{
					expanded = false;
				}

				if (expanded)
				{
					_mark.IsPressed = true;
				}

				// Create category header label
				var label = new Label(null)
				{
					Text = header,
				};
				Grid.SetColumn(label, 1);
				label.ApplyLabelStyle(parent.PropertyGridStyle.LabelStyle);

				Children.Add(label);

				HorizontalAlignment = HorizontalAlignment.Stretch;
				VerticalAlignment = VerticalAlignment.Stretch;
			}

			public override void OnTouchDoubleClick()
			{
				base.OnTouchDoubleClick();

				var mousePosition = ToLocal(Desktop.MousePosition);
				if (!HeaderBounds.Contains(mousePosition) || _mark.Bounds.Contains(mousePosition))
				{
					return;
				}

				_mark.IsPressed = !_mark.IsPressed;
			}

			public override void InternalRender(RenderContext context)
			{
				if (_propertyGrid.PropertyGridStyle.SelectionHoverBackground != null && IsMouseInside)
				{
					var headerBounds = HeaderBounds;
					if (headerBounds.Contains(ToLocal(Desktop.MousePosition)))
					{
						_propertyGrid.PropertyGridStyle.SelectionHoverBackground.Draw(context, headerBounds);
					}
				}

				base.InternalRender(context);
			}
		}

		private readonly GridLayout _layout = new GridLayout();
		private readonly PropertyGrid _parentGrid;
		private Record _parentProperty;
		private readonly Dictionary<string, List<Record>> _records = new Dictionary<string, List<Record>>();
		private readonly HashSet<string> _expandedCategories = new HashSet<string>();
		private object _object;
		private bool _ignoreCollections;
		private readonly PropertyGridSettings _settings = new PropertyGridSettings();
		private string _filter;
		private Type _parentType;

		/// <summary>
		/// Gets the tree style used for styling the property grid.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public TreeStyle PropertyGridStyle { get; private set; }

		/// <summary>
		/// Gets or sets the object being displayed and edited in this property grid.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public object Object
		{
			get { return _object; }

			set
			{
				if (value == _object)
				{
					return;
				}

				_object = value;
				Rebuild();

				ObjectChanged?.Invoke(this, InputEventType.ValueChanged);
			}
		}

		/// <summary>
		/// Used to determine the attached properties
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Type ParentType
		{
			get
			{
				if (_parentGrid != null)
				{
					return _parentGrid.ParentType;
				}

				return _parentType;
			}

			set
			{
				_parentType = value;
			}
		}

		/// <summary>
		/// Gets the category name of this property grid.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public string Category { get; private set; }

		/// <summary>
		/// Gets or sets a value indicating whether collection properties should be ignored when building the property grid.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(false)]
		public bool IgnoreCollections
		{
			get
			{
				if (_parentGrid != null)
				{
					return _parentGrid.IgnoreCollections;
				}

				return _ignoreCollections;
			}

			set
			{
				_ignoreCollections = value;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the property grid has no child properties.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public bool IsEmpty
		{
			get
			{
				return Children.Count == 0;
			}
		}

		/// <summary>
		/// Gets the settings that control the behavior and appearance of the property grid.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public PropertyGridSettings Settings
		{
			get
			{
				if (_parentGrid != null)
				{
					return _parentGrid.Settings;
				}

				return _settings;
			}
		}

		/// <summary>
		/// Gets or sets the width of the first column in the property grid layout.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public int FirstColumnWidth
		{
			get
			{
				return (int)_layout.ColumnsProportions[0].Value;
			}

			set
			{
				_layout.ColumnsProportions[0].Value = value;
			}
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the property grid.
		/// </summary>
		[DefaultValue(HorizontalAlignment.Stretch)]
		public override HorizontalAlignment HorizontalAlignment
		{
			get { return base.HorizontalAlignment; }
			set { base.HorizontalAlignment = value; }
		}

		/// <summary>
		/// Gets or sets the vertical alignment of the property grid.
		/// </summary>
		[DefaultValue(VerticalAlignment.Stretch)]
		public override VerticalAlignment VerticalAlignment
		{
			get { return base.VerticalAlignment; }
			set { base.VerticalAlignment = value; }
		}

		/// <summary>
		/// Gets or sets the filter string used to filter properties displayed in the grid.
		/// </summary>
		[XmlIgnore]
		[Browsable(false)]
		public string Filter
		{
			get => _filter;
			set
			{
				if (_filter == value)
				{
					return;
				}

				_filter = value;
				Rebuild();
			}
		}

		/// <summary>
		/// Gets or sets a custom provider for determining the valid values for a property.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Func<object, Record, CustomValues> CustomValuesProvider;

		/// <summary>
		/// Gets or sets a custom setter for applying property values.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Func<Record, object, object, bool> CustomSetter;

		/// <summary>
		/// Gets or sets a custom provider for creating widgets to edit property values.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Func<Record, object, Widget> CustomWidgetProvider;

		/// <summary>
		/// Occurs when a property value is changed in the grid.
		/// </summary>
		public event MyraEventHandler<GenericEventArgs<string>> PropertyChanged;

		/// <summary>
		/// Occurs when the edited object is changed.
		/// </summary>
		public event MyraEventHandler ObjectChanged;

		// Private constructor used for nested property grids (categories and complex objects)
		private PropertyGrid(TreeStyle style, string category, Record parentProperty, PropertyGrid parentGrid = null)
		{
			ChildrenLayout = _layout;

			_parentGrid = parentGrid;

			_parentProperty = parentProperty;
			// Two-column layout: property names (left) and value editors (right)
			_layout.ColumnSpacing = 8;
			_layout.RowSpacing = 8;
			_layout.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1));
			_layout.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1));

			Category = category;

			if (style != null)
			{
				ApplyPropertyGridStyle(style);
			}

			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;
			Filter = string.Empty;

			// Inherit customization callbacks from parent grid
			this.CustomWidgetProvider = parentGrid?.CustomWidgetProvider;
			this.CustomSetter = parentGrid?.CustomSetter;
			this.CustomValuesProvider = parentGrid?.CustomValuesProvider;
		}

		/// <summary>
		/// Initializes a new instance of the PropertyGrid class with the specified style and category.
		/// </summary>
		/// <param name="style">The tree style to apply to the property grid.</param>
		/// <param name="category">The category name for the property grid (typically "Miscellaneous" for root grids).</param>
		public PropertyGrid(TreeStyle style, string category) : this(style, category, null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the PropertyGrid class with the specified category using the current stylesheet.
		/// </summary>
		/// <param name="category">The category name for the property grid.</param>
		public PropertyGrid(string category) : this(Stylesheet.Current.TreeStyle, category)
		{
		}

		/// <summary>
		/// Initializes a new instance of the PropertyGrid class with the default category name.
		/// </summary>
		public PropertyGrid() : this(DefaultCategoryName)
		{
		}

		// Propagates a property change event up the hierarchy to the root property grid
		private void FireChanged(string name)
		{
			var ev = PropertyChanged;

			// Walk up to the root grid's event handler
			var p = _parentGrid;
			while (p != null)
			{
				ev = p.PropertyChanged;
				p = p._parentGrid;
			}

			if (ev != null)
			{
				ev(this, new GenericEventArgs<string>(name, InputEventType.ValueChanged));
			}
		}

		private static void UpdateLabelCount(Label textBlock, int count)
		{
			textBlock.Text = string.Format("{0} Items", count);
		}

		private void SetValue(Record record, object obj, object value)
		{
			if (CustomSetter != null && CustomSetter(record, obj, value))
			{
				return;
			}

			record.SetValue(obj, value);
		}

		// Creates a dropdown editor for custom value lists provided by CustomValuesProvider
		private ComboView CreateCustomValuesEditor(Record record, CustomValues customValues, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var cv = new ComboView();
			// Populate dropdown with custom values supplied by the provider
			foreach (var v in customValues.Values)
			{
				var label = new Label
				{
					Text = v.Name,
					Tag = v.Value
				};

				cv.Widgets.Add(label);
			}

			cv.SelectedIndex = customValues.SelectedIndex;
			if (hasSetter)
			{
				cv.SelectedIndexChanged += (sender, args) =>
				{
					var item = cv.SelectedIndex != null ? customValues.Values[cv.SelectedIndex.Value].Value : null;
					SetValue(record, _object, item);
					FireChanged(record.Name);
				};
			}
			else
			{
				cv.Enabled = false;
			}

			return cv;
		}

		// Creates a checkbox editor for boolean properties
		private CheckButton CreateBooleanEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var isChecked = (bool)value;
			var cb = new CheckButton
			{
				IsChecked = isChecked
			};

			if (hasSetter)
			{
				cb.Click += (sender, args) =>
				{
					SetValue(record, _object, cb.IsChecked);
					FireChanged(propertyType.Name);
				};
			}
			else
			{
				cb.Enabled = false;
			}

			return cb;
		}

		// Creates a color editor with preview swatch and picker dialog button
		private Grid CreateColorEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var subGrid = new Grid
			{
				ColumnSpacing = 8,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			var isColor = propertyType == typeof(Color);

			subGrid.ColumnsProportions.Add(new Proportion());
			subGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));

			// Get current color from property (handle both Color and Color?)
			var color = Color.Transparent;
			if (isColor)
			{
				color = (Color)value;
			}
			else if (value != null)
			{
				color = ((Color?)value).Value;
			}

			// Color preview swatch
			var image = new Image
			{
				Renderable = Stylesheet.Current.WhiteRegion,
				VerticalAlignment = VerticalAlignment.Center,
				Width = 32,
				Height = 16,
				Color = color
			};

			subGrid.Widgets.Add(image);

			// "Change..." button to open color picker dialog
			var button = new Button
			{
				Tag = value,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					HorizontalAlignment = HorizontalAlignment.Center,
					Text = "Change..."
				}
			};
			Grid.SetColumn(button, 1);

			subGrid.Widgets.Add(button);

			if (hasSetter)
			{
				button.Click += (sender, args) =>
				{
					var dlg = new ColorPickerDialog()
					{
						Color = image.Color
					};

					dlg.Closed += (s, a) =>
					{
						if (!dlg.Result)
						{
							return;
						}

						image.Color = dlg.Color;
						SetValue(record, _object, dlg.Color);

						FireChanged(propertyType.Name);
					};

					dlg.ShowModal(Desktop);
				};
			}
			else
			{
				button.Enabled = false;
			}

			return subGrid;
		}

		// Creates editor for SolidBrush properties: shows color swatch and opens color picker dialog
		private Grid CreateBrushEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;

			var value = record.GetValue(_object) as SolidBrush;

			var subGrid = new Grid
			{
				ColumnSpacing = 8,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			subGrid.ColumnsProportions.Add(new Proportion());
			subGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));

			// Get brush color, or transparent if brush is null
			var color = Color.Transparent;
			if (value != null)
			{
				color = value.Color;
			}

			// Color preview swatch
			var image = new Image
			{
				Renderable = Stylesheet.Current.WhiteRegion,
				VerticalAlignment = VerticalAlignment.Center,
				Width = 32,
				Height = 16,
				Color = color
			};

			subGrid.Widgets.Add(image);

			// "Change..." button to open color picker
			var button = new Button
			{
				Tag = value,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					Text = "Change...",
					HorizontalAlignment = HorizontalAlignment.Center,
				}
			};
			Grid.SetColumn(button, 1);

			subGrid.Widgets.Add(button);

			if (hasSetter)
			{
				button.Click += (sender, args) =>
				{
					var dlg = new ColorPickerDialog()
					{
						Color = image.Color
					};

					dlg.Closed += (s, a) =>
					{
						if (!dlg.Result)
						{
							return;
						}

						image.Color = dlg.Color;
						SetValue(record, _object, new SolidBrush(dlg.Color));
						// Store color in BaseObject resources if applicable
						var baseObject = _object as BaseObject;
						if (baseObject != null)
						{
							baseObject.Resources[record.Name] = dlg.Color.ToHexString();
						}
						FireChanged(propertyType.Name);
					};

					dlg.ShowModal(Desktop);
				};
			}
			else
			{
				button.Enabled = false;
			}

			return subGrid;
		}

		// Creates a dropdown editor for enum properties with support for nullable enums
		private ComboView CreateEnumEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var isNullable = propertyType.IsNullableEnum();
			var enumType = isNullable ? propertyType.GetNullableType() : propertyType;
			var values = Enum.GetValues(enumType);

			var cv = new ComboView();

			// Add empty option for nullable enums
			if (isNullable)
			{
				cv.Widgets.Add(new Label
				{
					Text = string.Empty
				});
			}

			// Populate dropdown with all enum values
			foreach (var v in values)
			{
				cv.Widgets.Add(new Label
				{
					Text = v.ToString(),
					Tag = v
				});
			}

			var selectedIndex = Array.IndexOf(values, value);
			if (isNullable)
			{
				++selectedIndex;
			}
			cv.SelectedIndex = selectedIndex;

			if (hasSetter)
			{
				cv.SelectedIndexChanged += (sender, args) =>
				{
					if (cv.SelectedIndex != -1)
					{
						SetValue(record, _object, cv.SelectedItem.Tag);
						FireChanged(record.Name);
					}
				};
			}
			else
			{
				cv.Enabled = false;
			}

			return cv;
		}

		// Creates a numeric spin button editor for numeric types with optional Range attribute constraints
		private SpinButton CreateNumericEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var numericType = propertyType;
			if (propertyType.IsNullablePrimitive())
			{
				numericType = propertyType.GetNullableType();
			}

			var spinButton = new SpinButton
			{
				Integer = numericType.IsNumericInteger(),
				Nullable = propertyType.IsNullablePrimitive(),
				Value = value != null ? (float)Convert.ChangeType(value, typeof(float)) : default(float?)
			};

			// Apply Range attribute if present to set min/max bounds
			var rangeAttribute = record.FindAttribute<RangeAttribute>();
			if (rangeAttribute != null)
			{
				spinButton.Minimum = rangeAttribute.Minimum;
				spinButton.Maximum = rangeAttribute.Maximum;
			}

			if (hasSetter)
			{
				spinButton.ValueChanged += (sender, args) =>
				{
					try
					{
						object result;

						if (spinButton.Value != null)
						{
							result = Convert.ChangeType(spinButton.Value.Value, numericType);
						}
						else
						{
							result = null;
						}

						SetValue(record, _object, result);

						// Handle value type (struct) propagation up the hierarchy
						if (record.Type.IsValueType)
						{
							var tg = this;
							var pg = tg._parentGrid;
							while (pg != null && tg._parentProperty != null && tg._parentProperty.Type.IsValueType)
							{
								tg._parentProperty.SetValue(pg._object, tg._object);

								if (!tg._parentProperty.Type.IsValueType)
								{
									break;
								}

								tg = pg;
								pg = tg._parentGrid;
							}
						}

						FireChanged(record.Name);
					}
					catch (InvalidCastException)
					{
						// TODO: Rework this ugly type conversion solution
					}
					catch (Exception ex)
					{
						spinButton.Value = args.OldValue;
						var dialog = Dialog.CreateMessageBox("Error", ex.ToString());
						dialog.ShowModal(Desktop);
					}
				};
			}
			else
			{
				spinButton.Enabled = false;
			}

			return spinButton;
		}

		// Creates a text box editor for string and primitive type properties with type conversion
		private TextBox CreateStringEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var tf = new TextBox
			{
				Text = value != null ? value.ToString() : string.Empty
			};

			if (hasSetter)
			{
				tf.TextChanged += (sender, args) =>
				{
					try
					{
						object result;

						// Convert text input to appropriate type, handle nullable types
						if (propertyType.IsNullablePrimitive())
						{
							if (string.IsNullOrEmpty(tf.Text))
							{
								result = null;
							}
							else
							{
								result = Convert.ChangeType(tf.Text, record.Type.GetNullableType());
							}
						}
						else
						{
							result = Convert.ChangeType(tf.Text, record.Type);
						}

						SetValue(record, _object, result);

						// Propagate value type changes up the hierarchy
						if (record.Type.IsValueType)
						{
							var tg = this;
							var pg = tg._parentGrid;
							while (pg != null && tg._parentProperty != null)
							{
								tg._parentProperty.SetValue(pg._object, tg._object);

								if (!tg._parentProperty.Type.IsValueType)
								{
									break;
								}

								tg = pg;
								pg = tg._parentGrid;
							}
						}

						FireChanged(record.Name);
					}
					catch (Exception)
					{
						// TODO: Rework this ugly type conversion solution
					}
				};
			}
			else
			{
				tf.Enabled = false;
			}

			return tf;
		}

		// Creates editor for IList properties: shows item count and opens CollectionEditor dialog
		private Grid CreateCollectionEditor(Record record, Type itemType)
		{
			var value = record.GetValue(_object);

			var items = (IList)value;

			var subGrid = new Grid
			{
				ColumnSpacing = 8,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			subGrid.ColumnsProportions.Add(new Proportion());
			subGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));

			// Display current item count
			var label = new Label
			{
				VerticalAlignment = VerticalAlignment.Center,
			};
			UpdateLabelCount(label, items.Count);

			subGrid.Widgets.Add(label);

			// Button to open collection editor dialog
			var button = new Button
			{
				Tag = value,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					Text = "Change...",
					HorizontalAlignment = HorizontalAlignment.Center,
				}
			};
			Grid.SetColumn(button, 1);

			button.Click += (sender, args) =>
			{
				var collectionEditor = new CollectionEditor(items, itemType);

				var dialog = Dialog.CreateMessageBox("Edit", collectionEditor);

				dialog.ButtonOk.Click += (o, eventArgs) =>
				{
					collectionEditor.SaveChanges();
					UpdateLabelCount(label, items.Count);
				};

				dialog.ShowModal(Desktop);
			};

			subGrid.Widgets.Add(button);

			return subGrid;
		}

		// Creates file picker editor for asset properties (textures, fonts, etc.)
		// Uses provided loader function to convert file path to the desired asset type
		private Grid CreateFileEditor<T>(Record record, bool hasSetter, string filter, Func<string, T> loader)
		{
			if (Settings.AssetManager == null)
			{
				return null;
			}

			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var subGrid = new Grid
			{
				ColumnSpacing = 8,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			subGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
			subGrid.ColumnsProportions.Add(new Proportion());

			// Retrieve current file path from BaseObject resources or custom getter
			var baseObject = _object as BaseObject;
			var path = string.Empty;
			if (baseObject != null)
			{
				baseObject.Resources.TryGetValue(record.Name, out path);
			}
			else if (Settings.ImagePropertyValueGetter != null)
			{
				path = Settings.ImagePropertyValueGetter(record.Name);
			}

			var textBox = new TextBox
			{
				Text = path
			};

			subGrid.Widgets.Add(textBox);

			// "Change..." button to open file dialog
			var button = new Button
			{
				Tag = value,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					Text = "Change...",
					HorizontalAlignment = HorizontalAlignment.Center,
				}
			};
			Grid.SetColumn(button, 1);

			subGrid.Widgets.Add(button);

			if (hasSetter)
			{
				button.Click += (sender, args) =>
				{
					var dlg = new FileDialog(FileDialogMode.OpenFile)
					{
						Filter = filter
					};

					// Set initial file path or folder based on BasePath setting
					if (!string.IsNullOrEmpty(textBox.Text))
					{
						var filePath = textBox.Text;
						if (!Path.IsPathRooted(filePath) && !string.IsNullOrEmpty(Settings.BasePath))
						{
							filePath = Path.Combine(Settings.BasePath, filePath);
						}
						dlg.FilePath = filePath;
					}
					else if (!string.IsNullOrEmpty(Settings.BasePath))
					{
						dlg.Folder = Settings.BasePath;
					}

					dlg.Closed += (s, a) =>
					{
						if (!dlg.Result)
						{
							return;
						}

						try
						{
							var filePath = dlg.FilePath;
							// Make path relative to BasePath if applicable
							if (!string.IsNullOrEmpty(Settings.BasePath))
							{
								filePath = PathUtils.TryToMakePathRelativeTo(filePath, Settings.BasePath);
							}

							// Load asset and update property value
							var newValue = loader(filePath);
							textBox.Text = filePath;
							SetValue(record, _object, newValue);
							// Store path in resources
							if (baseObject != null)
							{
								baseObject.Resources[record.Name] = filePath;
							}
							else if (Settings.ImagePropertyValueSetter != null)
							{
								Settings.ImagePropertyValueSetter(record.Name, filePath);
							}

							FireChanged(propertyType.Name);
						}
						catch (Exception)
						{

						}
					};

					dlg.ShowModal(Desktop);
				};
			}
			else
			{
				button.Enabled = false;
			}

			return subGrid;
		}

		// Creates file picker editor for string properties with FilePathAttribute
		// Allows both open and save dialogs based on DialogMode in the attribute
		private Widget CreateAttributeFileEditor(Record record, bool hasSetter, FilePathAttribute attribute)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var result = new HorizontalStackPanel
			{
				Spacing = 8
			};

			// Optionally display the current file path as read-only text
			TextBox path = null;
			if (attribute.ShowPath)
			{
				path = new TextBox
				{
					Readonly = true,
					HorizontalAlignment = HorizontalAlignment.Stretch
				};

				if (value != null)
				{
					path.Text = value.ToString();
				}

				StackPanel.SetProportionType(path, ProportionType.Fill);
				result.Widgets.Add(path);
			}

			// "Change..." button to open file dialog with filter from attribute
			var button = new Button
			{
				Tag = value,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					Text = "Change...",
					HorizontalAlignment = HorizontalAlignment.Center,
				}
			};
			Grid.SetColumn(button, 1);

			if (hasSetter)
			{
				button.Click += (sender, args) =>
				{
					// Dialog mode (open/save) and filter from FilePathAttribute
					var dlg = new FileDialog(attribute.DialogMode)
					{
						Filter = attribute.Filter
					};

					// Set initial file path or folder
					if (value != null)
					{
						var filePath = value.ToString();
						if (!Path.IsPathRooted(filePath) && !string.IsNullOrEmpty(Settings.BasePath))
						{
							filePath = Path.Combine(Settings.BasePath, filePath);
						}
						dlg.FilePath = filePath;
					}
					else if (!string.IsNullOrEmpty(Settings.BasePath))
					{
						dlg.Folder = Settings.BasePath;
					}

					dlg.Closed += (s, a) =>
					{
						if (!dlg.Result)
						{
							return;
						}

						try
						{
							var filePath = dlg.FilePath;
							// Make path relative to BasePath if applicable
							if (!string.IsNullOrEmpty(Settings.BasePath))
							{
								filePath = PathUtils.TryToMakePathRelativeTo(filePath, Settings.BasePath);
							}

							// Update displayed path if shown
							if (path != null)
							{
								path.Text = filePath;
							}

							SetValue(record, _object, filePath);

							FireChanged(propertyType.Name);
						}
						catch (Exception)
						{
						}
					};

					dlg.ShowModal(Desktop);
				};
			}
			else
			{
				button.Enabled = false;
			}

			result.Widgets.Add(button);

			return result;
		}

		// Populates the grid with property rows: determines appropriate editor for each property type
		// and creates name label + value editor widget pairs
		private void FillSubGrid(ref int y, IReadOnlyList<Record> records)
		{
			for (var i = 0; i < records.Count; ++i)
			{
				var record = records[i];

				// Determine if property can be edited: check setter availability and struct constraints
				var hasSetter = record.HasSetter;
				if (_parentProperty != null && _parentProperty.Type.IsValueType && !_parentProperty.HasSetter)
				{
					hasSetter = false;
				}

				var value = record.GetValue(_object);
				Widget valueWidget = null;

				var oldY = y;

				var propertyType = record.Type;

				Proportion rowProportion;
				CustomValues customValues = null;

				// Flag indicating if property should have a collapsible nested grid for its sub-properties
				var needsSubGrid = false;

				// Try various providers and type matchers to determine appropriate editor widget
				if ((valueWidget = CustomWidgetProvider?.Invoke(record, _object)) != null)
				{
					// Custom widget provider takes precedence
				}
				else if (CustomValuesProvider != null && (customValues = CustomValuesProvider(_object, record)) != null)
				{
					if (customValues.Values.Length == 0)
					{
						continue;
					}

					valueWidget = CreateCustomValuesEditor(record, customValues, hasSetter);
					// Non-primitive custom values may need nested property display
					if (value != null && !value.GetType().IsPrimitive && value.GetType() != typeof(string))
					{
						needsSubGrid = true;
					}
				}
				else if (propertyType == typeof(bool))
				{
					valueWidget = CreateBooleanEditor(record, hasSetter);
				}
				else if (propertyType == typeof(Color) || propertyType == typeof(Color?))
				{
					valueWidget = CreateColorEditor(record, hasSetter);
				}
				else if (propertyType.IsEnum || propertyType.IsNullableEnum())
				{
					valueWidget = CreateEnumEditor(record, hasSetter);
				}
				else if (propertyType.IsNumericType() ||
						 (propertyType.IsNullablePrimitive() && propertyType.GetNullableType().IsNumericType()))
				{
					valueWidget = CreateNumericEditor(record, hasSetter);
				}
				else if (propertyType == typeof(string) && record.FindAttribute<FilePathAttribute>() != null)
				{
					var filePathAttr = record.FindAttribute<FilePathAttribute>();
					valueWidget = CreateAttributeFileEditor(record, hasSetter, filePathAttr);
				}
				else if (propertyType == typeof(string) || propertyType.IsPrimitive || propertyType.IsNullablePrimitive())
				{
					valueWidget = CreateStringEditor(record, hasSetter);
				}
				else if (typeof(IList).IsAssignableFrom(propertyType))
				{
					if (!IgnoreCollections)
					{
						var it = propertyType.FindGenericType(typeof(ICollection<>));
						if (it != null)
						{
							var itemType = it.GenericTypeArguments[0];
							if (value != null)
							{
								valueWidget = CreateCollectionEditor(record, itemType);
							}
						}
					}
				}
				else if (propertyType == typeof(SpriteFontBase))
				{
					valueWidget = CreateFileEditor(record, hasSetter, "*.fnt", name => Settings.AssetManager.LoadFont(name));
				}
				else if (propertyType == typeof(IBrush))
				{
					valueWidget = CreateBrushEditor(record, hasSetter);
				}
				else if (propertyType == typeof(IImage))
				{
					valueWidget = CreateFileEditor(record, hasSetter, "*.png|*.jpg|*.bmp|*.gif", name => Settings.AssetManager.LoadTextureRegion(name));
				}
#if !PLATFORM_AGNOSTIC
				else if (propertyType == typeof(Texture2D))
				{
					valueWidget = CreateFileEditor(record, hasSetter, "*.png|*.jpg|*.bmp|*.gif", name => Settings.AssetManager.LoadTexture2D(MyraEnvironment.GraphicsDevice, name));
				}
#if !STRIDE
				else if (propertyType == typeof(TextureCube))
				{
					valueWidget = CreateFileEditor(record, hasSetter, "*.dds", name => Settings.AssetManager.LoadTexture2D(MyraEnvironment.GraphicsDevice, name));
				}
#endif
#endif
				else
				{
					// No editor found for this type: show null label or prepare nested grid
					if (value == null)
					{
						var tb = new Label();
						tb.ApplyLabelStyle(PropertyGridStyle.LabelStyle);
						tb.Text = "null";

						valueWidget = tb;
					}
					else
					{
						// Complex object: will create nested PropertyGrid in SubGrid
						needsSubGrid = true;
					}
				}

				// Add the property row if we have an editor widget
				if (valueWidget != null)
				{
					var name = record.Name;
					var dn = record.FindAttribute<DisplayNameAttribute>();

					// Use DisplayName attribute if available, otherwise use property name
					if (dn != null)
					{
						name = dn.DisplayName;
					}

					// Skip properties that don't match the filter
					if (!PassesFilter(name))
					{
						continue;
					}

					// Create property name label (left column)
					var nameLabel = new Label
					{
						Text = name,
						VerticalAlignment = VerticalAlignment.Center,
					};

					Grid.SetColumn(nameLabel, 0);
					Grid.SetRow(nameLabel, oldY);

					Children.Add(nameLabel);

					// Add value editor widget (right column)
					Grid.SetColumn(valueWidget, 1);
					Grid.SetRow(valueWidget, oldY);
					valueWidget.HorizontalAlignment = HorizontalAlignment.Stretch;
					valueWidget.VerticalAlignment = VerticalAlignment.Top;

					Children.Add(valueWidget);

					rowProportion = new Proportion(ProportionType.Auto);
					_layout.RowsProportions.Add(rowProportion);
					++y;
				}

				// Add collapsible nested property grid for complex object properties
				if (needsSubGrid)
				{
					if (value != null)
					{
						if (PassesFilter(record.Name))
						{
							var subGrid = new SubGrid(this, value, record.Name, DefaultCategoryName, string.Empty, record);
							Grid.SetColumnSpan(subGrid, 2);
							Grid.SetRow(subGrid, y);

							Children.Add(subGrid);

							rowProportion = new Proportion(ProportionType.Auto);
							_layout.RowsProportions.Add(rowProportion);
							++y;
						}

						continue;
					}
				}
			}
		}

		/// <summary>
		/// Determines whether the specified property name passes the current filter.
		/// Uses case-insensitive substring matching.
		/// </summary>
		/// <param name="name">The property name to check.</param>
		/// <returns>true if the name passes the filter; otherwise, false.</returns>
		public bool PassesFilter(string name)
		{
			// Empty filter or name matches everything
			if (string.IsNullOrEmpty(Filter) || string.IsNullOrEmpty(name))
			{
				return true;
			}

			// Case-insensitive substring match
			return name.ToLower().Contains(_filter.ToLower());
		}

		/// <summary>
		/// Rebuilds the property grid based on the current object and settings.
		/// Discovers all public properties, fields, and attached properties via reflection,
		/// organizes them by category, applies filter, and creates appropriate editor widgets.
		/// </summary>
		public void Rebuild()
		{
			_layout.RowsProportions.Clear();
			Children.Clear();
			_records.Clear();
			_expandedCategories.Clear();

			if (_object == null)
			{
				return;
			}

			// Discover all public properties using reflection
			var properties = from p in _object.GetType().GetProperties() select p;
			var records = new List<Record>();
			foreach (var property in properties)
			{
				// Skip non-public, static, or getter-less properties
				if (property.GetGetMethod() == null ||
					!property.GetGetMethod().IsPublic ||
					property.GetGetMethod().IsStatic)
				{
					continue;
				}

				var hasSetter = property.GetSetMethod() != null &&
								property.GetSetMethod().IsPublic;

				// Skip properties marked as non-browsable
				var browsableAttr = property.FindAttribute<BrowsableAttribute>();
				if (browsableAttr != null && !browsableAttr.Browsable)
				{
					continue;
				}

				// Mark read-only properties as having no setter
				var readOnlyAttr = property.FindAttribute<ReadOnlyAttribute>();
				if (readOnlyAttr != null && readOnlyAttr.IsReadOnly)
				{
					hasSetter = false;
				}

				var record = new PropertyRecord(property)
				{
					HasSetter = hasSetter
				};

				// Extract category from attribute, default to "Miscellaneous"
				var categoryAttr = property.FindAttribute<CategoryAttribute>();
				record.Category = categoryAttr != null ? categoryAttr.Category : DefaultCategoryName;

				records.Add(record);
			}

			// Discover all public fields using reflection
			var fields = from f in _object.GetType().GetFields() select f;
			foreach (var field in fields)
			{
				// Skip non-public and static fields
				if (!field.IsPublic || field.IsStatic)
				{
					continue;
				}

				var browsableAttr = field.FindAttribute<BrowsableAttribute>();
				if (browsableAttr != null && !browsableAttr.Browsable)
				{
					continue;
				}

				var categoryAttr = field.FindAttribute<CategoryAttribute>();

				var hasSetter = true;
				var readOnlyAttr = field.FindAttribute<ReadOnlyAttribute>();
				if (readOnlyAttr != null && readOnlyAttr.IsReadOnly)
				{
					hasSetter = false;
				}

				var record = new FieldRecord(field)
				{
					HasSetter = hasSetter,
					Category = categoryAttr != null ? categoryAttr.Category : DefaultCategoryName
				};

				records.Add(record);
			}

			// Discover attached properties if object is a Widget
			var asWidget = _object as Widget;
			if (asWidget != null && ParentType != null)
			{
				var attachedProperties = AttachedPropertiesRegistry.GetPropertiesOfType(ParentType);
				foreach (var attachedProperty in attachedProperties)
				{
					var record = new AttachedPropertyRecord(attachedProperty)
					{
						Category = attachedProperty.OwnerType.Name
					};

					records.Add(record);
				}
			}

			// Organize records by category
			for (var i = 0; i < records.Count; ++i)
			{
				var record = records[i];

				List<Record> categoryRecords;
				if (!_records.TryGetValue(record.Category, out categoryRecords))
				{
					categoryRecords = new List<Record>();
					_records[record.Category] = categoryRecords;
				}

				categoryRecords.Add(record);
			}

			// Sort properties within each category alphabetically
			foreach (var category in _records)
			{
				category.Value.Sort((a, b) => Comparer<string>.Default.Compare(a.Name, b.Name));
			}

			var ordered = _records.OrderBy(key => key.Key);

			// Fill this grid's category properties first
			var y = 0;
			List<Record> defaultCategoryRecords;
			if (_records.TryGetValue(Category, out defaultCategoryRecords))
			{
				FillSubGrid(ref y, defaultCategoryRecords);
			}

			// Only show collapsible category groups if this is the root grid (DefaultCategoryName)
			if (Category != DefaultCategoryName)
			{
				return;
			}

			// Create collapsible SubGrid widgets for each non-default category
			foreach (var category in ordered)
			{
				if (category.Key == DefaultCategoryName)
				{
					continue;
				}

				var subGrid = new SubGrid(this, Object, category.Key, category.Key, Filter, null);
				Grid.SetColumnSpan(subGrid, 2);
				Grid.SetRow(subGrid, y);

				// Skip empty categories
				if (subGrid.IsEmpty)
				{
					continue;
				}

				Children.Add(subGrid);

				// Restore expanded state from previous session if saved
				if (_expandedCategories.Contains(category.Key))
				{
					subGrid.Mark.IsPressed = true;
				}

				var rp = new Proportion(ProportionType.Auto);
				_layout.RowsProportions.Add(rp);

				y++;
			}
		}

		/// <summary>
		/// Applies the specified tree style to the property grid and all child elements.
		/// Stores the style for use when creating child widgets and categories.
		/// </summary>
		/// <param name="style">The tree style to apply.</param>
		public void ApplyPropertyGridStyle(TreeStyle style)
		{
			// Apply style to this widget and store for child widget creation
			ApplyWidgetStyle(style);

			PropertyGridStyle = style;
		}
	}
}
