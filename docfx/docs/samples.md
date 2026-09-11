## Myra Samples

Myra includes a collection of sample projects that demonstrate various features and capabilities of the framework.

### AllWidgets
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.AllWidgets)

![AllWidgets Sample](~/images/samples/AllWidgets.png)

Demonstrates all available built-in Myra widgets including buttons, checkboxes, radio buttons, text boxes, trees, grids, and dialogs.

### CustomUIStylesheet
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.CustomUIStylesheet)

![CustomUIStylesheet Sample](~/images/samples/CustomUIStylesheet.png)

Shows how to create and apply a custom UI stylesheet to customize the appearance of Myra widgets.

### DebugConsole
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.DebugConsole)

![DebugConsole Sample](~/images/samples/DebugConsole.png)

Shows how to implement an in-game debug console for testing and development.

### GridContainer
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.GridContainer)

![GridContainer Sample](~/images/samples/GridContainer.png)

Demonstrates the use of grid-based layout containers for organizing UI elements in rows and columns.

### SplitPaneContainer
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.SplitPaneContainer)

![SplitPaneContainer Sample](~/images/samples/SplitPaneContainer.png)

Shows how to implement resizable split panes for dividing UI space between multiple content areas.

### NonModalWindows
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.NonModalWindows)

![NonModalWindows Sample](~/images/samples/NonModalWindows.png)

Demonstrates the creation and management of multiple non-modal windows that don't block interaction with other UI elements.

### AssetManagement
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.AssetManagement)

![AssetManagement Sample](~/images/samples/AssetManagement.png)

Demonstrates how to manage and load assets (textures, fonts) within Myra applications.

### ObjectEditor
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.ObjectEditor)

![ObjectEditor Sample](~/images/samples/ObjectEditor.png)

Shows how to create a property editor UI for editing object properties, useful for tools and debugging utilities.

### Notepad
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.Notepad)

![Notepad Sample](~/images/samples/Notepad.png)

A practical example of building a text editor application with file operations, including open, save, and edit functionality.

### Scaling
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.Scaling)

![Scaling Sample](~/images/samples/Scaling.png)

Demonstrates how to scale the Myra UI. A slider changes the scale of the widget tree via the `Scale` property, while a "Text Scaling" combo switches between different text rasterization modes (standard, high-resolution with supersampling, and SDF) to keep text crisp when rendering at higher scales. The selected mode's parameters can be tweaked live through a property grid. The sample also shows configuring text and image texture filtering to control visual quality, and a stylesheet combo switching between the `Default` and `Default2X` asset sets for comparing high-resolution assets under scaling.

### Viewports
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.Viewports)

![Viewports Sample](~/images/samples/Viewports.png)

Demonstrates the use of viewport-based rendering for displaying content in constrained areas of the UI.

### DataGrid
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.DataGrid)

![DataGrid Sample](~/images/samples/DataGrid.png)

This sample demonstrates the `DataGrid` widget with a large CSV data set. It loads 10,000 customer records and displays them in a scrollable, sortable, and filterable grid. The sample showcases column definitions, data binding via reflection, header-click sorting, per-column filtering, row selection with inline property editing, interactive column resizing with a fill-column option, and toggling grid features (grid lines, resizable columns, header visibility, index column, sortable headers, and filtering) through a top panel with checkboxes. Edits to the selected record in the `PropertyGrid` are reflected immediately in the data grid via `InvalidateDataRow`.

### Arrow
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.CustomWidgets.Arrow)

![Arrow Sample](~/images/samples/CustomWidgets.Arrow.png)

This sample implements a custom `Arrow` widget that draws arrows (left or right direction) using the RenderContext's `DrawLine` and `DrawPolygon` methods.

### LogView
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.CustomWidgets.LogView)

![LogView Sample](~/images/samples/CustomWidgets.LogView.png)

This sample implements a `LogView` widget that extends `ScrollViewer` and contains a `VerticalStackPanel` filled with `Label` widgets. It shows how to compose custom widgets from existing built-in widgets and manage dynamic content with scrolling and animation.

### Scene3D
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.CustomWidgets.Scene3D)

![Scene3D Sample](~/images/samples/CustomWidgets.Scene3D.png)

