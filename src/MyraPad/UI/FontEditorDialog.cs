using AssetManagementBase;
using FontStashSharp;
using Myra;
using Myra.Graphics2D.UI.File;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using System;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace MyraPad.UI
{
	public partial class FontEditorDialog
	{
		private const int DefaultTTFFontSize = 32;

		private SpriteFontBase _font;

		public SpriteFontBase Font
		{
			get
			{
				var dynamicFont = _font as DynamicSpriteFont;
				if (dynamicFont == null)
				{
					return _font;
				}

				if (FontSize == DefaultFontSize)
				{
					return _font;
				}

				var assetName = _font.Name;
				string parameter;
				StylesheetFont.TryGetParameter(ref assetName, out parameter);

				var project = Studio.MainForm.Project;
				return MyraAssets.LoadFont($"{assetName}{StylesheetFont.Separator}{FontSize}", project.Stylesheet, Studio.AssetManager);
			}

			set
			{
				_font = value;

				UpdateFont();
			}
		}

		private int FontSize => (int)Math.Round(_spinSize.Value.Value);

		private int DefaultFontSize => (int)Math.Round(_font.FontSize);

		public FontEditorDialog()
		{
			BuildUI();

			_spinSize.ValueChangedByUser += _spinSize_ValueChanged;
			_buttomSetFromStylesheet.Click += _buttomSetFromStylesheet_Click;
			_buttonSetFromFile.Click += _buttonSetFromFile_Click;

			UpdateFont();
		}

		private void UpdateFont()
		{
			if (_font == null)
			{
				_textValue.Text = string.Empty;
				_spinSize.TextBox.Text = string.Empty;
				_spinSize.Enabled = false;
			}
			else
			{
				_textValue.Text = _font.ToString();
				_spinSize.Value = DefaultFontSize;
				_spinSize.Enabled = _font is DynamicSpriteFont;
			}
		}

		private void _buttomSetFromStylesheet_Click(object sender, MyraEventArgs e)
		{
			var dlg = new ChooseFontDialog();

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				Font = dlg.Font.Font;
			};

			dlg.ShowModal(Desktop);
		}

		private void _spinSize_ValueChanged(object sender, ValueChangedEventArgs<float?> e)
		{
			_textValue.Text = Font.ToString();
		}

		private void _buttonSetFromFile_Click(object sender, MyraEventArgs e)
		{
			var dlg = new FileDialog(FileDialogMode.OpenFile)
			{
				Filter = "*.fnt|*.ttf"
			};

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				var path = dlg.FilePath;
				if (!string.IsNullOrEmpty(Studio.MainForm.FilePath))
				{
					path = PathUtils.TryToMakePathRelativeTo(path, Path.GetDirectoryName(Studio.MainForm.FilePath));
				}

				SpriteFontBase font;

				if (path.EndsWith(".ttf", StringComparison.InvariantCultureIgnoreCase))
				{
					// TTF font
					// We need to prepend AMB path rooted symbol since we're using absolute path
					// Otherwise this code will crash on Linux
					var fontSystem = Studio.AssetManager.LoadFontSystem("@" + path);

					font = fontSystem.GetFont(DefaultTTFFontSize);
					font.Name = $"{path}{StylesheetFont.Separator}{DefaultTTFFontSize}";
				} else
				{
					// FNT font
					// We need to prepend AMB path rooted symbol since we're using absolute path
					// Otherwise this code will crash on Linux
					font = Studio.AssetManager.LoadFont("@" + path);
					font.Name = path;
				}

				Font = font;
			};

			dlg.ShowModal(Desktop);
		}
	}
}