using AssetManagementBase;
using Microsoft.Xna.Framework;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI.ColorPicker;
using Myra.Graphics2D.UI.File;
using Myra.MML;
using System;

namespace MyraPad.UI
{
	public partial class ImageEditorDialog
	{
		private IBrush _image;

		public IBrush Image
		{
			get => _image;

			set
			{
				_image = value;
				OnImageSet();
			}
		}

		private Color Color
		{
			get
			{
				if (_image == null)
				{
					return Color.Transparent;
				}

				var hasColor = _image as IHasColor;
				if (hasColor != null)
				{
					return hasColor.Color;
				}

				return Color.White;
			}
		}


		public ImageEditorDialog()
		{
			BuildUI();

			_buttonChangeColor.Click += _buttonChangeColor_Click;
			_buttomSetFromStylesheet.Click += _buttomSetFromStylesheet_Click;
			_buttonSetFromFile.Click += _buttonSetFromFile_Click;
		}

		private void _buttonChangeColor_Click(object sender, MyraEventArgs e)
		{
			var dlg = new ColorPickerDialog()
			{
				Color = Color
			};

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				do
				{
					if (_image == null)
					{
						Image = new SolidBrush(dlg.Color);
						break;
					}

					var asSolidBrush = _image as SolidBrush;
					if (asSolidBrush != null)
					{
						Image = new SolidBrush(dlg.Color);
						break;
					}

					var asTextureRegion = _image as TextureRegion;
					if (asTextureRegion != null)
					{
						Image = new TintedRegion(asTextureRegion, dlg.Color);
						break;
					}

					var asTinted = _image as TintedRegion;
					if (asTinted != null)
					{
						Image = new TintedRegion(asTinted.Region, dlg.Color);
						break;
					}

					throw new Exception($"Could not set Color for type {_image.GetType().Name}");
				} while (false);
			};

			dlg.ShowModal(Desktop);
		}

		private void _buttomSetFromStylesheet_Click(object sender, MyraEventArgs e)
		{
			var dlg = new ChooseTextureRegionDialog();

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				Image = dlg.Image;
			};

			dlg.ShowModal(Desktop);
		}

		private void _buttonSetFromFile_Click(object sender, MyraEventArgs e)
		{
			var dlg = new FileDialog(FileDialogMode.OpenFile)
			{
				Filter = "*.png|*.jpg|*.bmp|*.gif|*.dds"
			};

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				// We need to prepend AMB path rooted symbol since we're using absolute path
				// Otherwise this code will crash on Linux
				var image = Studio.AssetManager.LoadTexture2D(MyraEnvironment.GraphicsDevice, "@" + dlg.FilePath);
				Image = new TextureRegion(image)
				{
					Name = dlg.FilePath
				};
			};

			dlg.ShowModal(Desktop);
		}

		private void OnImageSet()
		{
			if (_image != null)
			{
				_textValue.Text = _image.ToString();
			}
			else
			{
				_textValue.Text = string.Empty;
			}

			_panelColor.Background = new SolidBrush(Color);
		}
	}
}