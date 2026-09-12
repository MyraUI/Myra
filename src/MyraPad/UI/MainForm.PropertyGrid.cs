using FontStashSharp;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Properties;
using Myra.Graphics2D.UI.Styles;
using Myra.MML;
using System.Collections.Generic;

namespace MyraPad.UI;

partial class MainForm
{
	/// <summary>
	/// Reference to the property grid UI control for inspecting widget properties
	/// </summary>
	private PropertyGrid PropertyGrid => _propertyGrid;

	/// <summary>
	/// The settings object for the property grid, including asset manager and base path
	/// </summary>
	private PropertyGridSettings PropertyGridSettings
	{
		get
		{
			return PropertyGrid.Settings;
		}
	}

	// Provides custom style name values for the property grid based on the current stylesheet
	private CustomValues RecordValuesProvider(object obj, Record record)
	{
		if (record.Name != "StyleName")
		{
			// Default processing
			return null;
		}

		var widget = PropertyGrid.Object as Widget;
		if (widget == null)
		{
			return null;
		}

		var stylesDict = widget.GetStylesDictionary(Project.Stylesheet);
		if (stylesDict == null)
		{
			return null;
		}

		var stylesList = new List<string>();
		foreach (var key in stylesDict.Keys)
		{
			stylesList.Add(key.ToString());
		}

		var styleNames = stylesList.ToArray(); ;
		if (styleNames == null || styleNames.Length < 2)
		{
			// Dont show this property if there's only one style(Default) or less
			styleNames = new string[0];
		}

		var values = new List<CustomValue>();
		int? selectedIndex = null;
		var val = (string)record.GetValue(obj);
		for (var i = 0; i < styleNames.Length; ++i)
		{
			var styleName = styleNames[i];

			values.Add(new CustomValue(styleName, styleName));

			if (styleName == val)
			{
				selectedIndex = i;
			}
		}

		return new CustomValues(values)
		{
			SelectedIndex = selectedIndex
		};
	}

	// Applies the selected style to the widget when the StyleName property is changed
	private bool RecordSetter(Record record, object obj, object value)
	{
		if (record.Name != "StyleName")
		{
			// Default processing
			return false;
		}

		var widget = obj as Widget;
		if (widget == null)
		{
			return false;
		}

		widget.SetStyle(Project.Stylesheet, (string)value);

		return true;
	}

	private Widget CreateImageEditor(Record record, object obj)
	{
		const int previewHeight = 16;

		var panel = new HorizontalStackPanel
		{
			Spacing = 8
		};

		// Color preview swatch
		var image = new Image
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
			Height = previewHeight
		};

		StackPanel.SetProportionType(image, ProportionType.Fill);
		panel.Widgets.Add(image);

		var value = record.GetValue(obj);
		var asImage = value as IImage;
		if (asImage != null)
		{
			image.Renderable = asImage;

			if (record.Type != typeof(IBrush))
			{
				// It's image; keep aspect ratio
				image.HorizontalAlignment = HorizontalAlignment.Center;

				var aspectRatio = (float)asImage.Size.X / asImage.Size.Y;
				image.Width = (int)(aspectRatio * previewHeight);
			}
		}
		else
		{
			var asHasColor = value as IHasColor;
			if (asHasColor != null)
			{
				image.Renderable = Stylesheet.Current.WhiteRegion;
				image.Color = asHasColor.Color;
			}
		}

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
		panel.Widgets.Add(button);

		button.Click += (s, a) =>
		{
			var value = (IBrush)record.GetValue(obj);
			var dlg = new ImageEditorDialog
			{
				Image = value
			};

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				if (dlg.Image == null || record.Type.IsAssignableFrom(dlg.Image.GetType()))
				{
					// This code skips setting new value if dlg.Image is IBrush and the property excepts IImage
					record.SetValue(obj, dlg.Image);

					_propertyGrid.Rebuild();
					OnPropertyChanged();
				}
			};

			dlg.ShowModal(Desktop);
		};

		return panel;
	}

	private Widget CreateFontEditor(Record record, object obj)
	{
		var value = (SpriteFontBase)record.GetValue(obj);

		var panel = new HorizontalStackPanel
		{
			Spacing = 8
		};

		// Color preview swatch
		var textFont = new TextBox
		{
			Readonly = true,
			Text = value?.ToString()
		};

		StackPanel.SetProportionType(textFont, ProportionType.Fill);
		panel.Widgets.Add(textFont);

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
		panel.Widgets.Add(button);

		button.Click += (s, a) =>
		{
			var value = (SpriteFontBase)record.GetValue(obj);
			var dlg = new FontEditorDialog
			{
				Font = value
			};

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				record.SetValue(obj, dlg.Font);
				_propertyGrid.Rebuild();
				OnPropertyChanged();
			};

			dlg.ShowModal(Desktop);
		};

		return panel;
	}

	private Widget CreateCustomEditor(Record record, object obj)
	{
		if (typeof(IBrush).IsAssignableFrom(record.Type))
		{
			return CreateImageEditor(record, obj);
		}

		if (typeof(SpriteFontBase).IsAssignableFrom(record.Type))
		{
			return CreateFontEditor(record, obj);
		}

		return null;
	}
}
