using AssetManagementBase;
using FontStashSharp;
using FontStashSharp.RichText;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.File;
using Myra.Graphics2D.UI.Properties;
using Myra.Graphics2D.UI.Styles;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

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
			typeof(WrapPanel),
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

		// Path to the currently open project file
		private string _filePath;
		// Flag indicating whether the current project has unsaved changes
		private bool _isDirty;
		// The loaded UI project containing the widget hierarchy
		private Project _project;
		// Current line and column position of the cursor, and indent nesting level
		private int _line, _col, _indentLevel;
		// Timestamp when the last project refresh was initiated (used for delayed refresh)
		private DateTime? _refreshInitiated;
		// Cache of loaded fonts to avoid reloading the same fonts multiple times
		private readonly Dictionary<string, FontSystem> _fontCache = new Dictionary<string, FontSystem>();
		// Cache of loaded textures to avoid reloading the same images multiple times
		private readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
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
					PropertyGridSettings.AssetManager = MyraAssets.DefaultAssetManager;
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

		// A newly loaded project to be displayed in the visual preview
		public Project NewProject { get; set; }

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
			_menuFileReload.Selected += (s, e) => Reload();
			_menuFileSave.Selected += SaveItemOnClicked;
			_menuFileSaveAs.Selected += SaveAsItemOnClicked;
			_menuFileExportToCS.Selected += ExportCsItemOnSelected;
			_menuFileExportToCSLight.Selected += ExportCsLightItemOnSelected;
			_menuFileLoadStylesheet.Selected += OnMenuFileLoadStylesheet;
			_menuFileResetStylesheet.Selected += OnMenuFileResetStylesheetSelected;
			_menuFileSaveStylesheet.Selected += (s, a) => SaveStylesheet();
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

			PropertyGrid.PropertyChanged += (s, e) => OnPropertyChanged();
			PropertyGrid.CustomValuesProvider = RecordValuesProvider;
			PropertyGrid.CustomSetter = RecordSetter;
			PropertyGrid.CustomWidgetProvider = CreateCustomEditor;
			PropertyGrid.Settings.AssetManager = MyraAssets.DefaultAssetManager;

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

			_treeViewStylesheet = new TreeView
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
			};

			_treeViewStylesheet.SelectionChanged += _treeViewStylesheet_SelectionChanged;
			_panelStyles.Content = _treeViewStylesheet;

			_treeViewStyleExplorer = new TreeView
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch
			};

			_treeViewStyleExplorer.SelectionChanged += _treeViewStyleExplorer_SelectionChanged;
			_panelStyleExplorer.Content = _treeViewStyleExplorer;

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

			RemoveStylesheetTab();

			UpdateTitle();
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
						Reload();
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
			var data = Project != null ? Project.ToXml() : string.Empty;
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
				_textSource.Text = _project.ToXml();
			}
			catch (Exception ex)
			{
				var messageBox = Dialog.CreateMessageBox("Error", ex.Message);
				messageBox.ShowModal(Desktop);
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

		private void Reload()
		{
			AssetManager.Cache.Clear();
			_fontCache.Clear();
			_textureCache.Clear();
			Load(FilePath);
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

			dlg.SetOptions(Project.ExportOptions);
			dlg.ShowModal(Desktop);

			dlg.Closed += (s, a) =>
			{
				if (!dlg.Result)
				{
					return;
				}

				try
				{
					dlg.GetOptions(Project.ExportOptions);

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
					code = export.ExportDesignerCode(Resources.ExportCSLight, true);
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

		private void OnPropertyChanged()
		{
			IsDirty = true;

			if (PropertyGrid.Object is WidgetStyle)
			{
				// Stylesheet property changed
				QueueRefreshProject();
			}
			else
			{
				OnObjectPropertyChanged();
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
				else if (dlg._radioButtonWrapPanel.IsPressed)
				{
					rootType = "WrapPanel";
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

				// Update the visual preview with newly loaded project from async queue
				if (NewProject != null)
				{
					Project = NewProject;

					if (HasCustomStylesheet)
					{
						// Show stylesheet tab
						AddStylesheetTab();
					}
					else
					{
						RemoveStylesheetTab();
					}

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
			var source = Resources.NewProjectTemplate.Replace("$containerType", rootType);

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

			// Write the XML content to the file
			File.WriteAllText(filePath, _textSource.Text);

			SaveStylesheet();

			// Update the project path and state
			FilePath = filePath;
			IsDirty = false;
		}

		// Saves the current project to a file; prompts for a filename if this is a new project or Save As is selected
		private void Save(bool setFileName)
		{
			if (string.IsNullOrEmpty(FilePath) || setFileName)
			{
				if (setFileName && HasCustomStylesheet && !Path.IsPathRooted(Project.StylesheetPath))
				{
					var dialog = Dialog.CreateMessageBox("Error", "Can't change path of a project with a custom stylesheet.");
					dialog.ShowModal(Desktop);

					return;
				}

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
			_menuFileSaveStylesheet.Enabled = HasCustomStylesheet;
		}

		// Shuts down the async task queue when the application closes
		public void Quit()
		{
			_queue.Quit();
		}
	}
}