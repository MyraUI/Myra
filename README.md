## Overview
[![Nuget](https://img.shields.io/nuget/dt/Myra)](https://www.nuget.org/packages/Myra/)
[![Build & Publish Beta](https://github.com/rds1983/Myra/actions/workflows/build-and-publish-beta.yml/badge.svg)](https://github.com/rds1983/Myra/actions/workflows/build-and-publish-beta.yml)
[![Chat](https://img.shields.io/discord/628186029488340992.svg)](https://discord.gg/ZeHxhCY)

Myra is a UI library for [MonoGame](http://www.monogame.net/), [FNA](https://github.com/FNA-XNA/FNA), and [Stride](https://github.com/stride3d/stride).  

## Features
* **Rich Set of Widgets.** Myra has the following widgets: Label, Button, ToggleButton, CheckButton, RadioButton, TextBox, Image, Separator, ComboView, ListView, Menu, TabControl, Slider, ProgressBar, SpinButton, Grid, HorizontalStackPanel, VerticalStackPanel, Panel, ScrollViewer, SplitPane (with arbitrary number of splitters), TreeView, Window, Dialog, FileDialog, ColorPickerDialog, PropertyGrid.
* **MML (Myra Markup Language).** An XML-based declarative language to describe UI ([example](/samples/Myra.Samples.AllWidgets/allControls.xmmp)).
* **Skinning.** The default skin (borrowed from [VisUI](https://github.com/kotcrab/vis-ui)) can be replaced with a custom skin loaded from XML ([example](/samples/Myra.Samples.CustomUIStylesheet/Resources/ui_stylesheet.xmms)).
* **MyraPad.** Standalone WYSIWYG MML based UI designer.
* **Myra.PlatformAgnostic.** Version of the library that could be used in any C# game engine.
* **Myra.Mcp.** Headless [MCP](https://modelcontextprotocol.io) server so AI coding agents (Claude Code, Codex) can validate, read, and save Myra `.xmmp` layouts through the real engine, plus discover the widget vocabulary. See [src/Myra.Mcp/README.md](/src/Myra.Mcp/README.md).

## Documentation
[https://myraui.github.io/Myra/](https://myraui.github.io/Myra/)

## Support
Use the following resources if you need help with Myra or have any questions:
* [Myra Discord](https://discord.gg/ZeHxhCY)

## Building From Source
1. Clone this repository.
2. Open a solution from the "build" folder.

## Sponsor
If this project is useful for you, you can support development:
- Boosty: https://boosty.to/rds1983
- Telegram Wallet: https://t.me/rds1983

### Crypto
USDT (TON): `UQCQy6tFInPvqinE44zHY4R0rYS3niaBikkqiSyGmyoAMwyO`

TON: `UQCQy6tFInPvqinE44zHY4R0rYS3niaBikkqiSyGmyoAMwyO`

## Gallery
All Widgets Sample
![](/images/AllWidgetsSample.png)

Commodore 64 Skin
![](/images/CustomStylesheetSample.png)

MyraPad
![](/images/MyraPad.png)

## Credits
* [MonoGame](http://www.monogame.net/)
* [FNA](https://github.com/FNA-XNA/FNA)
* [Stride](https://github.com/stride3d/stride)
* [MonoGame.Extended](https://github.com/craftworkgames/MonoGame.Extended)
* [VisUI](https://github.com/kotcrab/vis-editor/wiki/VisUI)
* [LibGDX](http://libgdx.badlogicgames.com/)
* [Cyotek.Drawing.BitmapFont](https://github.com/cyotek/Cyotek.Drawing.BitmapFont)
* [stb](https://github.com/nothings/stb)
* [TextCopy](https://github.com/SimonCropp/TextCopy)
