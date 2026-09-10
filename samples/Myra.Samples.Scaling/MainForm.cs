using FontStashSharp;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI.Styles;

namespace Myra.Samples;

public partial class MainForm
{
	private AllWidgets _allWidgets;

	private SupersamplingSettings SupersamplingSettings { get; } = new SupersamplingSettings();
	private SDFSettings SDFSettings { get; } = new SDFSettings();

	public MainForm()
	{
		BuildUI();

		_sliderScale.ValueChangedByUser += (s, a) => UpdateScale();
		_comboTextScaling.SelectedIndexChanged += (s, a) => RecreateAllWidgets();

		_comboTextScaling.SelectedIndex = 0;

		_propertyGridParameters.PropertyChanged += (s, a) => RecreateAllWidgets();
		_comboTextureFiltering.SelectedIndexChanged += (s, a) => MyraEnvironment.TextTextureFiltering = (TextureFiltering)_comboTextureFiltering.SelectedIndex;

		UpdateScale();
		_comboTextureFiltering.SelectedIndex = 0;
	}

	private void ResetFontSettings()
	{
		FontSystemDefaults.FontRasterizationMode = FontRasterizationMode.Standard;
		FontSystemDefaults.FontResolutionFactor = null;
		FontSystemDefaults.KernelWidth = 0;
		FontSystemDefaults.KernelHeight = 0;
	}


	private void RecreateAllWidgets()
	{
		switch (_comboTextScaling.SelectedIndex)
		{
			case 0:
				ResetFontSettings();
				break;

			case 1:
				FontSystemDefaults.FontRasterizationMode = FontRasterizationMode.Standard;
				FontSystemDefaults.FontResolutionFactor = SupersamplingSettings.FontResolutionFactor;
				FontSystemDefaults.KernelWidth = SupersamplingSettings.KernelWidth;
				FontSystemDefaults.KernelHeight = SupersamplingSettings.KernelHeight;
				break;

			case 2:
				FontSystemDefaults.FontRasterizationMode = FontRasterizationMode.SDF;
				FontSystemDefaults.FontResolutionFactor = null;
				FontSystemDefaults.KernelWidth = 0;
				FontSystemDefaults.KernelHeight = 0;
				FontSystemDefaults.FixedSDFFontSize = SDFSettings.FixedFontSize;
				break;
		}

		DefaultAssets.Reset();
		Stylesheet.Current = DefaultAssets.DefaultStylesheet;

		_allWidgets = new AllWidgets
		{
			TransformOrigin = Vector2.Zero
		};

		_panelContainer.Content = _allWidgets;

		// Reset font settings so property grid fields would be created correctly
		ResetFontSettings();
		DefaultAssets.Reset();
		Stylesheet.Current = DefaultAssets.DefaultStylesheet;
		switch (_comboTextScaling.SelectedIndex)
		{
			case 0:
				_propertyGridParameters.Object = null;
				break;

			case 1:
				_propertyGridParameters.Object = SupersamplingSettings;
				break;

			case 2:
				_propertyGridParameters.Object = SDFSettings;
				break;
		}

		UpdateScale();
	}

	private void UpdateScale()
	{
		_labelScale.Text = _sliderScale.Value.ToString("0.00");
		_allWidgets.Scale = new Vector2((float)_sliderScale.Value);
	}
}