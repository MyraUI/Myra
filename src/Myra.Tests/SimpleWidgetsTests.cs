using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Xunit;

namespace Myra.Tests
{
	[Collection("Myra Tests")]
	public class SimpleWidgetsTests
	{
		[Fact]
		public void LabelTest()
		{
			var root = Utility.LoadProjectRootClone("labelWithPaddings.xmmp");

			Assert.IsType<Panel>(root);
			var panel = (Panel)root;

			Assert.Single(panel.Widgets);
			Assert.IsType<Label>(panel.Widgets[0]);
			var label = (Label)panel.Widgets[0];

			Assert.Equal("StbImageSharp", label.Text);
			Assert.Equal(new Thickness(8), label.Margin);
			Utility.AssertSolidBrush("#808000FF", label.Border);
			Assert.Equal(new Thickness(8), label.BorderThickness);
			Assert.Equal(new Thickness(16), label.Padding);
			Utility.AssertSolidBrush("#008000FF", label.Background);
		}

		[Fact]
		public void LabelTest2()
		{
			var root = Utility.LoadProjectRootClone("marginBorderPadding.xmmp");

			Assert.IsType<Panel>(root);
			var panel1 = (Panel)root;

			Utility.AssertSolidBrush("#ADD8E6FF", panel1.Background);

			Assert.Single(panel1.Widgets);
			Assert.IsType<Panel>(panel1.Widgets[0]);
			var panel2 = (Panel)panel1.Widgets[0];

			Assert.Equal(new Thickness(36), panel2.Margin);
			Utility.AssertSolidBrush("#008000", panel2.Border);
			Assert.Equal(new Thickness(12, 0), panel2.BorderThickness);
			Assert.Equal(new Thickness(20, 20, 0, 0), panel2.Padding);
			Utility.AssertSolidBrush("#FFA500FF", panel2.Background);
			Utility.AssertSolidBrush("#808000FF", panel2.OverBorder);

			Assert.Single(panel2.Widgets);
			Assert.IsType<Label>(panel2.Widgets[0]);
			var label1 = (Label)panel2.Widgets[0];

			Assert.Equal("Some Text", label1.Text);
			Utility.AssertColor("#FF0000FF", label1.TextColor);
		}

		[Fact]
		public void CheckButtonTest()
		{
			var root = Utility.LoadProjectRootClone("checkButton.xmmp");

			Assert.IsType<Panel>(root);
			var panel = (Panel)root;

			Assert.Single(panel.Widgets);
			Assert.IsType<CheckButton>(panel.Widgets[0]);
			var checkButton = (CheckButton)panel.Widgets[0];

			Assert.IsType<VerticalStackPanel>(checkButton.Content);
			var stackPanel = (VerticalStackPanel)checkButton.Content;
			Assert.Equal(3, stackPanel.Widgets.Count);

			for (var i = 1; i <= 3; ++i)
			{
				Assert.IsType<Label>(stackPanel.Widgets[i - 1]);
				var label = (Label)stackPanel.Widgets[i - 1];
				Assert.Equal("Text " + i, label.Text);
			}
		}

		[Fact]
		public void NewButtonsTest()
		{
			var root = Utility.LoadProjectRootClone("newButtons.xmmp");

			Assert.IsType<HorizontalStackPanel>(root);
			var rootPanel = (HorizontalStackPanel)root;

			Assert.Equal(2, rootPanel.Widgets.Count);
			Assert.IsType<Button>(rootPanel.Widgets[0]);
			var button = (Button)rootPanel.Widgets[0];
			Assert.Equal(new Thickness(4), button.Margin);
			Utility.AssertSolidBrush("#4BD961FF", button.Border);
			Assert.Equal(new Thickness(1), button.BorderThickness);
			Assert.Equal(new Thickness(4), button.Padding);

			Assert.IsType<VerticalStackPanel>(button.Content);
			var buttonStackPanel = (VerticalStackPanel)button.Content;
			Assert.Equal(4, buttonStackPanel.Widgets.Count);
			Assert.Equal(8, buttonStackPanel.Spacing);

			for (var i = 1; i <= buttonStackPanel.Widgets.Count; ++i)
			{
				Assert.IsType<Label>(buttonStackPanel.Widgets[i - 1]);
				var label = (Label)buttonStackPanel.Widgets[i - 1];
				Assert.Equal("Test" + i, label.Text);
			}

			Assert.IsType<ToggleButton>(rootPanel.Widgets[1]);
			var toggleButton = (ToggleButton)rootPanel.Widgets[1];
			Assert.Equal(new Thickness(4), toggleButton.Margin);
			Utility.AssertSolidBrush("#4BD961FF", toggleButton.Border);
			Assert.Equal(new Thickness(1), toggleButton.BorderThickness);
			Assert.Equal(new Thickness(4), toggleButton.Padding);

			Assert.IsType<VerticalStackPanel>(toggleButton.Content);
			var toggleButtonStackPanel = (VerticalStackPanel)toggleButton.Content;

			Assert.Equal(8, toggleButtonStackPanel.Spacing);
			Assert.Equal(4, toggleButtonStackPanel.Widgets.Count);
			for (var i = 1; i <= toggleButtonStackPanel.Widgets.Count; ++i)
			{
				Assert.IsType<Label>(toggleButtonStackPanel.Widgets[i - 1]);
				var label = (Label)toggleButtonStackPanel.Widgets[i - 1];
				Assert.Equal("Test" + i, label.Text);
			}
		}

		[Fact]
		public void TextBoxTest()
		{
			var root = Utility.LoadProjectRootClone("scrolledTextField.xmmp");

			Assert.IsType<Panel>(root);
			var panel = (Panel)root;

			Assert.Single(panel.Widgets);
			Assert.IsType<TextBox>(panel.Widgets[0]);
			var textBox = (TextBox)panel.Widgets[0];

			Assert.Equal(HorizontalAlignment.Center, textBox.HorizontalAlignment);
			Assert.Equal(VerticalAlignment.Center, textBox.VerticalAlignment);
			Assert.Equal(Dimension.Pixel(100), textBox.Width);
		}
	}
}



