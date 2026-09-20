### Overview
This entry describes in detail how Myra deals with images.

### IBrush
[IBrush](https://github.com/rds1983/Myra/blob/master/src/Myra/Graphics2D/IBrush.cs) represents something that can draw itself in the specified rectangle with the specified color:
```c#
  public interface IBrush
  {
    void Draw(RenderContext context, Rectangle dest, Color color);
  }
```

Many widget properties, such as Widget.Background or Menu.SelectionBackground, have the IBrush type.
The simplest implementation of IBrush is [SolidBrush](https://github.com/rds1983/Myra/blob/master/src/Myra/Graphics2D/Brushes/SolidBrush.cs).

The following code sets a SolidBrush as widget.Background:
```c#
  widget.Background = new SolidBrush(Color.Red); // SolidBrush from Color
  widget.Background = new SolidBrush("#808000FF"); // SolidBrush from RGBA string
  widget.Background = new SolidBrush("#FFA500"); // SolidBrush from RGB string
```
It can also be set through [MML](MML.md).

The following MML:
```xml
<Project>
  <Panel>
    <HorizontalStackPanel Spacing="8">
      <Panel Width="100" Height="50" Background="#FF0000FF" />
      <Label Text="Label" Background="#0000FF" />
      <Button Background="YellowGreen">
        <Label Text="Push Me" />
      </Button>
    </HorizontalStackPanel>
  </Panel>
</Project>
```
Would result in the following image:

![alt text](~/images/images.png)

### Rounded Corners
[BrushFactory](https://github.com/MyraUI/Myra/blob/master/src/Myra/Graphics2D/Brushes/BrushFactory.cs) generates procedural brushes with rounded corners.

Both factory methods return an IBrush based on a [NinePatchRegion](#ninepatchregion), so the corners always keep their shape (the region's border is set to the radius).

#### CreateSolidRoundedRect
Creates a brush filled entirely with the given color:
```c#
// 64x64 px source texture, 16 px corner radius, solid blue background
widget.Background = BrushFactory.CreateSolidRoundedRect(color: new Color(48, 120, 220));
```

#### CreateHollowRoundedRect
Creates a brush with a border (stroke) of the given width around it plus a fill color:
```c#
// 64x64 px source texture, 16 px corner radius,
// 4 px blue border with a semi-transparent blue interior
widget.Background = BrushFactory.CreateHollowRoundedRect(radius: 16, borderWidth: 4,
                                                         color: new Color(48, 120, 220),
                                                         fillColor: new Color(48, 120, 220, 120));
```

Both `radius` and `borderWidth` are in source-texture pixels, so scale them (along with `size`) if you need high resolution on high-DPI displays.

### IImage
[IImage](https://github.com/rds1983/Myra/blob/master/src/Myra/Graphics2D/IImage.cs) extends IBrush with Size property:
```c#
  public interface IImage: IBrush
  {
    Point Size { get; }
  }
```
Widgets properties such as Image.Renderable or TextBox.Cursor have IImage type.

Myra provides 2 IImage implementation: TextureRegion and NinePatchRegion.

### TextureRegion
[TextureRegion](https://github.com/rds1983/Myra/blob/master/src/Myra/Graphics2D/TextureAtlases/TextureRegion.cs) describes rectangle in the texture. TextureRegion implements IImage. 

Therefore following code will work:
```c#
// 'texture' is object of type Texture2D
image.Renderable = new TextureRegion(texture, new Rectangle(10, 10, 50, 50));
```

Also following:
```c#
// If 2nd parameter is omitted, then TextureRegion covers the whole texture
image.Renderable = new TextureRegion(texture);
```
  _**Note**. It's also possible to use TextureRegion as IBrush. However usually it won't make much sense, since it would result in the TextureRegion stretched over the rectangle IBrush is drawn at._

### NinePatchRegion
[NinePatchRegion](https://github.com/rds1983/Myra/blob/master/src/Myra/Graphics2D/TextureAtlases/NinePatchRegion.cs) is an IImage that uses the **nine-patch** technique: it can be stretched to any size while keeping its corners and borders looking exactly like in the source texture.

An ordinary [TextureRegion](#textureregion) stretches every pixel uniformly, which distorts images whose look depends on their edges - a rounded button becomes an oval, a 1px border becomes 2px thick. Nine-patch fixes this by dividing the region into a 3×3 grid:

```
+-------+------+-------+
| corner| edge | corner|
+-------+------+-------+
| edge  |center| edge  |
+-------+------+-------+
| corner| edge | corner|
+-------+------+-------+
```

The four **corners** are always drawn at original size, the four **edges** stretch along one axis only (top/bottom horizontally, left/right vertically), and the **center** stretches along both. This keeps smooth corners and borders crisp at any size, which is why all Myra widget backgrounds use it.

The `Thickness` parameter defines that border, in **source-texture pixels**: `Left`/`Right` set the width of left/right columns, `Top`/`Bottom` the height of top/bottom rows. Everything inside those borders (the middle column `Width - Left - Right` by the middle row `Height - Top - Bottom`) is what actually stretches. So a corner with `Left = 4, Top = 4` stays 4×4 pixels no matter the destination size.

```c#
widget.Background = new NinePatchRegion(texture, new Rectangle(10, 10, 50, 50), 
                                        new Thickness {Left = 2, Right = 2, 
                                                       Top = 2, Bottom = 2});
```

### TextureRegionAtlas
[TextureRegionAtlas](https://github.com/rds1983/Myra/blob/master/src/Myra/Graphics2D/TextureAtlases/TextureRegionAtlas.cs) is collection of texture regions(each could be nine patch) accessible by string key.

It could be loaded from [MyraTexturePacker](https://github.com/rds1983/MyraTexturePacker) format(.xmat) using following code:
```c#
// 'data' is string containing contents of .atlas file
// 'textures' is dictionary that maps texture file names to actual textures
TextureRegionAtlas spriteSheet = TextureRegionAtlas.Load(data, name => textures[name]);
```