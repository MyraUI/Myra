using System;
using System.ComponentModel;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using System.Xml.Serialization;
using TextCopy;
using Myra.Graphics2D.UI.TextEdit;
using FontStashSharp;
using FontStashSharp.RichText;
using Myra.Events;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#elif STRIDE
using Stride.Core.Mathematics;
using Stride.Input;
#else
using System.Numerics;
using System.Drawing;
using Myra.Platform;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// A text input widget that allows users to enter and edit text with support for undo/redo and selection.
	/// Features include multi-line text, text wrapping, password masking, keyboard navigation, selection,
	/// clipboard operations, and full undo/redo support.
	/// </summary>
	public class TextBox : Widget
	{
		private const int CursorUpdateDelayInMs = 30;

		// Cursor rendering and timing
		private DateTime _lastCursorUpdate;
		private DateTime _lastBlinkStamp = DateTime.Now;
		private bool _cursorOn = true;  // Cursor visibility state for blinking animation
		private bool _wrap = false;

		// Layout engine for rendering text with proper glyph positioning
		private readonly RichTextLayout _richTextLayout = new RichTextLayout
		{
			CalculateGlyphs = true,
			SupportsCommands = false
		};

		// Cursor and selection state
		private Point? _lastCursorPosition;
		private int _cursorIndex;  // Zero-based position of cursor in text
		private Point _internalScrolling = Mathematics.PointZero;  // Scroll offset for text that doesn't fit
		private bool _suppressRedoStackReset = false;  // Flag to prevent redo stack reset during undo/redo operations

		// Text content and display
		private string _text;
		private string _hintText;  // Placeholder text shown when empty and unfocused
		private bool _passwordField;
		private bool _isTouchDown;

		// Undo/redo stacks for handling text modifications
		private readonly UndoRedoStack UndoStack = new UndoRedoStack();
		private readonly UndoRedoStack RedoStack = new UndoRedoStack();

		/// <summary>
		/// Gets or sets the vertical spacing in pixels between lines of text.
		/// </summary>
		[Category("Appearance")]
		[DefaultValue(0)]
		public int VerticalSpacing
		{
			get
			{
				return _richTextLayout.VerticalSpacing;
			}
			set
			{
				_richTextLayout.VerticalSpacing = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the text contained in the text box.
		/// </summary>
		[Category("Appearance")]
		[DefaultValue(null)]
		public string Text
		{
			get
			{
				return _text;
			}
			set
			{
				SetText(value, false);
				DisableHintText();
			}
		}

		/// <summary>
		/// Gets or sets the hint text displayed when the text box is empty.
		/// </summary>
		[Category("Appearance")]
		[DefaultValue(null)]
		public string HintText
		{
			get
			{
				return _hintText;
			}
			set
			{
				_hintText = value;

				if (_text == null)
				{
					EnableHintText();
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether hint text is currently displayed.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public bool HintTextEnabled { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the text box supports multiple lines of text.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(false)]
		public bool Multiline { get; set; }

		private string UserText
		{
			get
			{
				return _text;
			}

			set
			{
				SetText(value, true);
			}
		}

		private int Length => _text.Length();

		private bool InsertMode { get; set; }

		/// <summary>
		/// Gets or sets the font used to render the text.
		/// </summary>
		[Category("Appearance")]
		public SpriteFontBase Font
		{
			get
			{
				return _richTextLayout.Font;
			}
			set
			{
				_richTextLayout.Font = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether text wraps to multiple lines.
		/// </summary>
		[Category("Appearance")]
		[DefaultValue(false)]
		public bool Wrap
		{
			get
			{
				return _wrap;
			}

			set
			{
				if (value == _wrap)
				{
					return;
				}

				_wrap = value;
				InvalidateMeasure();
			}
		}

		/// <summary>
		/// Gets or sets the color of the text.
		/// </summary>
		[Category("Appearance")]
		public Color TextColor { get; set; }

		/// <summary>
		/// Gets or sets the color of the text when the text box is disabled.
		/// </summary>
		[Category("Appearance")]
		public Color? DisabledTextColor { get; set; }

		/// <summary>
		/// Gets or sets the color of the text when the text box has focus.
		/// </summary>
		[Category("Appearance")]
		public Color? FocusedTextColor { get; set; }

		/// <summary>
		/// Gets or sets the image displayed as the text cursor.
		/// </summary>
		[Category("Appearance")]
		public IImage Cursor { get; set; }

		/// <summary>
		/// Gets or sets the brush used to draw the selection highlight.
		/// </summary>
		[Category("Appearance")]
		public IBrush Selection { get; set; }

		/// <summary>
		/// Gets or sets the cursor blink interval in milliseconds.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(450)]
		public int BlinkIntervalInMs { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the text box is read-only.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(false)]
		public bool Readonly { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the text box masks input as a password field.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(false)]
		public bool PasswordField
		{
			get
			{
				return _passwordField;
			}
			set
			{
				_passwordField = value;
				UpdateRichTextLayout();
			}
		}

		/// <summary>
		/// Gets or sets the vertical alignment of the text within the text box.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(VerticalAlignment.Top)]
		public VerticalAlignment TextVerticalAlignment { get; set; }

		/// <summary>
		/// Gets or sets the mouse cursor type to display when hovering over the text box.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(MouseCursorType.IBeam)]
		public override MouseCursorType? MouseCursor
		{
			get => base.MouseCursor;
			set => base.MouseCursor = value;
		}

		/// <summary>
		/// Gets or sets a value indicating whether the text box clips its content to its bounds.
		/// </summary>
		[DefaultValue(true)]
		public override bool ClipToBounds
		{
			get
			{
				return base.ClipToBounds;
			}
			set
			{
				base.ClipToBounds = value;
			}
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the text box.
		/// </summary>
		[DefaultValue(HorizontalAlignment.Stretch)]
		public override HorizontalAlignment HorizontalAlignment
		{
			get
			{
				return base.HorizontalAlignment;
			}
			set
			{
				base.HorizontalAlignment = value;
			}
		}

		/// <summary>
		/// Gets or sets the zero-based index of the text cursor position.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public int CursorPosition
		{
			get
			{
				return _cursorIndex;
			}

			set
			{
				if (_cursorIndex == value)
				{
					return;
				}

				_cursorIndex = value;

				OnCursorIndexChanged();
			}
		}

		/// <summary>
		/// Gets the position of the text cursor in local coordinates.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Point CursorCoords => GetRenderPositionByIndex(CursorPosition);

		/// <summary>
		/// Gets the zero-based index where text selection starts.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public int SelectStart { get; private set; }

		/// <summary>
		/// Gets the zero-based index where text selection ends.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public int SelectEnd { get; private set; }

		private int CursorWidth => 1 + (Cursor != null ? Cursor.Size.X : 0);

		/// <summary>
		/// Gets or sets the desktop this text box is attached to.
		/// </summary>
		public override Desktop Desktop
		{
			get
			{
				return base.Desktop;
			}

			internal set
			{
				if (Desktop != null)
				{
					Desktop.TouchUp -= DesktopTouchUp;
					Desktop.TouchDown -= DesktopTouchDown;
				}

				base.Desktop = value;

				if (Desktop != null)
				{
					Desktop.TouchUp += DesktopTouchUp;
					Desktop.TouchDown += DesktopTouchDown;
				}
			}
		}

		/// <summary>
		/// Fires when the value is about to be changed
		/// Set Cancel to true if you want to cancel the change
		/// </summary>
		public event MyraEventHandler<ValueChangingEventArgs<string>> ValueChanging;

		/// <summary>
		/// Fires every time when the text had been changed
		/// </summary>
		public event MyraEventHandler<ValueChangedEventArgs<string>> TextChanged;

		/// <summary>
		/// Fires every time when the text had been changed by user(doesnt fire if it had been assigned through code)
		/// </summary>
		public event MyraEventHandler<ValueChangedEventArgs<string>> TextChangedByUser;

		/// <summary>
		/// Fires every time when the text had been deleted
		/// </summary>
		public event MyraEventHandler<TextDeletedEventArgs> TextDeleted;

		/// <summary>
		/// Occurs when the cursor position in the text box changes.
		/// </summary>
		public event MyraEventHandler CursorPositionChanged;

		/// <summary>
		/// Initializes a new instance of the <see cref="TextBox"/> class with the specified style.
		/// </summary>
		/// <param name="styleName">The name of the style to apply. Defaults to the default stylesheet style.</param>
		public TextBox(string styleName = Stylesheet.DefaultStyleName)
		{
			AcceptsKeyboardFocus = true;

			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Top;

			ClipToBounds = true;

			SetStyle(styleName);

			BlinkIntervalInMs = 450;

			MouseCursor = MouseCursorType.IBeam;

			if (MyraEnvironment.EventHandlingModel == EventHandlingStrategy.EventBubbling)
				this.TouchDoubleClick += TextBox_TouchDoubleClickStopPropagation;
		}

		// Prevent double-click from propagating to parent widgets
		private void TextBox_TouchDoubleClickStopPropagation(object sender, MyraEventArgs e)
		{
			e.StopPropagation();
			InputEventsManager.StopPropagation(InputEventType.TouchDown);
		}

		// Removes 'l' characters starting at position 'pos' from the text
		private void DeleteChars(int pos, int l)
		{
			if (l == 0)
				return;

			UserText = UserText.Substring(0, pos) + UserText.Substring(pos + l);
		}

		// Inserts a string 's' at the specified position, returns false if string is empty
		private bool InsertChars(int pos, string s)
		{
			if (string.IsNullOrEmpty(s))
				return false;

			if (string.IsNullOrEmpty(Text))
				UserText = s;
			else
				UserText = UserText.Substring(0, pos) + s + UserText.Substring(pos);

			return true;
		}

		// Inserts a single character at the specified position
		private bool InsertChar(int pos, char ch)
		{
			if (string.IsNullOrEmpty(Text))
				UserText = ch.ToString();
			else
				UserText = UserText.Substring(0, pos) + ch + UserText.Substring(pos);

			return true;
		}

		/// <summary>
		/// Inserts text at the specified position in the text box.
		/// </summary>
		/// <param name="where">The zero-based index where the text will be inserted.</param>
		/// <param name="text">The text to insert.</param>
		public void Insert(int where, string text)
		{
			text = Process(text);
			if (string.IsNullOrEmpty(text))
			{
				return;
			}

			if (InsertChars(where, text))
			{
				UndoStack.MakeInsert(where, text.Length());
				CursorPosition += text.Length;
			}
		}

		/// <summary>
		/// Replaces a range of text at the specified position with new text.
		/// </summary>
		/// <param name="where">The zero-based index where replacement begins.</param>
		/// <param name="len">The number of characters to replace.</param>
		/// <param name="text">The replacement text.</param>
		public void Replace(int where, int len, string text)
		{
			if (len <= 0)
			{
				Insert(where, text);
				return;
			}

			text = Process(text);

			if (string.IsNullOrEmpty(text))
			{
				Delete(where, len);
				return;
			}

			UndoStack.MakeReplace(Text, where, len, text.Length);
			UserText = UserText.Substring(0, where) + text + UserText.Substring(where + len);
		}

		/// <summary>
		/// Replaces all text in the text box with the specified text.
		/// </summary>
		/// <param name="text">The new text to set.</param>
		public void ReplaceAll(string text)
		{
			if (string.IsNullOrEmpty(Text))
			{
				Replace(0, 0, text);
			}
			else
			{
				Replace(0, Text.Length, text);
			}
		}

		// Deletes 'len' characters starting at 'where', handling surrogate pairs for Unicode characters
		// Returns the number of characters actually deleted
		private int Delete(int where, int len)
		{
			if (where < 0 || where >= Length || len < 0)
			{
				return 0;
			}

			// Handle surrogate pairs (multi-byte Unicode characters)
			// If we're deleting part of a surrogate pair, delete the whole pair
			if (len == 1)
			{
				if (char.IsSurrogate(Text[where]))
				{
					len++;
				}

				if (char.IsLowSurrogate(Text[where]))
				{
					where--;
				}
			}

			UndoStack.MakeDelete(Text, where, len);
			var stringToDelete = Text.Substring(where, len);
			DeleteChars(where, len);

			TextDeleted?.Invoke(this, new TextDeletedEventArgs(where, stringToDelete));
			return len;
		}

		// Deletes the selected text range, adjusting cursor and selection to match the deletion
		private void DeleteSelection()
		{
			if (SelectStart != SelectEnd)
			{
				if (SelectStart < SelectEnd)
				{
					Delete(SelectStart, SelectEnd - SelectStart);
					SelectEnd = CursorPosition = SelectStart;
				}
				else
				{
					Delete(SelectEnd, SelectStart - SelectEnd);
					SelectStart = CursorPosition = SelectEnd;
				}
			}
		}

		// Pastes text from clipboard, deleting any selection first
		private bool Paste(string text)
		{
			text = Process(text);

			DeleteSelection();
			if (InsertChars(CursorPosition, text))
			{
				UndoStack.MakeInsert(CursorPosition, text.Length());
				CursorPosition += text.Length;
				ResetSelection();
				return true;
			}

			return false;
		}

		// Inputs a single character, handling insert mode and selection deletion
		private void InputChar(char ch)
		{
			// Don't allow newline in single-line mode
			if (!Multiline && ch == '\n')
				return;

			// In insert mode: replace character at cursor instead of inserting
			if (InsertMode && !(SelectStart != SelectEnd) && CursorPosition < Length)
			{
				UndoStack.MakeReplace(Text, CursorPosition, 1, 1);
				DeleteChars(CursorPosition, 1);
				if (InsertChar(CursorPosition, ch))
				{
					UserSetCursorPosition(CursorPosition + 1);
				}
			}
			else
			{
				// In append mode: delete selection if any, then insert character
				DeleteSelection();
				if (InsertChar(CursorPosition, ch))
				{
					UndoStack.MakeInsert(CursorPosition, 1);
					UserSetCursorPosition(CursorPosition + 1);
				}
			}

			ResetSelection();
		}

		// Performs undo or redo by applying the opposite operation from one stack and pushing to the other
		private void UndoRedo(UndoRedoStack undoStack, UndoRedoStack redoStack)
		{
			if (undoStack.Stack.Count == 0)
			{
				return;
			}

			var record = undoStack.Stack.Pop();
			try
			{
				// Prevent redo stack reset while executing undo/redo to maintain redo chain
				_suppressRedoStackReset = true;
				switch (record.OperationType)
				{
					case OperationType.Insert:
						// Undo insert: delete the inserted text and record as redo
						redoStack.MakeDelete(Text, record.Where, record.Length);
						DeleteChars(record.Where, record.Length);
						UserSetCursorPosition(record.Where);
						break;
					case OperationType.Delete:
						// Undo delete: re-insert the deleted text and record as redo
						if (InsertChars(record.Where, record.Data))
						{
							redoStack.MakeInsert(record.Where, record.Data.Length);
							UserSetCursorPosition(record.Where + record.Data.Length);
						}
						break;
					case OperationType.Replace:
						// Undo replace: restore original text and record as redo
						redoStack.MakeReplace(Text, record.Where, record.Length, record.Data.Length());
						DeleteChars(record.Where, record.Length);
						InsertChars(record.Where, record.Data);
						break;
				}
			}
			finally
			{
				_suppressRedoStackReset = false;
			}

			ResetSelection();
		}

		// Undoes the last text modification
		private void Undo()
		{
			UndoRedo(UndoStack, RedoStack);
		}

		// Redoes the last undone text modification
		private void Redo()
		{
			UndoRedo(RedoStack, UndoStack);
		}

		// Sets cursor position, clamping to valid text range [0, Length]
		private void UserSetCursorPosition(int newPosition)
		{
			if (newPosition > Length)
			{
				newPosition = Length;
			}

			if (newPosition < 0)
			{
				newPosition = 0;
			}

			CursorPosition = newPosition;
		}

		// Collapses selection: sets both start and end to cursor position
		private void ResetSelection()
		{
			SelectStart = SelectEnd = CursorPosition;
		}

		// Extends selection: moves end to cursor position, keeping start fixed
		private void UpdateSelection()
		{
			SelectEnd = CursorPosition;
		}

		// Updates selection based on shift key state: if shift held, extend; otherwise reset
		private void UpdateSelectionIfShiftDown()
		{
			if (Desktop.IsShiftDown)
			{
				UpdateSelection();
			}
			else
			{
				ResetSelection();
			}
		}

		// Moves cursor up or down one line while maintaining horizontal position (preferredX)
		private void MoveLine(int delta)
		{
			var line = _richTextLayout.GetLineByCursorPosition(CursorPosition);
			if (line == null)
			{
				return;
			}

			var newLine = line.LineIndex + delta;
			if (newLine < 0 || newLine >= _richTextLayout.Lines.Count)
			{
				return;
			}

			var bounds = ActualBounds;
			var pos = GetRenderPositionByIndex(CursorPosition);
			var preferredX = pos.X - bounds.X;  // Remember horizontal position

			// Find the glyph at the target line closest to the preferred X position
			var newString = _richTextLayout.Lines[newLine];
			var cursorPosition = newString.TextStartIndex;
			var glyphIndex = newString.GetGlyphIndexByX(preferredX);
			if (glyphIndex != null)
			{
				cursorPosition += glyphIndex.Value;
			}

			UserSetCursorPosition(cursorPosition);
			UpdateSelectionIfShiftDown();
		}

		/// <summary>
		/// Selects all text in the text box.
		/// </summary>
		public void SelectAll()
		{
			// Select all
			SelectStart = 0;
			SelectEnd = Length;
		}

		/// <summary>
		/// Called when a keyboard key is pressed while the text box has focus.
		/// Handles standard text editing operations: copy/paste/cut, undo/redo, navigation,
		/// text deletion, selection, and special keys like Insert, Home, End, etc.
		/// </summary>
		/// <param name="k">The key that was pressed.</param>
		public override void OnKeyDown(Keys k)
		{
			base.OnKeyDown(k);
			if (!Enabled)
			{
				return;
			}

			switch (k)
			{
				case Keys.C:
					if (Desktop.IsControlDown)
					{
						Copy();
					}
					break;

				case Keys.V:
					if (!Readonly && Desktop.IsControlDown)
					{
						string clipboardText;
						try
						{
							clipboardText = Clipboard.GetText();
						}
						catch (Exception)
						{
							clipboardText = MyraEnvironment.InternalClipboard;
						}

						if (!string.IsNullOrEmpty(clipboardText))
						{
							Paste(clipboardText);
						}
					}
					break;

				case Keys.X:
					if (Desktop.IsControlDown)
					{
						Copy();
						if (!Readonly && SelectStart != SelectEnd)
						{
							DeleteSelection();
						}
					}
					break;

				case Keys.D:
					if (!Readonly && Desktop.IsControlDown)
					{
						// nothing selected -> duplicate current line
						if (SelectStart == SelectEnd)
						{
							// get start of line
							var searchStart = Math.Max(0, SelectStart - 1);
							var lineStart = Text.LastIndexOf("\n", searchStart);
							// special case: cursor is in first line
							if (lineStart == -1) lineStart = 0;

							// get end of line
							var lineEnd = Text.IndexOf("\n", SelectEnd);
							// special case: cursor is in last line
							if (lineEnd == -1) lineEnd = Text.Length;

							var line = Text.Substring(lineStart, lineEnd - lineStart);
							if (lineStart == 0)
								line = "\n" + line;
							Insert(lineEnd, line);
						}
						// duplicate selection
						else
						{
							var start = Math.Min(SelectStart, SelectEnd);
							var end = Math.Max(SelectStart, SelectEnd);
							var text = Text.Substring(start, end - start);
							Insert(end, text);
						}
					}
					break;

				case Keys.Insert:
					if (!Readonly)
					{
						InsertMode = !InsertMode;
					}
					break;

				case Keys.Z:
					if (!Readonly && Desktop.IsControlDown)
					{
						Undo();
					}
					break;

				case Keys.Y:
					if (!Readonly && Desktop.IsControlDown)
					{
						Redo();
					}
					break;

				case Keys.A:
					if (Desktop.IsControlDown)
					{
						SelectAll();
					}
					break;

				case Keys.Left:
					if (CursorPosition > 0)
					{
						UserSetCursorPosition(CursorPosition - 1);
						UpdateSelectionIfShiftDown();
					}
					break;

				case Keys.Right:
					if (CursorPosition < Length)
					{
						UserSetCursorPosition(CursorPosition + 1);
						UpdateSelectionIfShiftDown();
					}
					break;

				case Keys.Up:
					MoveLine(-1);
					break;

				case Keys.Down:
					MoveLine(1);
					break;

				case Keys.Back:
					if (!Readonly)
					{
						if (SelectStart == SelectEnd)
						{
							int deleted = Delete(CursorPosition - 1, 1);
							if (deleted > 0)
							{
								UserSetCursorPosition(CursorPosition - deleted);
								ResetSelection();
							}
						}
						else
						{
							DeleteSelection();
						}
					}
					break;

				case Keys.Delete:
					if (!Readonly)
					{
						if (SelectStart == SelectEnd)
						{
							Delete(CursorPosition, 1);
						}
						else
						{
							DeleteSelection();
						}
					}
					break;

				case Keys.Home:
					{
						if (!Desktop.IsControlDown && !string.IsNullOrEmpty(Text))
						{
							var newPosition = CursorPosition;

							while (newPosition > 0 &&
								(newPosition - 1 >= Text.Length ||
								Text[newPosition - 1] != '\n'))
							{
								--newPosition;
							}

							UserSetCursorPosition(newPosition);
						}
						else
						{
							UserSetCursorPosition(0);
						}

						UpdateSelectionIfShiftDown();

						break;
					}

				case Keys.End:
					{
						if (!Desktop.IsControlDown)
						{
							var newPosition = CursorPosition;

							while (newPosition < Length && Text[newPosition] != '\n')
							{
								++newPosition;
							}

							UserSetCursorPosition(newPosition);
						}
						else
						{
							UserSetCursorPosition(Length);
						}

						UpdateSelectionIfShiftDown();

						break;
					}

				case Keys.Enter:
					if (!Readonly)
					{
						InputChar('\n');
					}
					break;
			}
		}

		// Copies selected text to clipboard, falling back to internal clipboard on failure
		private void Copy()
		{
			if (SelectEnd != SelectStart)
			{
				var selectStart = Math.Min(SelectStart, SelectEnd);
				var selectEnd = Math.Max(SelectStart, SelectEnd);

				var clipboardText = _richTextLayout.Text.Substring(selectStart, selectEnd - selectStart);
				try
				{
					Clipboard.SetText(clipboardText);
				}
				catch (Exception)
				{
					// Fallback if system clipboard is unavailable
					MyraEnvironment.InternalClipboard = clipboardText;
				}
			}
		}

		// Normalizes text by removing carriage returns (converts CRLF to LF)
		private static string Process(string value)
		{
			if (value != null)
			{
				value = value.Replace("\r", string.Empty);
			}

			return value;
		}

		// Sets text content, optionally firing events and managing undo stack
		// byUser=true indicates text was entered by user (not programmatic change)
		private bool SetText(string value, bool byUser)
		{
			value = Process(value);
			if (value == _text)
			{
				return false;
			}

			var oldValue = _text;
			// Fire ValueChanging event to allow cancellation or modification
			if (ValueChanging != null)
			{
				var args = new ValueChangingEventArgs<string>(oldValue, value);
				ValueChanging(this, args);
				if (args.Cancel)
				{
					return false;
				}

				value = args.NewValue;
			}

			_text = value;

			// Update layout to reflect new text (handles password masking, etc.)
			UpdateRichTextLayout();

			// Reset cursor and selection only if text was set programmatically
			if (!byUser)
			{
				CursorPosition = SelectStart = SelectEnd = 0;
			}

			// Reset redo stack on any programmatic text change (unless suppressed during undo/redo)
			if (!_suppressRedoStackReset)
			{
				RedoStack.Reset();
			}

			InvalidateMeasure();

			// Fire TextChanged event (always)
			TextChanged?.Invoke(this, new ValueChangedEventArgs<string>(oldValue, value));

			// Fire TextChangedByUser event (only for user input)
			if (byUser)
			{
				TextChangedByUser?.Invoke(this, new ValueChangedEventArgs<string>(oldValue, value));
			}

			return true;
		}

		// Updates the layout text: masks password fields and manages hint text display
		private void UpdateRichTextLayout()
		{
			if (string.IsNullOrEmpty(_text))
			{
				_richTextLayout.Text = _text;
				EnableHintText();
				return;
			}

			DisableHintText();
			// If password field, replace all characters with asterisks for security
			_richTextLayout.Text = PasswordField ? new string('*', _text.Length) : _text;
		}

		// Hides hint text and shows actual text
		private void DisableHintText()
		{
			if (_hintText == null)
			{
				return;
			}

			_richTextLayout.Text = _text;
			HintTextEnabled = false;
		}

		// Shows hint text if conditions are met (empty text, unfocused)
		private void EnableHintText()
		{
			if (ShouldEnableHintText())
			{
				_richTextLayout.Text = _hintText;
				HintTextEnabled = true;
			}
		}

		// Hint text should be shown when: hint is defined, text is empty, and textbox is not focused
		private bool ShouldEnableHintText()
		{
			return _hintText != null &&
				   string.IsNullOrEmpty(_text)
				   && !IsKeyboardFocused;
		}

		// Ensures the cursor is visible by scrolling the text box content if necessary.
		// Handles both internal scrolling and parent ScrollViewer scrolling.
		// Calculates visible viewport dimensions and adjusts scroll position to keep cursor in view.
		private void UpdateScrolling()
		{
			var p = GetRenderPositionByIndex(CursorPosition);
			if (p == _lastCursorPosition || Desktop == null)
			{
				return;
			}

			Desktop.UpdateLayout();

			var asScrollViewer = Parent as ScrollViewer;

			// Calculate viewport dimensions: available space for showing text
			Point sz, maximum;
			var bounds = ActualBounds;
			if (asScrollViewer != null)
			{
				// If parent is ScrollViewer, use its dimensions minus scrollbar widths
				sz = new Point(asScrollViewer.Bounds.Width, asScrollViewer.Bounds.Height);
				sz.X -= asScrollViewer.VerticalThumbWidth;
				sz.Y -= asScrollViewer.HorizontalThumbHeight;

				maximum = asScrollViewer.ScrollMaximum;
			}
			else
			{
				// Otherwise use our own bounds for internal scrolling
				sz = new Point(Bounds.Width, Bounds.Height);
				maximum = new Point(_richTextLayout.Size.X + CursorWidth - sz.X,
					_richTextLayout.Size.Y - sz.Y);

				if (maximum.X < 0)
				{
					maximum.X = 0;
				}

				if (maximum.Y < 0)
				{
					maximum.Y = 0;
				}
			}

			// No scrolling needed if content fits entirely in viewport
			if (maximum == Mathematics.PointZero)
			{
				_internalScrolling = Mathematics.PointZero;
				_lastCursorPosition = p;
				return;
			}

			// Convert cursor position to local coordinates relative to bounds
			p.X -= bounds.X;
			p.Y -= bounds.Y;

			var lineHeight = _richTextLayout.Font.LineHeight;

			// Get current scroll position from parent or internal scrolling
			Point sp;
			if (asScrollViewer != null)
			{
				sp = asScrollViewer.ScrollPosition;
			}
			else
			{
				sp = _internalScrolling;
			}

			// Scroll vertically: ensure cursor line is visible within viewport
			if (p.Y < sp.Y)
			{
				// Cursor above viewport: scroll up
				sp.Y = p.Y;
			}
			else if (p.Y + lineHeight > sp.Y + sz.Y)
			{
				// Cursor below viewport: scroll down to show line at bottom
				sp.Y = p.Y + lineHeight - sz.Y;
			}

			// Scroll horizontally: ensure cursor is visible within viewport
			if (p.X < sp.X)
			{
				// Cursor left of viewport: scroll left
				sp.X = p.X;
			}
			else if (p.X + CursorWidth > sp.X + sz.X)
			{
				// Cursor right of viewport: scroll right to show cursor at edge
				sp.X = p.X + CursorWidth - sz.X;
			}

			// Apply bounds checking and update scroll position on parent or internally
			if (asScrollViewer != null)
			{
				// Clamp scroll position to valid range [0, maximum]
				if (sp.X < 0)
				{
					sp.X = 0;
				}

				if (sp.X > maximum.X)
				{
					sp.X = maximum.X;
				}

				if (sp.Y < 0)
				{
					sp.Y = 0;
				}

				if (sp.Y > maximum.Y)
				{
					sp.Y = maximum.Y;
				}

				asScrollViewer.ScrollPosition = sp;
			}
			else
			{
				// Clamp internal scrolling to valid range
				if (sp.X < 0)
				{
					sp.X = 0;
				}

				if (sp.X > maximum.X)
				{
					sp.X = maximum.X;
				}

				if (sp.Y < 0)
				{
					sp.Y = 0;
				}

				if (sp.Y > maximum.Y)
				{
					sp.Y = maximum.Y;
				}

				_internalScrolling = sp;
			}

			_lastCursorPosition = p;
		}

		// Called whenever the cursor position changes. Resets cursor blink timing, scrolls viewport to keep cursor visible, and fires position-changed event.
		private void OnCursorIndexChanged()
		{
			// Reset blink animation: cursor movement makes cursor appear immediately
			_lastCursorUpdate = DateTime.Now;

			// Ensure cursor is visible in viewport by scrolling if needed
			UpdateScrolling();

			// Notify listeners that cursor position has changed
			CursorPositionChanged.Invoke(this, InputEventType.CursorPositionChanged);
		}

		/// <summary>
		/// Called when a character is entered into the text box.
		/// </summary>
		/// <param name="c">The character that was entered.</param>
		public override void OnChar(char c)
		{
			base.OnChar(c);

			if (!Enabled)
			{
				return;
			}

			if (!Readonly && !char.IsControl(c))
			{
				InputChar(c);
			}
		}

		// Sets cursor position based on touch/click coordinates, with optional selection extension
		private void SetCursorByTouch()
		{
			if (Desktop == null)
			{
				return;
			}

			var mousePos = ToLocal(Desktop.TouchPosition.Value);
			// Account for internal scrolling offset
			mousePos.X += _internalScrolling.X;
			mousePos.Y += _internalScrolling.Y;

			// Clamp position to valid bounds
			if (mousePos.X < 0)
			{
				mousePos.X = 0;
			}

			if (mousePos.Y < 0)
			{
				mousePos.Y = 0;
			}

			// Find which line the touch is on, then find the glyph at that X position
			var line = _richTextLayout.GetLineByY(mousePos.Y);
			if (line != null)
			{
				var glyphIndex = line.GetGlyphIndexByX(mousePos.X);
				if (glyphIndex != null)
				{
					UserSetCursorPosition(line.TextStartIndex + glyphIndex.Value);
					// Extend selection if dragging or shift is held, otherwise reset selection
					if (_isTouchDown || Desktop.IsShiftDown)
					{
						UpdateSelection();
					}
					else
					{
						ResetSelection();
					}
				}
			}
		}

		// Handles end of touch/drag operation
		private void DesktopTouchUp(object sender, MyraEventArgs args)
		{
			_isTouchDown = false;
		}

		// Handles start of touch/drag operation
		private void DesktopTouchDown(object sender, MyraEventArgs e)
		{
			if (!Enabled || !IsTouchInside || Length == 0)
			{
				return;
			}

			SetCursorByTouch();

			_lastCursorUpdate = DateTime.Now;
			_isTouchDown = true;
		}

		/// <summary>
		/// Called when the text box receives a double-tap touch event, selecting the word at the tap position.
		/// Double-clicking on whitespace or when shift is held does nothing.
		/// </summary>
		public override void OnTouchDoubleClick()
		{
			base.OnTouchDoubleClick();

			var position = CursorPosition;
			if (string.IsNullOrEmpty(Text) || position < 0 || position >= Text.Length || Desktop.IsShiftDown)
			{
				return;
			}

			// If cursor is on whitespace, move to adjacent non-whitespace
			if (char.IsWhiteSpace(Text[position]))
			{
				if (position == 0)
				{
					return;
				}

				--position;
				if (char.IsWhiteSpace(Text[position]))
				{
					return;
				}
			}

			// Find word boundaries by scanning for whitespace in both directions
			int start, end;
			start = end = position;

			// Find start of word (scan left until whitespace)
			while (start > 0)
			{
				if (char.IsWhiteSpace(Text[start]))
				{
					++start;
					break;
				}

				--start;
			}

			// Find end of word (scan right until whitespace)
			while (end < Text.Length)
			{
				if (char.IsWhiteSpace(Text[end]))
				{
					break;
				}

				++end;
			}

			if (start == end)
			{
				return;
			}

			// Select the word
			SelectStart = start;
			SelectEnd = end;
		}

		/// <summary>
		/// Called when the text box receives keyboard focus.
		/// Resets cursor blink state and hides hint text if present.
		/// </summary>
		public override void OnGotKeyboardFocus()
		{
			base.OnGotKeyboardFocus();

			// Reset blink animation: show cursor immediately
			_lastBlinkStamp = DateTime.Now;
			_cursorOn = true;

			// Hide hint text when focused
			DisableHintText();
		}

		/// <summary>
		/// Called when the text box loses keyboard focus.
		/// Shows hint text if text is empty.
		/// </summary>
		public override void OnLostKeyboardFocus()
		{
			base.OnLostKeyboardFocus();

			// Show hint text if conditions are met
			EnableHintText();
		}

		// Calculates the screen position of the cursor for the given text index.
		// Handles edge cases: index within text (use glyph info), after last glyph (position after last char),
		// and empty lines (position at start of empty line). Returns coordinates in screen space.
		private Point GetRenderPositionByIndex(int index)
		{
			var bounds = ActualBounds;

			// Start at textbox origin
			var x = bounds.X;
			var y = bounds.Y;

			if (Text != null)
			{
				if (index < Text.Length)
				{
					// Index is within text: get glyph info and use its position
					var glyphRender = _richTextLayout.GetGlyphInfoByIndex(index);
					if (glyphRender != null)
					{
						x += glyphRender.Value.Bounds.Left;
						y += glyphRender.Value.LineTop;
					}
				}
				else if (_richTextLayout.Lines != null && _richTextLayout.Lines.Count > 0)
				{
					// Index is at or past end of text: position cursor after last glyph or on empty last line
					var lastLine = _richTextLayout.Lines[_richTextLayout.Lines.Count - 1];
					if (lastLine.Count > 0)
					{
						// Last line has glyphs: position after the last glyph
						var glyphRender = lastLine.GetGlyphInfoByIndex(lastLine.Count - 1);

						x += glyphRender.Value.Bounds.Left + glyphRender.Value.XAdvance;
						y += glyphRender.Value.LineTop;
					}
					else if (_richTextLayout.Lines.Count > 1)
					{
						// Last line is empty: position at start of empty line below previous line
						var previousLine = _richTextLayout.Lines[_richTextLayout.Lines.Count - 2];
						if (previousLine.Count > 0)
						{
							var glyphRender = previousLine.GetGlyphInfoByIndex(0);
							y += glyphRender.Value.LineTop + lastLine.Size.Y + _richTextLayout.VerticalSpacing;
						}
					}
				}
			}

			return new Point(x, y);
		}

		// Draws selection highlight rectangles over selected text.
		// Handles multi-line selections by drawing one rectangle per line of text.
		// For single-line selections, draws one rectangle. For multi-line, draws full-width
		// rectangles on intermediate lines and partial rectangles on first/last lines.
		private void RenderSelection(RenderContext context)
		{
			var bounds = ActualBounds;

			// Skip rendering if no text or no selection brush
			if (string.IsNullOrEmpty(Text) || Selection == null)
			{
				return;
			}

			// Normalize selection: ensure selectStart <= selectEnd
			var selectStart = Math.Min(SelectStart, SelectEnd);
			var selectEnd = Math.Max(SelectStart, SelectEnd);

			// Skip if no actual selection
			if (selectStart >= selectEnd)
			{
				return;
			}

			// Get starting glyph to determine initial line
			var startGlyph = _richTextLayout.GetGlyphInfoByIndex(selectStart);
			if (startGlyph == null)
			{
				return;
			}

			var lineIndex = startGlyph.Value.TextChunk.LineIndex;
			var i = selectStart;

			var lineHeight = _richTextLayout.Font.LineHeight;
			while (true)
			{
				// Get glyph at current position in selection
				startGlyph = _richTextLayout.GetGlyphInfoByIndex(i);
				if (startGlyph == null)
				{
					break;
				}

				var startPosition = GetRenderPositionByIndex(i);

				var line = _richTextLayout.Lines[startGlyph.Value.TextChunk.LineIndex];

				// Check if selection ends on this line
				if (selectEnd < line.TextStartIndex + line.Count)
				{
					// Single-line selection: draw from start to end within this line
					var endPosition = GetRenderPositionByIndex(selectEnd);

					Selection.Draw(context,
						new Rectangle(startPosition.X - _internalScrolling.X,
							startPosition.Y - _internalScrolling.Y,
							endPosition.X - startPosition.X,
							lineHeight));

					break;
				}

				// Multi-line selection: draw from start position to end of this line
				Selection.Draw(context,
					new Rectangle(startPosition.X - _internalScrolling.X,
						startPosition.Y - _internalScrolling.Y,
						bounds.Left + startGlyph.Value.TextChunk.Size.X - startPosition.X,
						lineHeight));

				// Move to next line
				++lineIndex;
				if (lineIndex >= _richTextLayout.Lines.Count)
				{
					break;
				}

				i = _richTextLayout.Lines[lineIndex].TextStartIndex;
			}
		}

		/// <summary>
		/// Renders the text box's content, including text, cursor, and selection.
		/// Handles text color selection (normal/disabled/focused), cursor blinking, and selection highlighting.
		/// </summary>
		/// <param name="context">The render context to draw with.</param>
		public override void InternalRender(RenderContext context)
		{
			if (_richTextLayout.Font == null)
			{
				return;
			}

			// Handle continuous scrolling when touch/drag is outside bounds
			if (_isTouchDown)
			{
				var passed = DateTime.Now - _lastCursorUpdate;
				if (passed.TotalMilliseconds > CursorUpdateDelayInMs)
				{
					SetCursorByTouch();
					_lastCursorUpdate = DateTime.Now;
				}
			}

			var bounds = ActualBounds;
			RenderSelection(context);

			// Determine text color based on state: hint/disabled/focused
			var textColor = TextColor;
			var oldOpacity = context.Opacity;

			if (HintTextEnabled)
			{
				// Hint text is semi-transparent
				context.Opacity *= 0.5f;
			}
			else if (!Enabled && DisabledTextColor != null)
			{
				// Use special color for disabled state
				textColor = DisabledTextColor.Value;
			}
			else if (IsKeyboardFocused && FocusedTextColor != null)
			{
				// Use special color when focused
				textColor = FocusedTextColor.Value;
			}

			// Align text within bounds based on TextVerticalAlignment
			var centeredBounds = LayoutUtils.Align(new Point(bounds.Width, bounds.Height), _richTextLayout.Size, HorizontalAlignment.Left, TextVerticalAlignment);
			centeredBounds.Offset(bounds.Location);

			// Apply internal scrolling offset to position calculation
			var p = new Point(centeredBounds.Location.X - _internalScrolling.X,
				centeredBounds.Location.Y - _internalScrolling.Y);

			// Debug visualization: draw glyph bounding boxes
			if (MyraEnvironment.DrawTextGlyphsFrames)
			{
				foreach (var line in _richTextLayout.Lines)
				{
					foreach (TextChunk chunk in line.Chunks)
					{
						foreach (var glyph in chunk.Glyphs)
						{
							var glyphBounds = glyph.Bounds;
							glyphBounds.Offset(p);
							context.DrawRectangle(glyphBounds, Color.White);
						}
					}
				}
			}

			// Draw the text
			context.DrawRichText(_richTextLayout, new Vector2(p.X, p.Y), textColor);

			// Skip cursor rendering if textbox doesn't have focus
			if (!IsKeyboardFocused)
			{
				context.Opacity = oldOpacity;
				return;
			}

			// Update cursor blink animation state
			var now = DateTime.Now;

			if (_lastCursorUpdate > _lastBlinkStamp)
			{
				// Cursor movement resets blink: show cursor immediately
				_lastBlinkStamp = _lastCursorUpdate;
				_cursorOn = true;
			}

			// Toggle cursor visibility based on blink interval
			if ((now - _lastBlinkStamp).TotalMilliseconds >= BlinkIntervalInMs)
			{
				_cursorOn = !_cursorOn;
				_lastBlinkStamp = now;
			}

			// Draw the cursor image at the current position
			if (Enabled && _cursorOn && Cursor != null)
			{
				p = GetRenderPositionByIndex(CursorPosition);

				p.X -= _internalScrolling.X;
				p.Y -= _internalScrolling.Y;

				var rect = new Rectangle(p.X, p.Y, Cursor.Size.X, _richTextLayout.Font.LineHeight);
				Cursor.Draw(context, rect);
			}

			context.Opacity = oldOpacity;
		}

		/// <summary>
		/// Measures the size required to display the text box contents.
		/// Accounts for text wrapping, cursor width, and minimum line height.
		/// </summary>
		/// <param name="availableSize">The available size for the text box.</param>
		/// <returns>The measured size needed for the text box.</returns>
		protected override Point InternalMeasure(Point availableSize)
		{
			if (Font == null)
			{
				return Mathematics.PointZero;
			}

			var width = availableSize.X;
			width -= CursorWidth;

			// Measure text with or without wrapping depending on Wrap property
			var result = Mathematics.PointZero;
			if (Font != null)
			{
				result = _richTextLayout.Measure(_wrap ? width : default(int?));
			}

			// Ensure minimum height for at least one line of text
			if (result.Y < Font.LineHeight)
			{
				result.Y = Font.LineHeight;
			}

			// Account for cursor width in total size
			if (Cursor != null)
			{
				result.X += CursorWidth;
				result.Y = Math.Max(result.Y, Cursor.Size.Y);
			}

			return result;
		}

		/// <summary>
		/// Arranges the text box's content within its bounds.
		/// Sets the layout width for text wrapping.
		/// </summary>
		protected override void InternalArrange()
		{
			base.InternalArrange();

			var width = ActualBounds.Width;
			width -= CursorWidth;

			// Set wrapping width for layout engine: null means no wrapping
			_richTextLayout.Width = _wrap ? width : default(int?);
		}

		/// <summary>
		/// Applies a text box style to the text box, setting colors, cursor, selection, and font.
		/// </summary>
		/// <param name="style">The text box style to apply.</param>
		public void ApplyTextBoxStyle(TextBoxStyle style)
		{
			ApplyWidgetStyle(style);

			TextColor = style.TextColor;
			DisabledTextColor = style.DisabledTextColor;
			FocusedTextColor = style.FocusedTextColor;

			Cursor = style.Cursor;
			Selection = style.Selection;

			Font = style.Font;
		}

		/// <summary>
		/// Applies a named text box style from the stylesheet to the text box.
		/// </summary>
		/// <param name="stylesheet">The stylesheet containing the style.</param>
		/// <param name="name">The name of the text box style to apply.</param>
		protected override void InternalSetStyle(Stylesheet stylesheet, string name)
		{
			ApplyTextBoxStyle(stylesheet.TextBoxStyles.SafelyGetStyle(name));
		}

		/// <summary>
		/// Gets the width of the character at the specified text index.
		/// </summary>
		/// <param name="index">The index of the character in the text.</param>
		/// <returns>The width of the character, or 0 if the index is out of bounds or the character is a newline.</returns>
		public float GetWidth(int index)
		{
			var glyph = _richTextLayout.GetGlyphInfoByIndex(index);
			if (glyph == null)
			{
				return 0;
			}

			if (glyph.Value.Codepoint == '\n')
			{
				return 0;
			}

			return glyph.Value.Bounds.Width;
		}

		/// <summary>
		/// Copies all properties from another widget to this text box.
		/// </summary>
		/// <param name="w">The widget to copy properties from.</param>
		protected internal override void CopyFrom(Widget w)
		{
			base.CopyFrom(w);

			var textBox = (TextBox)w;
			VerticalSpacing = textBox.VerticalSpacing;
			Text = textBox.Text;
			HintText = textBox.HintText;
			HintTextEnabled = textBox.HintTextEnabled;
			Multiline = textBox.Multiline;
			Font = textBox.Font;
			Wrap = textBox.Wrap;
			TextColor = textBox.TextColor;
			DisabledTextColor = textBox.DisabledTextColor;
			FocusedTextColor = textBox.FocusedTextColor;
			Cursor = textBox.Cursor;
			Selection = textBox.Selection;
			BlinkIntervalInMs = textBox.BlinkIntervalInMs;
			Readonly = textBox.Readonly;
			PasswordField = textBox.PasswordField;
			TextVerticalAlignment = textBox.TextVerticalAlignment;
		}
	}
}