using AssetManagementBase;
using FontStashSharp;
using FontStashSharp.RichText;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Attributes;
using Myra.Events;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.File;
using Myra.Graphics2D.UI.Properties;
using Myra.Graphics2D.UI.Styles;
using Myra.MML;
using Myra.Utility;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MyraPad.UI
{
	/// <summary>
	/// MainForm is the main window of MyraPad UI designer. It manages the editing of Myra UI layouts through XML editing
	/// with live preview, property grid inspection, and visual widget explorer. It handles file operations (new, open, save),
	/// text editing with auto-indent/auto-close features, XML parsing for widget creation, and synchronization between
	/// the XML source code view and the visual widget hierarchy. The class uses an async queue for project loading and
	/// provides auto-complete suggestions for XML tags based on the current parent widget context.
	/// </summary>
	public partial class MainForm
	{
		// XML element names for layout proportions and menu/list items
		private const string RowsProportionsName = "RowsProportions";
		private const string ColumnsProportionsName = "ColumnsProportions";
		private const string ProportionsName = "Proportions";
		private const string MenuItemName = "MenuItem";
		private const string ListItemName = "ListItem";

		// Simple widgets that don't contain other widgets (leaf nodes)
		private static readonly Type[] SimpleWidgets = new[]
		{
			typeof(ImageTextButton),
			typeof(SpinButton),
			typeof(HorizontalProgressBar),
			typeof(VerticalProgressBar),
			typeof(HorizontalSeparator),
			typeof(VerticalSeparator),
			typeof(HorizontalSlider),
			typeof(VerticalSlider),
			typeof(Image),
			typeof(Label),
			typeof(TextBox),
			typeof(PropertyGrid),
		};

		// Container widgets that can hold other widgets as children
		private static readonly Type[] Containers = new[]
		{
			typeof(Button),
			typeof(ToggleButton),
			typeof(CheckButton),
			typeof(RadioButton),
			typeof(Grid),
			typeof(Panel),
			typeof(ScrollViewer),
			typeof(VerticalSplitPane),
			typeof(HorizontalSplitPane),
			typeof(VerticalStackPanel),
			typeof(HorizontalStackPanel),
			typeof(ListView),
			typeof(ComboView)
		};

		// Special containers like menus and tabs that have specialized child handling
		private static readonly Type[] SpecialContainers = new[]
{
			typeof(HorizontalMenu),
			typeof(VerticalMenu),
			typeof(TabControl),
		};

		// Regex pattern to extract XML tag names from opening tags like <Button or <Project.Columns>
		private static readonly Regex TagResolver = new Regex("<([A-Za-z0-9\\.]+)");

		// Queue for async project loading operations to avoid blocking the UI thread
		private readonly AsyncTasksQueue _queue = new AsyncTasksQueue();
		// Queue for UI updates that need to be processed on the main thread
		private readonly ConcurrentQueue<Action> _uiActions = new ConcurrentQueue<Action>();

		// Flags to prevent recursive refresh cycles when updating the project or explorer
		private bool _suppressProjectRefresh = false, _suppressExplorerRefresh = false;
		// Path to the currently open project file
		private string _filePath;
		// Flag indicating whether the current project has unsaved changes
		private bool _isDirty;
		// The loaded UI project containing the widget hierarchy
		private Project _project;
		// Whether the current XML tag at cursor position needs a closing tag
		private bool _needsCloseTag;
		// Name of the parent XML tag at the current cursor position
		private string _parentTag;
		// Start and end positions of the current XML tag being edited
		private int? _currentTagStart, _currentTagEnd;
		// Current line and column position of the cursor, and indent nesting level
		private int _line, _col, _indentLevel;
		// Flags to apply auto-indent when Enter is pressed and auto-close when > is typed
		private bool _applyAutoIndent = false;
		private bool _applyAutoClose = false;
		// Timestamp when the last project refresh was initiated (used for delayed refresh)
		private DateTime? _refreshInitiated;
		// Cache of loaded fonts to avoid reloading the same fonts multiple times
		private readonly Dictionary<string, FontSystem> _fontCache = new Dictionary<string, FontSystem>();
		// Cache of loaded textures to avoid reloading the same images multiple times
		private readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
		// Tree view widget for displaying the widget hierarchy in the explorer panel
		private readonly TreeView _treeViewExplorer;
		// Auto-complete context menu that appears while typing XML tags
		private VerticalMenu _autoCompleteMenu = null;
		// Flag to track if the last click in the explorer was a right-click (for context menu)
		private bool _rightClick;

		/// <summary>
		/// The file path of the currently open project; updates title bar and asset manager when changed
		/// </summary>
		public string FilePath
		{
			get
			{
				return _filePath;
			}

			set
			{
				if (value == _filePath)
				{
					return;
				}

				_filePath = value;

				if (!string.IsNullOrEmpty(_filePath))
				{
					var folder = Path.GetDirectoryName(_filePath);
					PropertyGridSettings.BasePath = folder;
					PropertyGridSettings.AssetManager = AssetManager.CreateFileAssetManager(folder);
					LastFolder = folder;
				}
				else
				{
					PropertyGridSettings.BasePath = string.Empty;
					PropertyGridSettings.AssetManager = MyraEnvironment.DefaultAssetManager;
					PropertyGridSettings.AssetManager.Cache.Clear();
				}

				UpdateTitle();
				UpdateMenuFile();
			}
		}

		/// <summary>
		/// Flag indicating unsaved changes; displays an asterisk in the title bar when true
		/// </summary>
		public bool IsDirty
		{
			get
			{
				return _isDirty;
			}

			set
			{
				if (value == _isDirty)
				{
					return;
				}

				_isDirty = value;
				UpdateTitle();
			}
		}

		/// <summary>
		/// The current loaded UI project; updates the visual preview and explorer tree when changed
		/// </summary>
		public Project Project
		{
			get
			{
				return _project;
			}

			set
			{
				if (value == _project)
				{
					return;
				}

				_project = value;

				_projectHolder.Widgets.Clear();

				if (_project != null && _project.Root != null)
				{
					_projectHolder.Widgets.Add(_project.Root);
				}

				RefreshExplorer();

				UpdateMenuFile();
			}
		}

		/// <summary>
		/// The complete XML opening tag string at the current cursor position
		/// </summary>
		private string CurrentTag
		{
			get
			{
				if (_currentTagStart == null || _currentTagEnd == null || _currentTagEnd.Value <= _currentTagStart.Value)
				{
					return null;
				}

				return _textSource.Text.Substring(_currentTagStart.Value, _currentTagEnd.Value - _currentTagStart.Value + 1);
			}
		}

		/// <summary>
		/// Reference to the property grid UI control for inspecting widget properties
		/// </summary>
		private PropertyGrid PropertyGrid => _propertyGrid;

		/// <summary>
		/// The settings object for the property grid, including asset manager and base path
		/// </summary>
		private PropertyGridSettings PropertyGridSettings
		{
			get
			{
				return PropertyGrid.Settings;
			}
		}

		/// <summary>
		/// The asset manager for loading external resources like images and fonts
		/// </summary>
		public AssetManager AssetManager
		{
			get
			{
				return PropertyGridSettings.AssetManager;
			}
		}

		/// <summary>
		/// The type of the parent widget containing the currently edited widget
		/// </summary>
		private Type ParentType
		{
			get
			{
				if (string.IsNullOrEmpty(_parentTag))
				{
					return null;
				}

				return Project.GetWidgetTypeByName(_parentTag);
			}
		}

		// The base path for resolving relative paths to rich text assets (fonts, images)
		private string BaseRichTextPath
		{
			get
			{
				var result = string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetDirectoryName(FilePath);
				if (!string.IsNullOrEmpty(Project.DesignerRtfAssetsPath))
				{
					if (string.IsNullOrEmpty(result) || Path.IsPathRooted(Project.DesignerRtfAssetsPath))
					{
						result = Project.DesignerRtfAssetsPath;
					}
					else
					{
						result = Path.Combine(result, Project.DesignerRtfAssetsPath);
					}
				}

				return result;
			}
		}

		// A newly loaded widget object to be displayed in the property grid
		public object NewObject { get; set; }
		// A newly loaded project to be displayed in the visual preview
		public Project NewProject { get; set; }

		// The index in the explorer tree of the node to select after project refresh
		public int? NewProjectSelectedNodeIndex { get; set; }

		// The last folder used in file dialogs
		public string LastFolder { get; set; }
		// User options for auto-indent, auto-close, and other editor behaviors
		public Options Options { get; }

		/// <summary>
		/// Initializes the main form UI, sets up event handlers for text editing and UI controls, and restores saved state
		/// </summary>
		public MainForm(State state)
		{
			BuildUI();

			_menuFileNew.Selected += NewItemOnClicked;
			_menuFileOpen.Selected += OpenItemOnClicked;
			_menuFileReload.Selected += OnMenuFileReloadSelected;
			_menuFileSave.Selected += SaveItemOnClicked;
			_menuFileSaveAs.Selected += SaveAsItemOnClicked;
			_menuFileExportToCS.Selected += ExportCsItemOnSelected;
			_menuFileExportToCSLight.Selected += ExportCsLightItemOnSelected;
			_menuFileLoadStylesheet.Selected += OnMenuFileLoadStylesheet;
			_menuFileResetStylesheet.Selected += OnMenuFileResetStylesheetSelected;
			_menuFileDebugOptions.Selected += DebugOptionsItemOnSelected;
			_menuFileQuit.Selected += QuitItemOnDown;

			_menuItemSelectAll.Selected += (s, a) => { _textSource.SelectAll(); };
			_menuEditFormatSource.Selected += _menuEditUpdateSource_Selected;

			_menuHelpAbout.Selected += AboutItemOnClicked;

			_textSource.CursorPositionChanged += _textSource_CursorPositionChanged;
			_textSource.TextChanged += _textSource_TextChanged;
			_textSource.KeyDown += _textSource_KeyDown;
			_textSource.Char += _textSource_Char;
			_textSource.TextDeleted += _textSource_TextDeleted;
			_textStatus.Text = string.Empty;
			_textLocation.Text = "Line: 0, Column: 0, Indent: 0";

			_textBoxFilter.TextChanged += _textBoxFilter_TextChanged;

			PropertyGrid.PropertyChanged += PropertyGridOnPropertyChanged;
			PropertyGrid.CustomValuesProvider = RecordValuesProvider;
			PropertyGrid.CustomSetter = RecordSetter;
			PropertyGrid.Settings.AssetManager = MyraEnvironment.DefaultAssetManager;

			_topSplitPane.SetSplitterPosition(0, state != null ? state.TopSplitterPosition1 : 0.2f);
			_topSplitPane.SetSplitterPosition(1, state != null ? state.TopSplitterPosition2 : 0.6f);
			_leftSplitPane.SetSplitterPosition(0, state != null ? state.CenterSplitterPosition : 0.5f);

			UpdateMenuFile();

			_treeViewExplorer = new TreeView
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
			};

			_treeViewExplorer.SelectionChanged += _treeViewExplorer_SelectionChanged;
			_treeViewExplorer.TouchDown += _treeViewExplorer_TouchDown;
			_treeViewExplorer.TouchUp += _treeViewExplorer_TouchUp;

			_panelExplorer.Content = _treeViewExplorer;

			RichTextDefaults.FontResolver = p =>
			{
				// Parse font name and size
				var args = p.Split(',');
				var fontName = args[0].Trim();
				var fontSize = int.Parse(args[1].Trim());

				// _fontCache is field of type Dictionary<string, FontSystem>
				// It is used to cache fonts
				FontSystem fontSystem;
				if (!_fontCache.TryGetValue(fontName, out fontSystem))
				{
					// Load and cache the font system
					fontSystem = new FontSystem();
					fontSystem.AddFont(File.ReadAllBytes(Path.Combine(BaseRichTextPath, fontName)));
					_fontCache[fontName] = fontSystem;
				}

				// Return the required font
				return fontSystem.GetFont(fontSize);
			};

			RichTextDefaults.ImageResolver = p =>
			{
				Texture2D texture;

				// _textureCache is field of type Dictionary<string, Texture2D>
				// it is used to cache textures
				if (!_textureCache.TryGetValue(p, out texture))
				{
					using (var stream = File.OpenRead(Path.Combine(BaseRichTextPath, p)))
					{
						texture = Texture2D.FromStream(MyraEnvironment.GraphicsDevice, stream);
					}

					_textureCache[p] = texture;
				}

				return new TextureFragment(texture);
			};

			if (state != null)
			{
				LastFolder = state.LastFolder;
				Options = state.Options;
			}
			else
			{
				Options = new Options();
			}
		}

		/// <summary>
		/// Creates a new widget of the specified type and adds it to the parent container
		/// </summary>
		private void DefaultCreate(object parent, Type t)
		{
			try
			{
				IItemWithId child;

				// Try to instantiate the widget using either parameterless or stylename constructor
				var constructor = t.GetConstructor(Type.EmptyTypes);
				if (constructor != null)
				{
					child = (IItemWithId)Activator.CreateInstance(t);
				}
				else
				{
					// Fallback to constructor that takes style name
					child = (IItemWithId)Activator.CreateInstance(t, Stylesheet.DefaultStyleName);
				}

				// Add the new widget to the parent using the appropriate collection/property
				do
				{
					var asContentControl = parent as IContent;
					if (asContentControl != null)
					{
						asContentControl.Content = (Widget)child;
						break;
					}

					var asContainer = parent as IContainer;
					if (asContainer != null)
					{
						asContainer.Widgets.Add((Widget)child);
						break;
					}

					var asMenu = parent as Menu;
					if (asMenu != null)
					{
						asMenu.Items.Add((IMenuItem)child);
						break;
					}

					var asTabControl = parent as TabControl;
					if (asTabControl != null)
					{
						asTabControl.Items.Add((TabItem)child);
						break;
					}
				}
				while (false);

				// Refresh the explorer tree to show the new widget
				RefreshExplorer();

				// Find and schedule selection of the new item in the explorer tree
				for (var i = 0; i < _treeViewExplorer.TotalNodesCount; ++i)
				{
					var node = _treeViewExplorer.GetNodeByAbsoluteIndex(i);
					if (node.Tag == child)
					{
						NewProjectSelectedNodeIndex = i;
						break;
					}
				}

				// Synchronize the text editor with the updated project structure
				_textSource.Text = _project.Save();
			}
			catch (Exception ex)
			{
				var msg = Dialog.CreateMessageBox("Error", ex.Message);
				msg.ShowModal(Desktop);
			}
		}

		private ChildCreator CreateNewItemAction(Widget parent, Type childType) => new ChildCreator(childType.Name, () => DefaultCreate(parent, childType));

		private ChildCreator[] CreateNewItemActions(Widget parent, IEnumerable<Type> childTypes)
		{
			var result = new List<ChildCreator>();

			foreach (var childType in childTypes)
			{
				result.Add(CreateNewItemAction(parent, childType));
			}

			return result.ToArray();
		}

		/// <summary>
		/// Builds a list of available child widget types that can be added to the specified parent widget
		/// </summary>
		private List<ChildCreator> BuildAddActions(Widget parent)
		{
			var result = new List<ChildCreator>();
			if (parent == null)
			{
				return result;
			}

			var widgetTypeName = parent.GetType().Name;

			// Add different widget types based on the parent's capabilities
			if (Containers.Contains(widgetTypeName) || widgetTypeName == "Window" || widgetTypeName == "Dialog")
			{
				// Containers can hold any type of child widget
				result.AddRange(CreateNewItemActions(parent, SimpleWidgets));
				result.AddRange(CreateNewItemActions(parent, Containers));
				result.AddRange(CreateNewItemActions(parent, SpecialContainers));
			}
			else if (widgetTypeName.EndsWith("Menu"))
			{
				// Menus can only contain menu items and separators
				result.Add(CreateNewItemAction(parent, typeof(MenuItem)));
				result.Add(CreateNewItemAction(parent, typeof(MenuSeparator)));
			}
			else if (widgetTypeName == "TabControl")
			{
				// TabControl can only contain TabItems
				result.Add(CreateNewItemAction(parent, typeof(TabItem)));
			}

			// Sort the results alphabetically by name
			result = result.OrderBy(s => s.Name).ToList();

			return result;
		}

		// Records whether the mouse down event was a right-click
		private void _treeViewExplorer_TouchDown(object sender, MyraEventArgs e)
		{
			var state = Mouse.GetState();

			_rightClick = state.RightButton == ButtonState.Pressed;
		}

		/// <summary>
		/// Handles right-click on the explorer tree to show a context menu for adding new widgets
		/// </summary>
		private void _treeViewExplorer_TouchUp(object sender, MyraEventArgs e)
		{
			if (!_rightClick || Desktop.ContextMenu != null)
			{
				// Don't show if a menu is already displayed
				return;
			}

			try
			{
				var selectedWidget = (Widget)_treeViewExplorer.SelectedNode.Tag;

				// Get the list of widgets that can be added to this widget
				var addActions = BuildAddActions(selectedWidget);
				if (addActions.Count == 0)
				{
					return;
				}

				var asContent = selectedWidget as IContent;

				var verticalMenu = new VerticalMenu();
				// If there are few options, show them directly in the context menu
				if (addActions.Count < 5)
				{
					var prefix = "Add ";
					if (asContent != null && asContent.Content != null)
					{
						prefix = "Replace Content With ";
					}

					// Add each action directly as a menu item
					foreach (var addAction in addActions)
					{
						var menuItem = new MenuItem
						{
							Text = prefix + addAction.Name
						};

						menuItem.Selected += (s, a) => addAction.Creator();
						verticalMenu.Items.Add(menuItem);
					}
				}
				else
				{
					// If there are many options, show a dialog to search for the desired widget
					var prefix = "Add New Widget";

					if (asContent != null && asContent.Content != null)
					{
						prefix = "Replace Content With New Widget";
					}
					var menuItem = new MenuItem
					{
						Text = prefix + "..."
					};

					menuItem.Selected += (sender, args) =>
					{
						// Display a searchable dialog for selecting the widget type
						var addNewWidgetDialog = new AddNewWidgetDialog();
						addNewWidgetDialog.Title = prefix;

						addNewWidgetDialog.SetNames((from a in addActions select a.Name).ToArray());

						addNewWidgetDialog.Closed += (s, a) =>
						{
							if (!addNewWidgetDialog.Result)
							{
								// Dialog was cancelled
								return;
							}

							// User confirmed a selection
							var addAction = addActions[addNewWidgetDialog.SelectedIndex];
							addAction.Creator();
						};

						addNewWidgetDialog.ShowModal(Desktop);
					};

					verticalMenu.Items.Add(menuItem);
				}

				Desktop.ShowContextMenu(verticalMenu, Desktop.MousePosition);
			}
			catch (Exception ex)
			{
				var msg = Dialog.CreateMessageBox("Error", ex.Message);
				msg.ShowModal(Desktop);
			}
		}

		// Initializes desktop event handlers after the form is placed on the desktop
		protected override void OnPlacedChanged()
		{
			base.OnPlacedChanged();

			if (Desktop == null)
			{
				return;
			}

			Desktop.ContextMenuClosed += Desktop_ContextMenuClosed;
			Desktop.KeyDownHandler = key =>
			{
				if (_autoCompleteMenu != null &&
					(key == Keys.Up || key == Keys.Down || key == Keys.Enter))
				{
					_autoCompleteMenu.OnKeyDown(key);
				}
				else
				{
					Desktop.OnKeyDown(key);
				}
			};

			Desktop.KeyDown += (s, a) =>
			{
				if (Desktop.HasModalWidget || _mainMenu.IsOpen)
				{
					return;
				}

				if (Desktop.IsKeyDown(Keys.LeftControl) || Desktop.IsKeyDown(Keys.RightControl))
				{
					if (Desktop.IsKeyDown(Keys.N))
					{
						NewItemOnClicked(this, MyraEventArgs.Empty);
					}
					else if (Desktop.IsKeyDown(Keys.O))
					{
						OpenItemOnClicked(this, MyraEventArgs.Empty);
					}
					else if (Desktop.IsKeyDown(Keys.R))
					{
						OnMenuFileReloadSelected(this, MyraEventArgs.Empty);
					}
					else if (Desktop.IsKeyDown(Keys.S))
					{
						SaveItemOnClicked(this, MyraEventArgs.Empty);
					}
					else if (Desktop.IsKeyDown(Keys.E))
					{
						ExportCsItemOnSelected(this, MyraEventArgs.Empty);
					}
					else if (Desktop.IsKeyDown(Keys.W))
					{
						ExportCsLightItemOnSelected(this, MyraEventArgs.Empty);
					}
					else if (Desktop.IsKeyDown(Keys.Q))
					{
						Studio.Instance.Exit();
					}
					else if (Desktop.IsKeyDown(Keys.F))
					{
						_menuEditUpdateSource_Selected(this, MyraEventArgs.Empty);
					}
				}
			};
		}

		/// <summary>
		/// Handles the window closing event; prevents close if there are unsaved changes
		/// </summary>
		public void ClosingFunction(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (_isDirty)
			{
				OnExiting();
				e.Cancel = true;
			}
		}

		/// <summary>
		/// Prompts the user to confirm exit if there are unsaved changes
		/// </summary>
		public void OnExiting()
		{
			var mb = Dialog.CreateMessageBox("Quit", "There are unsaved changes. Do you want to exit without saving?");

			mb.Closed += (o, args) =>
			{
				if (mb.Result)
				{
					Studio.Instance.Exit();
				}
			};

			mb.ShowModal(Desktop);
		}

		// Removes empty lines when they are left after deleting text on that line
		private void _textSource_TextDeleted(object _, TextDeletedEventArgs e)
		{
			if (e.Value.Contains('\n'))
			{
				return;
			}

			int startIndexOfLine = _textSource.Text.LastIndexOfSafely('\n', _textSource.CursorPosition - 2);
			var endIndexOfLine = _textSource.Text.IndexOfSafely('\n', _textSource.CursorPosition - 2);
			if (endIndexOfLine < 0)
			{
				endIndexOfLine = _textSource.Text.Length;
			}

			if (startIndexOfLine < 0)
			{
				startIndexOfLine = 0;
			}

			var currentLineString = _textSource.Text[startIndexOfLine..endIndexOfLine];
			if (currentLineString is null)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(currentLineString))
			{
				_textSource.Text = _textSource.Text.Remove(startIndexOfLine, currentLineString.Length);
				_textSource.CursorPosition = startIndexOfLine + 1;
			}
		}

		// Updates the property grid filter to show only matching properties
		private void _textBoxFilter_TextChanged(object sender, ValueChangedEventArgs<string> e)
		{
			PropertyGrid.Filter = _textBoxFilter.Text;
			_propertyGridPane.ResetScroll();
		}

		// Provides custom style name values for the property grid based on the current stylesheet
		private CustomValues RecordValuesProvider(object obj, Record record)
		{
			if (record.Name != "StyleName")
			{
				// Default processing
				return null;
			}

			var widget = PropertyGrid.Object as Widget;
			if (widget == null)
			{
				return null;
			}

			var styleNames = Project.Stylesheet.GetStylesByWidgetName(widget.GetType().Name);
			if (styleNames == null || styleNames.Length < 2)
			{
				// Dont show this property if there's only one style(Default) or less
				styleNames = new string[0];
			}

			var values = new List<CustomValue>();
			int? selectedIndex = null;
			var val = (string)record.GetValue(obj);
			for (var i = 0; i < styleNames.Length; ++i)
			{
				var styleName = styleNames[i];

				values.Add(new CustomValue(styleName, styleName));

				if (styleName == val)
				{
					selectedIndex = i;
				}
			}

			return new CustomValues(values)
			{
				SelectedIndex = selectedIndex
			};
		}

		// Applies the selected style to the widget when the StyleName property is changed
		private bool RecordSetter(Record record, object obj, object value)
		{
			if (record.Name != "StyleName")
			{
				// Default processing
				return false;
			}

			var widget = obj as Widget;
			if (widget == null)
			{
				return false;
			}

			widget.SetStyle(Project.Stylesheet, (string)value);

			return true;
		}

		// Clears the auto-complete menu reference when it is closed
		private void Desktop_ContextMenuClosed(object sender, GenericEventArgs<Widget> e)
		{
			if (e.Data != _autoCompleteMenu)
			{
				return;
			}

			_autoCompleteMenu = null;
		}

		// Refreshes the XML editor text to match the current project state
		private void UpdateSource()
		{
			var data = Project != null ? Project.Save() : string.Empty;
			if (data == _textSource.Text)
			{
				return;
			}

			_textSource.ReplaceAll(data);
		}

		// Reformats the XML source code with proper indentation and structure
		private void _menuEditUpdateSource_Selected(object sender, MyraEventArgs e)
		{
			try
			{
				var project = Project.LoadFromXml(_textSource.Text, AssetManager);
				_textSource.Text = _project.Save();
			}
			catch (Exception ex)
			{
				var messageBox = Dialog.CreateMessageBox("Error", ex.Message);
				messageBox.ShowModal(Desktop);
			}
		}

		/// <summary>
		/// Sets the flag to apply auto-close when the user types the > character
		/// </summary>
		private void _textSource_Char(object sender, GenericEventArgs<char> e)
		{
			_applyAutoClose = e.Data == '>';
		}

		/// <summary>
		/// Sets the flag to apply auto-indent when the user presses Enter
		/// </summary>
		private void _textSource_KeyDown(object sender, GenericEventArgs<Keys> e)
		{
			_applyAutoIndent = e.Data == Keys.Enter;
		}

		/// <summary>
		/// Iterates through a widget's external resources and applies the given processor function to each
		/// </summary>
		private static void ProcessResourcesPaths(Widget w, Func<string, bool> resourceProcessor)
		{
			var type = w.GetType();
			foreach (var res in w.Resources)
			{
				var propertyInfo = type.GetProperty(res.Key);
				if (propertyInfo == null)
				{
					continue;
				}

				// Skip brushes for now
				if (propertyInfo.PropertyType == typeof(IBrush))
				{
					continue;
				}

				var result = resourceProcessor(res.Key);
				if (!result)
				{
					break;
				}
			}
		}

		/// <summary>
		/// Updates resource paths in widgets to be relative to the new project location; prompts user for confirmation
		/// </summary>
		private void UpdateResourcesPaths(string oldPath, string newPath, Action<bool> onFinished)
		{
			try
			{
				// Currently only support moving from no external assets to having project-relative paths
				if (!string.IsNullOrEmpty(oldPath))
				{
					onFinished(false);
					return;
				}

				// Check if the project contains any external resources (fonts, images, etc.)
				var hasExternalResources = false;

				Project.Root.ProcessWidgets(w =>
				{
					ProcessResourcesPaths(w, k =>
					{
						// Found at least one external resource
						hasExternalResources = true;
						return false;
					});

					// Stop iterating if we found resources
					return !hasExternalResources;
				});

				if (!hasExternalResources)
				{
					onFinished(false);
					return;
				}

				// Prompt the user to confirm resource path updates
				var dialog = Dialog.CreateMessageBox("Resources Paths Update", "Would you like to update resources paths so it become relative to the project location?");
				dialog.Closed += (s, a) =>
				{
					if (dialog.Result)
					{
						var updated = false;

						var folder = Path.GetDirectoryName(newPath);
						// Iterate through all widgets and update absolute paths to relative paths
						UIUtils.ProcessWidgets(Project.Root, widget =>
						{
							var newResources = new Dictionary<string, string>();

							ProcessResourcesPaths(widget, key =>
							{
								try
								{
									var path = widget.Resources[key];

									// Only convert absolute paths to relative
									if (Path.IsPathRooted(path))
									{
										path = PathUtils.TryToMakePathRelativeTo(path, folder);
										newResources[key] = path;
									}
								}
								catch (Exception)
								{
									// Skip resources that can't be converted
								}

								return true;
							});

							// Apply the converted paths to the widget
							foreach (var pair in newResources)
							{
								if (widget.Resources[pair.Key] != pair.Value)
								{
									updated = true;
									widget.Resources[pair.Key] = pair.Value;
								}
							}

							return true;
						});

						// Sync the XML editor with the updated resources
						if (updated)
						{
							try
							{
								_suppressProjectRefresh = true;
								UpdateSource();
							}
							finally
							{
								_suppressProjectRefresh = false;
							}
						}
					}

					onFinished(true);
				};

				dialog.ShowModal(Desktop);
			}
			catch (Exception)
			{
				onFinished(false);
			}
		}

		/// <summary>
		/// Automatically indents the next line after pressing Enter based on the XML nesting level
		/// </summary>
		private void ApplyAutoIndent()
		{
			if (!Options.AutoIndent || Options.IndentSpacesSize <= 0 || !_applyAutoIndent)
			{
				return;
			}

			_applyAutoIndent = false;

			var text = _textSource.Text;
			var pos = _textSource.CursorPosition;

			if (string.IsNullOrEmpty(text) || pos == 0 || pos >= text.Length)
			{
				return;
			}

			var indentLevel = _indentLevel;
			// Check if a closing tag immediately follows the cursor
			bool wrapAfterIndent = text.SubstringSafely(pos + 1, 2) == "</";

			if (indentLevel <= 0)
			{
				return;
			}

			// Build the indent string based on the nesting level
			var indent = new string(' ', indentLevel * Options.IndentSpacesSize);
			// If a closing tag follows, add a newline after the indent
			if (wrapAfterIndent)
			{
				indent += '\n';
			}
			_textSource.Insert(pos + 1, indent);

			// Move cursor after the indent
			_textSource.CursorPosition = pos + 2;
		}

		/// <summary>
		/// Automatically adds a closing tag when typing > after an opening tag name
		/// </summary>
		private void ApplyAutoClose()
		{
			if (!Options.AutoClose || !_applyAutoClose)
			{
				return;
			}

			_applyAutoClose = false;

			var pos = _textSource.CursorPosition;
			var currentTag = CurrentTag;
			// Only auto-close non-self-closing tags
			if (string.IsNullOrEmpty(currentTag) || !_needsCloseTag)
			{
				return;
			}

			// Extract the tag name and build the closing tag
			var closeTag = "</" + ExtractTag(currentTag) + ">";
			_textSource.Insert(pos + 1, closeTag);

			// Position cursor between opening and closing tags
			_textSource.CursorPosition = pos;
		}

		/// <summary>
		/// Handles text changes in the XML editor; marks as dirty and queues project refresh
		/// </summary>
		private void _textSource_TextChanged(object sender, ValueChangedEventArgs<string> e)
		{
			try
			{
				IsDirty = true;

				// Skip refresh if suppressed (e.g., during programmatic text updates)
				if (_suppressProjectRefresh)
				{
					return;
				}

				UpdateCursor();

				// Decide whether to refresh immediately or after a short delay
				var newLength = string.IsNullOrEmpty(e.NewValue) ? 0 : e.NewValue.Length;
				var oldLength = string.IsNullOrEmpty(e.OldValue) ? 0 : e.OldValue.Length;

				// Large changes or auto-close actions should refresh immediately to keep preview in sync
				if (Math.Abs(newLength - oldLength) > 1 || _applyAutoClose)
				{
					QueueRefreshProject();
				}
				else
				{
					// Small changes (single character edits) are queued after a short delay to batch multiple edits
					_refreshInitiated = DateTime.Now;
				}
			}
			catch (Exception)
			{
			}
		}

		// Queues an async task to reload the project from the current XML text
		private void QueueRefreshProject()
		{
			_refreshInitiated = null;

			_queue.QueueLoadProject(_textSource.Text);
		}

		// Enqueues a UI action to be executed on the main thread in the Update method
		private void QueueUIAction(Action action)
		{
			_uiActions.Enqueue(action);
		}

		/// <summary>
		/// Clears all nodes from the explorer tree on the next update
		/// </summary>
		public void QueueClearExplorer()
		{
			QueueUIAction(() => _treeViewExplorer.RemoveAllSubNodes());
		}

		/// <summary>
		/// Sets the status text on the next update
		/// </summary>
		public void QueueSetStatusText(string text)
		{
			QueueUIAction(() => _textStatus.Text = text);
		}

		// Extracts the tag name from an XML opening tag string (e.g., "Button" from "<Button>")
		private static string ExtractTag(string source)
		{
			if (string.IsNullOrEmpty(source))
			{
				return null;
			}

			return TagResolver.Match(source).Groups[1].Value;
		}

		/// <summary>
		/// Updates cursor position state including line/column info, parent tag, and current tag bounds
		/// </summary>
		private void UpdatePositions()
		{
			var lastStart = _currentTagStart;
			var lastEnd = _currentTagEnd;

			// Reset all position tracking variables
			_line = _col = _indentLevel = 0;
			_parentTag = null;
			_currentTagStart = null;
			_currentTagEnd = null;
			_needsCloseTag = false;

			if (string.IsNullOrEmpty(_textSource.Text))
			{
				return;
			}

			var cursorPos = _textSource.CursorPosition;
			var text = _textSource.Text;

			int? tagOpen = null;
			var isOpenTag = true;
			var length = text.Length;

			string currentTag = null;
			// Stack to track nested tags and their nesting level
			Stack<string> parentStack = new Stack<string>();

			// Parse the XML character by character up to the cursor position
			for (var i = 0; i < length; ++i)
			{
				// Stop parsing if we're past the cursor and not in an open tag
				if (tagOpen == null)
				{
					if (i >= cursorPos)
					{
						break;
					}

					currentTag = null;
					_currentTagStart = null;
					_currentTagEnd = null;
				}

				// Count columns before the cursor
				if (i < cursorPos)
				{
					++_col;
				}

				var c = text[i];
				if (c == '\n')
				{
					// Track line breaks and reset column counter
					++_line;
					_col = 0;
				}

				// Handle opening bracket: start of a tag
				if (c == '<')
				{
					// Check if we have an unclosed tag after the cursor
					if (tagOpen != null && isOpenTag && i >= cursorPos + 1)
					{
						_currentTagStart = tagOpen;
						_currentTagEnd = null;
						break;
					}

					// Start tracking a tag (skip XML declarations like <?xml>)
					if (i < length - 1 && text[i + 1] != '?')
					{
						tagOpen = i;
						isOpenTag = text[i + 1] != '/';
					}
				}

				// Handle closing bracket: end of a tag
				if (tagOpen != null && i > tagOpen.Value && c == '>')
				{
					if (isOpenTag)
					{
						// Check if this tag is self-closing (ends with />)
						var needsCloseTag = text[i - 1] != '/';
						_needsCloseTag = needsCloseTag;

						currentTag = text.Substring(tagOpen.Value, i - tagOpen.Value + 1);
						_currentTagStart = tagOpen;
						_currentTagEnd = i;

						// Add to parent stack if this is an opening tag before the cursor
						if (needsCloseTag && i <= cursorPos)
						{
							parentStack.Push(currentTag);
						}
					}
					else
					{
						// Closing tag: pop from parent stack
						if (parentStack.Count > 0)
						{
							parentStack.Pop();
						}
					}

					tagOpen = null;
				}
			}

			// The indent level is determined by the nesting depth
			_indentLevel = parentStack.Count;
			if (parentStack.Count > 0)
			{
				_parentTag = parentStack.Pop();
			}

			// Update the status bar with position information
			_textLocation.Text = string.Format("Line: {0}, Col: {1}, Indent: {2}", _line + 1, _col + 1, _indentLevel);

			if (!string.IsNullOrEmpty(_parentTag))
			{
				_parentTag = ExtractTag(_parentTag);
				_textLocation.Text += ", Parent: " + _parentTag;
			}

			// If the current tag changed, load its widget object in the property grid
			if ((lastStart != _currentTagStart || lastEnd != _currentTagEnd))
			{
				PropertyGrid.Object = null;
				_propertyGridPane.ResetScroll();
				if (!string.IsNullOrEmpty(currentTag))
				{
					var xml = currentTag;

					// Add the closing tag for complete XML
					if (_needsCloseTag)
					{
						var tag = ExtractTag(currentTag);
						xml += "</" + tag + ">";
					}

					// Queue the object loading in the async task queue
					_queue.QueueLoadObject(xml);
				}
			}

			HandleAutoComplete();
		}

		/// <summary>
		/// Displays an auto-complete context menu with available widget types based on the parent tag and typed text
		/// </summary>
		private void HandleAutoComplete()
		{
			// Hide existing auto-complete menu if it's open
			if (Desktop.ContextMenu == _autoCompleteMenu)
			{
				Desktop.HideContextMenu();
			}

			// Only show auto-complete when we're inside an incomplete tag in a valid parent
			if (_currentTagStart == null || _currentTagEnd != null || string.IsNullOrEmpty(_parentTag))
			{
				return;
			}

			var cursorPos = _textSource.CursorPosition;
			var text = _textSource.Text;

			// Extract what the user has typed after the opening bracket
			var typed = text.Substring(_currentTagStart.Value, cursorPos - _currentTagStart.Value);
			if (typed.StartsWith("<"))
			{
				typed = typed.Substring(1);

				// Get all available widget types for this parent
				var all = BuildAutoCompleteVariants();

				// Filter to only show matches for what's been typed so far
				if (!string.IsNullOrEmpty(typed))
				{
					all = (from a in all where a.StartsWith(typed, StringComparison.OrdinalIgnoreCase) select a).ToList();
				}

				if (all.Count > 0)
				{
					var lastStartPos = _currentTagStart.Value;
					var lastEndPos = cursorPos;

					// Build the auto-complete menu with all matching types
					_autoCompleteMenu = new VerticalMenu();
					foreach (var a in all)
					{
						var menuItem = new MenuItem
						{
							Text = a
						};

						menuItem.Selected += (s, args) =>
						{
							var result = "<" + menuItem.Text;
							var skip = result.Length;
							var needsClose = false;

							// Simple widgets and proportions are self-closing
							if (SimpleWidgets.Contains(menuItem.Text) ||
								Project.IsProportionName(menuItem.Text) ||
								menuItem.Text == MenuItemName ||
								menuItem.Text == ListItemName)
							{
								result += "/>";
								skip += 2;
							}
							else
							{
								// Container widgets need closing tags
								result += ">";
								++skip;

								// Add formatted indentation if auto-indent is enabled
								if (Options.AutoIndent && Options.IndentSpacesSize > 0)
								{
									result += "\n";
									var indentSize = Options.IndentSpacesSize * (_indentLevel + 1);
									result += new string(' ', indentSize);
									skip += indentSize;

									// Add indentation for closing tag
									result += "\n";
									indentSize = Options.IndentSpacesSize * _indentLevel;
									result += new string(' ', indentSize);
								}
								result += "</" + menuItem.Text + ">";
								++skip;
								needsClose = true;
							}

							// Replace the typed text with the completed widget tag
							_textSource.Replace(lastStartPos, lastEndPos - lastStartPos, result);
							_textSource.CursorPosition = lastStartPos + skip;
						};

						_autoCompleteMenu.Items.Add(menuItem);
					}

					// Show menu at the cursor position
					var screen = _textSource.ToGlobal(_textSource.CursorCoords);
					screen.Y += _textSource.Font.LineHeight;

					if (_autoCompleteMenu.Items.Count > 0)
					{
						_autoCompleteMenu.HoverIndex = 0;
					}

					Desktop.ShowContextMenu(_autoCompleteMenu, screen);
					// Keep focus in the text editor
					Desktop.FocusedKeyboardWidget = _textSource;

					_refreshInitiated = null;
				}
			}
		}

		/// <summary>
		/// Builds a list of valid child widget types for auto-complete based on the parent container type
		/// </summary>
		private List<string> BuildAutoCompleteVariants()
		{
			var result = new List<string>();

			if (string.IsNullOrEmpty(_parentTag))
			{
				return result;
			}

			// Add available child types based on parent widget type
			if (_parentTag == "Project")
			{
				// Project can only contain top-level containers
				result.AddRange(Containers.ToStringList());
				result.Add("Window");
				result.Add("Dialog");
			}
			else if (Containers.Contains(_parentTag) || _parentTag == "Window" || _parentTag == "Dialog")
			{
				// General containers can hold any widget type
				result.AddRange(SimpleWidgets.ToStringList());
				result.AddRange(Containers.ToStringList());
				result.AddRange(SpecialContainers.ToStringList());
			}
			else if (_parentTag.EndsWith(RowsProportionsName) || _parentTag.EndsWith(ColumnsProportionsName) || _parentTag.EndsWith(ProportionsName))
			{
				// Proportion containers can only hold proportion definitions
				result.Add(Project.ProportionName);
			}
			else if (_parentTag.EndsWith("Menu"))
			{
				// Menus can only contain menu items
				result.Add("MenuItem");
				result.Add("MenuSeparator");
			}
			else if (_parentTag == "ListBox" || _parentTag == "ComboBox")
			{
				// List containers can only contain list items
				result.Add("ListItem");
			}
			else if (_parentTag == "TabControl")
			{
				// TabControl can only contain TabItems
				result.Add("TabItem");
			}

			// Add proportion definitions for specific container types
			if (_parentTag == "Grid")
			{
				result.Add(_parentTag + "." + ColumnsProportionsName);
				result.Add(_parentTag + "." + RowsProportionsName);
				result.Add(_parentTag + "." + Project.DefaultColumnProportionName);
				result.Add(_parentTag + "." + Project.DefaultRowProportionName);
			}

			if (_parentTag == "VerticalStackPanel" || _parentTag == "HorizontalStackPanel")
			{
				result.Add(_parentTag + "." + Project.DefaultProportionName);
			}

			// Sort: non-nested elements first, then alphabetically
			result = result.OrderBy(s => !s.Contains('.')).ThenBy(s => s).ToList();

			return result;
		}

		// Updates cursor-related state and applies auto-indent/auto-close features
		private void UpdateCursor()
		{
			try
			{
				UpdatePositions();
				ApplyAutoIndent();
				ApplyAutoClose();
			}
			catch (Exception)
			{
			}
		}

		// Handles cursor position changes in the text editor
		private void _textSource_CursorPositionChanged(object sender, MyraEventArgs e)
		{
			UpdateCursor();
		}

		// Reloads the current project file, clearing all cached assets
		private void OnMenuFileReloadSelected(object sender, MyraEventArgs e)
		{
			AssetManager.Cache.Clear();
			_fontCache.Clear();
			_textureCache.Clear();
			Load(FilePath);
		}

		// Displays a dialog to load a custom stylesheet file for the project
		private void OnMenuFileLoadStylesheet(object sender, MyraEventArgs e)
		{
			AssetManager.Cache.Clear();

			var dlg = new FileDialog(FileDialogMode.OpenFile)
			{
				Filter = "*.xmms|*.xml"
			};

			try
			{
				if (!string.IsNullOrEmpty(Project.StylesheetPath))
				{
					var stylesheetPath = Project.StylesheetPath;
					if (!Path.IsPathRooted(stylesheetPath))
					{
						// Prepend folder path
						stylesheetPath = Path.Combine(Path.GetDirectoryName(FilePath), stylesheetPath);
					}

					dlg.Folder = Path.GetDirectoryName(stylesheetPath);
				}
				else if (!string.IsNullOrEmpty(FilePath))
				{
					dlg.Folder = Path.GetDirectoryName(FilePath);
				}
			}
			catch (Exception)
			{
			}

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				var filePath = dlg.FilePath;

				// Check whether stylesheet could be loaded
				try
				{
					var stylesheet = AssetManager.LoadStylesheet(filePath);
				}
				catch (Exception ex)
				{
					var msg = Dialog.CreateMessageBox("Stylesheet Error", ex.Message);
					msg.ShowModal(Desktop);
					return;
				}

				// Try to make stylesheet path relative to project folder
				filePath = PathUtils.TryToMakePathRelativeTo(filePath, Path.GetDirectoryName(FilePath));

				Project.StylesheetPath = filePath;
				UpdateSource();
				UpdateMenuFile();
			};

			dlg.ShowModal(Desktop);
		}

		// Resets the project to use the default stylesheet
		private void OnMenuFileResetStylesheetSelected(object sender, MyraEventArgs e)
		{
			AssetManager.Cache.Clear();
			Project.StylesheetPath = null;
			UpdateSource();
			UpdateMenuFile();
		}

		// Opens the debug options window for configuring debugging features
		private void DebugOptionsItemOnSelected(object sender1, MyraEventArgs eventArgs)
		{
			var debugOptions = new DebugOptionsWindow();
			debugOptions.ShowModal(Desktop);
		}

		// Exports the UI project to a C# designer file with customizable namespace and class name
		private void ExportCsItemOnSelected(object sender1, MyraEventArgs eventArgs)
		{
			var dlg = new ExportOptionsDialog();
			dlg.ShowModal(Desktop);

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				try
				{
					Project.ExportOptions.Namespace = dlg._textNamespace.Text;
					Project.ExportOptions.OutputPath = dlg._textOutputPath.Text;
					Project.ExportOptions.Class = dlg._textClassName.Text;

					UpdateSource();

					using (var export = new ExporterCS(Project))
					{
						var strings = new List<string>
						{
							"Success. Following files had been written:"
						};
						strings.AddRange(export.Export());

						var msg = Dialog.CreateMessageBox("Export To C#", string.Join("\n", strings));
						msg.ShowModal(Desktop);
					}
				}
				catch (Exception ex)
				{
					var msg = Dialog.CreateMessageBox("Error", ex.Message);
					msg.ShowModal(Desktop);
				}
			};
		}

		// Exports a lightweight C# version of the UI that can be copied and pasted directly into code
		private void ExportCsLightItemOnSelected(object sender1, MyraEventArgs eventArgs)
		{
			try
			{
				string code;
				using (var export = new ExporterCS(Project))
				{
					code = export.ExportDesignerCode(MyraPad.Resources.ExportCSLight, true);
				}

				var dlg = new ExportLightWindow
				{
					Code = code
				};

				dlg.ShowModal(Desktop);
			}
			catch (Exception ex)
			{
				var msg = Dialog.CreateMessageBox("Error", ex.Message);
				msg.ShowModal(Desktop);
			}
		}

		/// <summary>
		/// Updates the XML when a widget property is modified in the property grid
		/// </summary>
		private void PropertyGridOnPropertyChanged(object sender, GenericEventArgs<string> eventArgs)
		{
			IsDirty = true;

			// Serialize the modified widget object back to XML
			var xml = _project.SaveObjectToXml(PropertyGrid.Object, ExtractTag(CurrentTag), ParentType);

			// If the original tag needs a closing tag, ensure the new XML has one too
			if (_needsCloseTag)
			{
				xml = xml.Replace("/>", ">");
			}

			// Replace the old XML tag with the new serialized XML
			if (_currentTagStart != null && _currentTagEnd != null)
			{
				try
				{
					_suppressProjectRefresh = true;
					// Replace the current tag with the updated XML
					_textSource.Replace(_currentTagStart.Value,
						_currentTagEnd.Value - _currentTagStart.Value + 1,
						xml);
					QueueRefreshProject();
				}
				finally
				{
					_suppressProjectRefresh = false;
				}

				// Update the end position of the current tag after replacement
				_currentTagEnd = _currentTagStart.Value + xml.Length - 1;
			}
		}

		// Prompts for confirmation before exiting the application
		private void QuitItemOnDown(object sender, MyraEventArgs eventArgs)
		{
			var mb = Dialog.CreateMessageBox("Quit", "Are you sure?");

			mb.Closed += (o, args) =>
			{
				if (mb.Result)
				{
					Studio.Instance.Exit();
				}
			};

			mb.ShowModal(Desktop);
		}

		// Displays the About dialog showing the MyraPad version
		private void AboutItemOnClicked(object sender, MyraEventArgs eventArgs)
		{
			var messageBox = Dialog.CreateMessageBox("About", "MyraPad " + MyraEnvironment.Version);
			messageBox.ShowModal(Desktop);
		}

		/// <summary>
		/// Saves the project to a new file (Save As dialog)
		/// </summary>
		private void SaveAsItemOnClicked(object sender, MyraEventArgs eventArgs)
		{
			Save(true);
		}

		/// <summary>
		/// Saves the current project to its existing file
		/// </summary>
		private void SaveItemOnClicked(object sender, MyraEventArgs eventArgs)
		{
			Save(false);
		}

		// Displays a dialog to create a new project with a user-selected root widget type
		private void NewItemOnClicked(object sender, MyraEventArgs eventArgs)
		{
			var dlg = new NewProjectWizard();

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				var rootType = "Grid";

				if (dlg._radioButtonHorizontalStackPanel.IsPressed)
				{
					rootType = "HorizontalStackPanel";
				}
				else if (dlg._radioButtonVerticalStackPanel.IsPressed)
				{
					rootType = "VerticalStackPanel";
				}
				else if (dlg._radioButtonPanel.IsPressed)
				{
					rootType = "Panel";
				}
				else if (dlg._radioButtonScrollViewer.IsPressed)
				{
					rootType = "ScrollViewer";
				}
				else if (dlg._radioButtonHorizontalSplitPane.IsPressed)
				{
					rootType = "HorizontalSplitPane";
				}
				else if (dlg._radioButtonVerticalSplitPane.IsPressed)
				{
					rootType = "VerticalSplitPane";
				}
				else if (dlg._radioButtonWindow.IsPressed)
				{
					rootType = "Window";
				}
				else if (dlg._radioButtonDialog.IsPressed)
				{
					rootType = "Dialog";
				}

				New(rootType);
			};

			dlg.ShowModal(Desktop);
		}

		// Displays a file dialog to open an existing project file
		private void OpenItemOnClicked(object sender, MyraEventArgs eventArgs)
		{
			var dlg = new FileDialog(FileDialogMode.OpenFile)
			{
				Filter = "*.xmmp|*.xml"
			};

			if (!string.IsNullOrEmpty(FilePath))
			{
				dlg.Folder = Path.GetDirectoryName(FilePath);
			}
			else if (!string.IsNullOrEmpty(LastFolder))
			{
				dlg.Folder = LastFolder;
			}

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				var filePath = dlg.FilePath;
				if (string.IsNullOrEmpty(filePath))
				{
					return;
				}

				Load(filePath);
			};

			dlg.ShowModal(Desktop);
		}

		/// <summary>
		/// Updates game logic, processes queued UI actions, and handles async project/object loading results
		/// </summary>
		public void Update(GameTime gameTime)
		{
			try
			{
				// Check if a delayed project refresh should be triggered
				if (_refreshInitiated != null && (DateTime.Now - _refreshInitiated.Value).TotalSeconds >= 0.75f)
				{
					QueueRefreshProject();
				}

				// Process all queued UI actions from async operations
				while (!_uiActions.IsEmpty)
				{
					Action action;
					_uiActions.TryDequeue(out action);
					action();
				}

				// Update property grid with newly loaded object from async queue
				if (NewObject != null)
				{
					PropertyGrid.ParentType = ParentType;
					PropertyGrid.Object = NewObject;

					// Automatically select the corresponding node in the explorer tree
					try
					{
						_suppressExplorerRefresh = true;

						// Find the node by matching line/column position in the XML
						object selectedItem = null;
						foreach (var pair in Project.ObjectsNodes)
						{
							var lineInfo = (IXmlLineInfo)pair.Item2;
							if (lineInfo.LineNumber - 1 > _line ||
								(lineInfo.LineNumber - 1 == _line && lineInfo.LinePosition - 1 > _col))
							{
								break;
							}

							selectedItem = pair.Item1;
						}

						if (selectedItem != null)
						{
							var node = _treeViewExplorer.FindNode(n => n.Tag == selectedItem);
							_treeViewExplorer.SelectedNode = node;
						}
					}
					finally
					{
						_suppressExplorerRefresh = false;
					}

					_propertyGridPane.ResetScroll();
					NewObject = null;
				}

				// Update the visual preview with newly loaded project from async queue
				if (NewProject != null)
				{
					Project = NewProject;

					// Apply the stylesheet's desktop background if available
					if (Project.Stylesheet != null && Project.Stylesheet.DesktopStyle != null)
					{
						_projectHolder.Background = Project.Stylesheet.DesktopStyle.Background;
					}
					else
					{
						_projectHolder.Background = null;
					}

					// Select the specified node in the explorer (if scheduled)
					if (NewProjectSelectedNodeIndex != null)
					{
						Debug.WriteLine(NewProjectSelectedNodeIndex);
						_treeViewExplorer.SelectedNode = _treeViewExplorer.GetNodeByAbsoluteIndex(NewProjectSelectedNodeIndex.Value);
					}

					NewProject = null;
					NewProjectSelectedNodeIndex = null;
				}
			}
			catch (Exception ex)
			{
				_textStatus.Text = ex.Message;
			}
		}

		// Creates a new project with the specified root widget type and initializes the text editor
		private void New(string rootType)
		{
			// Use the template and substitute the root container type
			var source = MyraPad.Resources.NewProjectTemplate.Replace("$containerType", rootType);

			_textSource.Text = source;

			// Position cursor after the opening root element for user convenience
			var newLineCount = 0;
			var pos = 0;
			while (pos < _textSource.Text.Length && newLineCount < 3)
			{
				++pos;

				if (_textSource.Text[pos] == '\n')
				{
					++newLineCount;
				}
			}

			_textSource.CursorPosition = pos;
			Desktop.FocusedKeyboardWidget = _textSource;

			// Reset state for a new project
			FilePath = string.Empty;
			IsDirty = false;
			_projectHolder.Background = null;
		}

		// Writes the current XML content to a file and updates the project path
		private void ProcessSave(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
			{
				return;
			}

			// Update resource paths to be relative to the new file location
			UpdateResourcesPaths(FilePath, filePath, updated =>
			{
				// Write the XML content to the file
				File.WriteAllText(filePath, _textSource.Text);

				// Update the project path and state
				FilePath = filePath;
				IsDirty = false;

				// Refresh the project if resources were updated
				if (updated)
				{
					QueueRefreshProject();
				}
			});
		}

		// Saves the current project to a file; prompts for a filename if this is a new project or Save As is selected
		private void Save(bool setFileName)
		{
			if (string.IsNullOrEmpty(FilePath) || setFileName)
			{
				var dlg = new FileDialog(FileDialogMode.SaveFile)
				{
					Filter = "*.xmmp"
				};

				if (!string.IsNullOrEmpty(FilePath))
				{
					dlg.FilePath = FilePath;
				}
				else if (!string.IsNullOrEmpty(LastFolder))
				{
					dlg.Folder = LastFolder;
				}

				dlg.ShowModal(Desktop);

				dlg.Closed += (s, a) =>
				{
					if (dlg.Result)
					{
						ProcessSave(dlg.FilePath);
					}
				};
			}
			else
			{
				ProcessSave(FilePath);
			}
		}

		/// <summary>
		/// Loads a project from a file and populates the XML editor with its content
		/// </summary>
		public void Load(string filePath)
		{
			try
			{
				// Read the file content
				var data = File.ReadAllText(filePath);

				FilePath = filePath;

				try
				{
					// Prevent automatic project refresh while setting the text
					_suppressProjectRefresh = true;
					_textSource.Text = data;
					_textSource.CursorPosition = 0;
				}
				finally
				{
					_suppressProjectRefresh = false;
				}

				// Queue a project refresh to parse the XML
				QueueRefreshProject();
				UpdateCursor();
				// Set keyboard focus to the text editor
				Desktop.FocusedKeyboardWidget = _textSource;

				IsDirty = false;
			}
			catch (Exception ex)
			{
				var dialog = Dialog.CreateMessageBox("Error", ex.ToString());
				dialog.ShowModal(Desktop);
			}
		}

		// Updates the window title to show the file path and unsaved changes indicator
		private void UpdateTitle()
		{
			var title = string.IsNullOrEmpty(_filePath) ? "MyraPad" : _filePath;

			if (_isDirty)
			{
				title += " *";
			}

			Studio.Instance.Window.Title = title;
		}

		// Enables/disables menu items based on the current state
		private void UpdateMenuFile()
		{
			_menuFileReload.Enabled = !string.IsNullOrEmpty(FilePath);
		}

		// Shuts down the async task queue when the application closes
		public void Quit()
		{
			_queue.Quit();
		}

		// Recursively builds the visual tree hierarchy in the explorer from a widget object and its children
		private void BuildExplorerTreeRecursive(ITreeViewNode node, IItemWithId root)
		{
			if (root == null)
			{
				return;
			}

			// Create a label showing the widget type and its ID (if set)
			var id = root.GetType().Name;
			if (!string.IsNullOrEmpty(root.Id))
			{
				id += $" (#{root.Id})";
			}

			var newNode = node.AddSubNode(new Label
			{
				Text = id
			});

			// Store the widget object as the node's tag for later reference
			newNode.Tag = root;
			newNode.IsExpanded = true;

			// Find the content property that holds child widgets
			var props = root.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
			var contentProperty = (from p in props where p.FindAttribute<ContentAttribute>() != null select p).FirstOrDefault();
			if (contentProperty == null)
			{
				return;
			}

			var content = contentProperty.GetValue(root);

			// Recursively add child widgets (either a single widget or a list of widgets)
			var asList = content as IList;
			if (asList != null)
			{
				// Multiple children (Widgets collection)
				foreach (IItemWithId child in asList)
				{
					BuildExplorerTreeRecursive(newNode, child);
				}
			}
			else
			{
				// Single child widget
				BuildExplorerTreeRecursive(newNode, (IItemWithId)content);
			}
		}

		// Rebuilds the explorer tree from the project hierarchy while preserving the current selection
		private void RefreshExplorer()
		{
			// Save the currently selected node so we can restore it after rebuild
			int? selectedIndex = null;
			if (_treeViewExplorer.SelectedNode != null)
			{
				selectedIndex = _treeViewExplorer.AllNodes.IndexOf(_treeViewExplorer.SelectedNode);
			}

			// Clear all existing nodes from the tree
			_treeViewExplorer.RemoveAllSubNodes();

			if (Project == null || Project.Root == null)
			{
				return;
			}

			// Rebuild the tree structure from the project hierarchy
			BuildExplorerTreeRecursive(_treeViewExplorer, Project.Root);

			// Restore the selection if it still exists
			if (selectedIndex != null && selectedIndex.Value < _treeViewExplorer.AllNodes.Count)
			{
				try
				{
					_suppressExplorerRefresh = true;

					_treeViewExplorer.SelectedNode = _treeViewExplorer.AllNodes[selectedIndex.Value];
				}
				finally
				{
					_suppressExplorerRefresh = false;
				}
			}
		}

		// Handles explorer tree node selection by moving the cursor to the corresponding position in the XML editor
		private void _treeViewExplorer_SelectionChanged(object sender, MyraEventArgs e)
		{
			// Don't respond to selection changes made by programmatic updates
			if (_suppressExplorerRefresh || _treeViewExplorer.SelectedNode == null || Project.ObjectsNodes == null)
			{
				return;
			}

			// Find the XML element corresponding to the selected tree node
			Tuple<object, XElement> find = null;
			foreach (var pair in Project.ObjectsNodes)
			{
				if (pair.Item1 == _treeViewExplorer.SelectedNode.Tag)
				{
					find = pair;
					break;
				}
			}

			if (find == null)
			{
				return;
			}

			var lineInfo = (IXmlLineInfo)find.Item2;

			// Calculate the text position corresponding to the XML element's line and column
			var currentLineNumber = 0;
			var currentLinePosition = 0;
			for (var pos = 0; pos < _textSource.Text.Length; ++pos)
			{
				// Check if we've reached the target line and column
				if (currentLineNumber > lineInfo.LineNumber - 1 ||
					(currentLineNumber == lineInfo.LineNumber - 1 && currentLinePosition >= lineInfo.LinePosition - 1))
				{
					// Move cursor to this position and focus the text editor
					_textSource.CursorPosition = pos;
					Desktop.FocusedKeyboardWidget = _textSource;
					break;
				}

				var c = _textSource.Text[pos];
				switch (c)
				{
					case '\n':
						// Track line breaks
						++currentLineNumber;
						currentLinePosition = 0;
						break;

					case '\r':
						// Ignore carriage returns
						break;

					default:
						// Track character position in the line
						++currentLinePosition;
						break;
				}
			}
		}

	}
}