This sample implements a `Scene3D` widget that performs 3D rendering using XNA Framework's `BasicEffect`. It handles mesh rendering, lighting, texture mapping, and manages 3D graphics state within the widget's render context, allowing seamless integration of 3D content into the Myra UI.

### SkiaSharp
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.CustomWidgets.SkiaSharp)

![SkiaSharp Sample](~/images/samples/CustomWidgets.SkiaSharp.png)

This sample demonstrates integrating SkiaSharp with Myra to create a custom widget that renders SkiaSharp 2D graphics within the Myra UI framework. The `SKCanvasWidget` bridges SkiaSharp's powerful vector graphics capabilities with Myra's widget system, allowing developers to use SkiaSharp's drawing API (circles, text, paths, etc.) to render content inside Myra widgets. The sample shows a split-pane layout with a SkiaSharp-rendered canvas alongside a text box, illustrating how third-party graphics libraries can be seamlessly integrated into Myra applications.

### ScottPlot
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.CustomWidgets.ScottPlot)

![ScottPlot Sample](~/images/samples/CustomWidgets.ScottPlot.png)

This sample demonstrates integrating ScottPlot, a .NET charting library, with Myra to create interactive data visualization widgets. It introduces a `MyraPlot` widget that inherits from `SKCanvasWidget` and implements ScottPlot's `IPlotControl` interface, providing full interactive features including mouse/keyboard input processing, context menus, and plot manipulation. The sample displays a signal plot with interactive features alongside a text input, showing how data visualization libraries can be seamlessly embedded into Myra applications for building dashboards and analytical tools.

### NvgSharp
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.CustomWidgets.NvgSharp)

![NvgSharp Sample](~/images/samples/CustomWidgets.NvgSharp.png)

This sample demonstrates integrating NvgSharp with Myra to create a custom widget that renders NanoVG-style 2D vector graphics within the Myra UI framework. The `NvgCanvasWidget` bridges NvgSharp's vector drawing capabilities with Myra's widget system, allowing developers to use NanoVG's drawing API for shapes, gradients, text, and images inside Myra widgets. The sample displays a split-pane layout with an NvgSharp-rendered canvas alongside interactive controls, showing how third-party vector graphics libraries can be seamlessly integrated into Myra applications.

### TextureAtlasViewer
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.TextureAtlasViewer)

![TextureAtlasViewer Sample](~/images/samples/TextureAtlasViewer.png)

This sample loads a texture atlas (`.xmat`) from the command line and displays every region in a `DataGrid`. It demonstrates the built-in image and check box column types: each row shows the region rendered as a thumbnail via `DataGridImageColumn`, its size as text, whether it is a nine-patch region via a read-only `DataGridCheckBoxColumn`, and its name. The grid occupies the left half of the window with a fixed width, while a detail panel on the right displays the image of the selected row, updated through the grid's `SelectedIndexChanged` event.

### PlatformAgnostic Family

The PlatformAgnostic samples demonstrate how to use Myra.PlatformAgnostic, a cross-platform graphics abstraction layer. Multiple backend implementations are provided to show integration with different graphics frameworks.

#### PlatformAgnostic.MonoGame
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.PlatformAgnostic.MonoGame)

Demonstrates using Myra.PlatformAgnostic with MonoGame Framework, enabling cross-platform desktop graphics with a familiar game development API.

#### PlatformAgnostic.Raylib
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.PlatformAgnostic.Raylib)

Demonstrates using Myra.PlatformAgnostic with Raylib, a lightweight and cross-platform graphics library that simplifies low-level graphics programming.

#### PlatformAgnostic.OpenTK
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.PlatformAgnostic.OpenTK)

Demonstrates using Myra.PlatformAgnostic with OpenTK, a C# binding for OpenGL providing direct and efficient access to graphics APIs.

#### PlatformAgnostic.Silk.NET
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.PlatformAgnostic.Silk.NET)

Demonstrates using Myra.PlatformAgnostic with Silk.NET, a modern OpenGL/Vulkan binding for .NET, providing low-level control and cross-platform graphics capabilities.

#### PlatformAgnostic.Silk.NET.TrippyGL
[View on GitHub](https://github.com/MyraUI/Myra/tree/master/samples/Myra.Samples.PlatformAgnostic.Silk.NET.TrippyGL)

An advanced example using Silk.NET with TrippyGL for specialized graphics rendering effects and advanced 3D techniques.
