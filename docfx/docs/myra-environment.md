# MyraEnvironment

`MyraEnvironment` is a static class that provides global configuration for the Myra UI framework.
All properties are static and can be accessed as `MyraEnvironment.<PropertyName>`.

## General

| Property | Type | Description |
| --- | --- | --- |
| `Version` | `string` | Gets the version number of Myra. |
| `EventHandlingModel` | `EventHandlingStrategy` | Gets or sets the event handling strategy used for input events (event capturing or event bubbling). |
| `MouseCursorType` | `MouseCursorType` | Gets or sets the current mouse cursor type displayed. |
| `DefaultMouseCursorType` | `MouseCursorType` | Gets or sets the default mouse cursor type to display when no widget-specific cursor is active. |
| `Game` | `Game` | Gets or sets the game instance that Myra uses for rendering. Must be set before using Myra. |
| `GraphicsDevice` | `GraphicsDevice` | Gets the graphics device from the current game instance. |
| `TooltipDelayInMs` | `int` | Gets or sets the delay in milliseconds before showing a tooltip. |
| `TooltipOffset` | `Point` | Gets or sets the offset from the mouse cursor where tooltips are displayed. |
| `TooltipCreator` | `Func<Widget, Widget>` | Gets or sets the function used to create tooltip widgets. |
| `ImageTextureFiltering` | `TextureFiltering` | Gets or sets the default texture filtering mode for images. |
| `TextTextureFiltering` | `TextureFiltering` | Gets or sets the default texture filtering mode for text. |
| `SmoothText` | `bool` | Gets or sets a value indicating whether text rendering should be smoothed (especially when scaling) at the cost of performance. |
| `EnableModalDarkening` | `bool` | Gets or sets a value indicating whether modal dialogs should darken the background. |
| `DarkeningColor` | `Color` | Gets or sets the color used to darken the background when modal dialogs are displayed. |

## Input

| Property | Type | Description |
| --- | --- | --- |
| `MouseInfoGetter` | `Func<MouseInfo>` | Gets or sets the function used to retrieve the current mouse information. |
| `DownKeysGetter` | `Action<bool[]>` | Gets or sets the function used to retrieve which keys are currently pressed. |
| `DoubleClickIntervalInMs` | `int` | Gets or sets the time interval in milliseconds for double-click detection. |
| `DoubleClickRadius` | `int` | Gets or sets the pixel radius within which a second click is considered a double-click. |
| `RepeatKeyDownStartInMs` | `int` | Gets or sets the delay in milliseconds before a pressed key starts repeating. |
| `RepeatKeyDownInternalInMs` | `int` | Gets or sets the interval in milliseconds between key repeat events. |
| `HasExternalTextInput` | `bool` | Gets or sets a value indicating whether text input is handled by an external mechanism. |

## Debug

| Property | Type | Description |
| --- | --- | --- |
| `DrawWidgetsFrames` | `bool` | Gets or sets a value indicating whether to draw debug frames around all widgets. |
| `DrawKeyboardFocusedWidgetFrame` | `bool` | Gets or sets a value indicating whether to draw a debug frame around the keyboard-focused widget. |
| `DrawMouseHoveredWidgetFrame` | `bool` | Gets or sets a value indicating whether to draw a debug frame around the widget under the mouse cursor. |
| `DrawMouseHoveredWidgetInfo` | `bool` | Gets or sets a value indicating whether to draw information about the widget under the mouse cursor. |
| `DrawTextGlyphsFrames` | `bool` | Gets or sets a value indicating whether to draw debug frames around text glyphs. |
| `DisableClipping` | `bool` | Gets or sets a value indicating whether clipping is disabled (useful for debugging). |

## Example

```csharp
// Configure general behavior
MyraEnvironment.SmoothText = true;
MyraEnvironment.EnableModalDarkening = true;

// Configure rendering filtering
MyraEnvironment.ImageTextureFiltering = TextureFiltering.Nearest;
MyraEnvironment.TextTextureFiltering = TextureFiltering.Linear;

// Configure input behavior
MyraEnvironment.DoubleClickIntervalInMs = 400;
MyraEnvironment.RepeatKeyDownStartInMs = 300;

// Enable debug visualization
MyraEnvironment.DrawWidgetsFrames = true;
```
