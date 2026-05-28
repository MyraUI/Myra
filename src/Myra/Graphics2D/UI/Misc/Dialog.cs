using System.ComponentModel;
using Myra.Graphics2D.UI.Styles;
using System.Xml.Serialization;
using Myra.Attributes;

#if MONOGAME || FNA
using Microsoft.Xna.Framework.Input;
#elif STRIDE
using Stride.Input;
#else
using Myra.Platform;
#endif

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// A window dialog with OK and Cancel buttons for user interaction.
	/// </summary>
	[StyleTypeName("Window")]
	public class Dialog : Window
	{
		/// <summary>
		/// Gets the OK button of the dialog.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Button ButtonOk { get; private set; }

		/// <summary>
		/// Gets the Cancel button of the dialog.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Button ButtonCancel { get; private set; }

		/// <summary>
		/// Gets or sets the key that confirms the dialog (triggers the OK button).
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(Keys.Enter)]
		public Keys ConfirmKey { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="Dialog"/> class with the specified style.
		/// </summary>
		/// <param name="styleName">The name of the style to apply. Defaults to the default stylesheet style.</param>
		public Dialog(string styleName = Stylesheet.DefaultStyleName) : base(styleName)
		{
			ConfirmKey = Keys.Enter;

			var buttonsPanel = new HorizontalStackPanel()
			{
				Spacing = 8,
				HorizontalAlignment = HorizontalAlignment.Right
			};

			ButtonOk = new Button
			{
				Content = new Label
				{
					Text = "Ok"
				}
			};

			ButtonOk.Click += (sender, args) =>
			{
				OnOk();
			};

			buttonsPanel.Widgets.Add(ButtonOk);

			ButtonCancel = new Button
			{
				Content = new Label
				{
					Text = "Cancel",
				}
			};

			ButtonCancel.Click += (sender, args) =>
			{
				Result = false;
				Close();
			};

			buttonsPanel.Widgets.Add(ButtonCancel);
			Children.Add(buttonsPanel);
		}

		/// <summary>
		/// Handles key down events for the dialog, including confirm and close key shortcuts.
		/// </summary>
		/// <param name="k">The key that was pressed.</param>
		public override void OnKeyDown(Keys k)
		{
			FireKeyDown(k);

			if (k == CloseKey)
			{
				CloseButton.DoClick();
			}
			else if (k == ConfirmKey)
			{
				ButtonOk.DoClick();
			}
		}

		/// <summary>
		/// Called when the OK button is clicked. Override to handle OK action.
		/// </summary>
		protected internal virtual void OnOk()
		{
			if (!CanCloseByOk())
			{
				return;
			}

			Result = true;
			Close();
		}

		/// <summary>
		/// Determines whether the dialog can be closed by the OK button. Override to implement validation.
		/// </summary>
		/// <returns>true if the dialog can close; otherwise false.</returns>
		protected internal virtual bool CanCloseByOk()
		{
			return true;
		}

		/// <summary>
		/// Copies properties from another Dialog widget.
		/// </summary>
		/// <param name="w">The Dialog widget to copy from.</param>
		protected internal override void CopyFrom(Widget w)
		{
			base.CopyFrom(w);

			var dialog = (Dialog)w;
			ConfirmKey = dialog.ConfirmKey;
		}


		/// <summary>
		/// Creates a message box dialog with the specified title and content widget.
		/// </summary>
		/// <param name="title">The title of the message box.</param>
		/// <param name="content">The content widget to display in the message box.</param>
		/// <returns>A new Dialog configured as a message box.</returns>
		public static Dialog CreateMessageBox(string title, Widget content)
		{
			var w = new Dialog
			{
				Title = title,
				Content = content
			};

			return w;
		}

		/// <summary>
		/// Creates a message box dialog with the specified title and message text.
		/// </summary>
		/// <param name="title">The title of the message box.</param>
		/// <param name="message">The message text to display.</param>
		/// <returns>A new Dialog configured as a message box with a text label.</returns>
		public static Dialog CreateMessageBox(string title, string message)
		{
			var messageLabel = new Label
			{
				Text = message,
				Wrap = true
			};

			return CreateMessageBox(title, messageLabel);
		}
	}
}