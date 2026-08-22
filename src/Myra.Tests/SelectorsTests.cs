using AssetManagementBase;
using Myra.Graphics2D.UI;
using Xunit;

namespace Myra.Tests
{
	[Collection("Myra Tests")]
	public class SelectorsTests
	{
		[Fact]
		public void ListViewTest()
		{
			var root = Utility.LoadProjectRootClone("listView.xmmp");

			Assert.IsType<Panel>(root);
			var panel = (Panel)root;

			Assert.Single(panel.Widgets);
			Assert.IsType<ListView>(panel.Widgets[0]);
			var listView = (ListView)panel.Widgets[0];

			Assert.Equal(Dimension.Pixel(200), listView.Width);
			Assert.Equal(Dimension.Pixel(200), listView.Height);
			Assert.Equal(HorizontalAlignment.Center, listView.HorizontalAlignment);
			Assert.Equal(VerticalAlignment.Center, listView.VerticalAlignment);
			Assert.Equal(6, listView.Widgets.Count);

			Assert.IsType<Label>(listView.Widgets[0]);
			var label1 = (Label)listView.Widgets[0];
			Assert.Equal("test", label1.Text);

			Assert.IsType<HorizontalSeparator>(listView.Widgets[1]);

			Assert.IsType<Label>(listView.Widgets[2]);
			var label2 = (Label)listView.Widgets[2];
			Assert.Equal("test2", label2.Text);

			Assert.IsType<VerticalStackPanel>(listView.Widgets[3]);
			var verticalStackPanel1 = (VerticalStackPanel)listView.Widgets[3];
			Assert.Equal(2, verticalStackPanel1.Widgets.Count);

			Assert.IsType<Label>(verticalStackPanel1.Widgets[0]);
			var label3 = (Label)verticalStackPanel1.Widgets[0];
			Assert.Equal("test3", label3.Text);

			Assert.IsType<Label>(verticalStackPanel1.Widgets[1]);
			var label4 = (Label)verticalStackPanel1.Widgets[1];
			Assert.Equal("test4", label4.Text);

			Assert.IsType<HorizontalSeparator>(listView.Widgets[4]);

			Assert.IsType<VerticalStackPanel>(listView.Widgets[5]);
			var verticalStackPanel2 = (VerticalStackPanel)listView.Widgets[5];
			Assert.Equal(3, verticalStackPanel2.Widgets.Count);

			Assert.IsType<Label>(verticalStackPanel2.Widgets[0]);
			var label5 = (Label)verticalStackPanel2.Widgets[0];
			Assert.Equal("test5", label5.Text);

			Assert.IsType<Label>(verticalStackPanel2.Widgets[1]);
			var label6 = (Label)verticalStackPanel2.Widgets[1];
			Assert.Equal("test6", label6.Text);

			Assert.IsType<Label>(verticalStackPanel2.Widgets[2]);
			var label7 = (Label)verticalStackPanel2.Widgets[2];
			Assert.Equal("test7", label7.Text);
		}

		[Fact]
		public void ComboViewTest()
		{
			var assetManager = Utility.CreateAssetManager();
			var project = assetManager.LoadProject("comboView.xmmp");
			var root = project.Root;

			Assert.IsType<Panel>(root);
			var panel = (Panel)root;

			Assert.Single(panel.Widgets);
			Assert.IsType<ComboView>(panel.Widgets[0]);
			var comboView = (ComboView)panel.Widgets[0];

			Assert.Equal(4, comboView.Widgets.Count);

			Assert.IsType<Label>(comboView.Widgets[0]);
			var label1 = (Label)comboView.Widgets[0];
			Assert.Equal("Test", label1.Text);

			Assert.IsType<HorizontalStackPanel>(comboView.Widgets[1]);
			var horizontalStackPanel = (HorizontalStackPanel)comboView.Widgets[1];
			Assert.Equal(2, horizontalStackPanel.Widgets.Count);

			Assert.IsType<Image>(horizontalStackPanel.Widgets[0]);
			var image1 = (Image)horizontalStackPanel.Widgets[0];
			Assert.Equal(Dimension.Pixel(16), image1.Width);
			Assert.Equal(Dimension.Pixel(16), image1.Height);
			Assert.NotNull(image1.Renderable);
			Assert.Equal(64, image1.Renderable.Size.X);
			Assert.Equal(64, image1.Renderable.Size.Y);

			Assert.IsType<Label>(horizontalStackPanel.Widgets[1]);
			var label2 = (Label)horizontalStackPanel.Widgets[1];
			Assert.Equal("Test2", label2.Text);

			Assert.IsType<HorizontalSeparator>(comboView.Widgets[2]);

			Assert.IsType<Label>(comboView.Widgets[3]);
			var label3 = (Label)comboView.Widgets[3];
			Assert.Equal("Test3", label3.Text);
		}
	}
}



