# Overview
The Desktop class has the following API that can be used to show context menus:
```c#
  public void ShowContextMenu(Widget menu, Point position);
  public void HideContextMenu();
```

# Usage
The following code demonstrates how to use the context menu:
```c#
private void ShowContextMenu()
{
    if (Desktop.ContextMenu != null)
    {
        // Don't show if it's already shown
        return;
    }

    var container = new VerticalStackPanel
    {
        Spacing = 4
    };

    var titleContainer = new Panel
    {
        Background = DefaultAssets.DefaultStylesheet.ButtonStyle.Background,
    };

    var titleLabel = new Label
    {
        Text = "Choose Option",
        HorizontalAlignment = HorizontalAlignment.Center
    };

    titleContainer.Widgets.Add(titleLabel);
    container.Widgets.Add(titleContainer);

    var menuItem1 = new MenuItem();
    menuItem1.Text = "Start New Game";
    menuItem1.Selected += (s, a) =>
    {
        // "Start New Game" selected
    };

    var menuItem2 = new MenuItem();
    menuItem2.Text = "Options";

    var menuItem3 = new MenuItem();
    menuItem3.Text = "Quit";

    var verticalMenu = new VerticalMenu();

    verticalMenu.Items.Add(menuItem1);
    verticalMenu.Items.Add(menuItem2);
    verticalMenu.Items.Add(menuItem3);

    container.Widgets.Add(verticalMenu);

    Desktop.ShowContextMenu(container, Desktop.TouchPosition);
}

/// <summary>
/// LoadContent will be called once per game and is the place to load
/// all of your content.
/// </summary>
protected override void LoadContent()
{
    // Create a new SpriteBatch, which can be used to draw textures.
    spriteBatch = new SpriteBatch(GraphicsDevice);

    // TODO: use this.Content to load your game content here
    MyraEnvironment.Game = this;

    Desktop.TouchDown += (s, a) => ShowContextMenu();
}
```

It will show the context menu wherever the click/touch will be made:

![alt text](~/images/context-menus.png)

# Desktop.ContextMenuClosing
By default a context menu is closed when a touch/click is made outside of its bounds or when _desktop.HideContextMenu is called explicitly. Event **Desktop.ContextMenuClosing** allows to change this behavior.

I.e. if we take above example, following code will prevent recreation of the context menu if a click/touch is done outside of its bounds:
```c#
    Desktop.ContextMenuClosing += (s, a) =>
    {
        if (!_desktop.ContextMenu.Bounds.Contains(_desktop.TouchPosition))
        {
            // Prevent closing if a click/touch is made outside of the existing context menu bounds
            a.Cancel = true;
        }
    };
```
