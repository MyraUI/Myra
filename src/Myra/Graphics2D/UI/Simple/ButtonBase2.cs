using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using System.ComponentModel;
using System.Xml.Serialization;
using System;
using Myra.Events;

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// An abstract base class for button-like content controls that can be pressed and respond to click events.
	/// </summary>
	public abstract class ButtonBase2 : ContentControl
	{
		public ButtonBase2 FocusNeighbourTop = null;
		public ButtonBase2 FocusNeighbourBottom = null;
		public ButtonBase2 FocusNeighbourLeft = null;
		public ButtonBase2 FocusNeighbourRight = null;
		public ButtonBase2 FocusNeighbourPrevious = null;
		public ButtonBase2 FocusNeighbourNext = null;

		private bool _isPressed = false;
		private bool _isClicked = false;

		/// <summary>
		/// Gets or sets the brush used to draw the background when the button is pressed.
		/// </summary>
		[Category("Appearance")]
		public virtual IBrush PressedBackground { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the button is currently in the pressed state.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public virtual bool IsPressed
		{
			get
			{
				return _isPressed;
			}

			set
			{
				if (value == _isPressed)
				{
					return;
				}

				_isPressed = value;
				OnPressedChanged();
			}
		}

		/// <summary>
		/// Occurs when the button is clicked.
		/// </summary>
		public event MyraEventHandler Click;

		/// <summary>
		/// Occurs when the pressed state changes.
		/// </summary>
		public event MyraEventHandler PressedChanged;

		/// <summary>
		/// Occurs when the pressed state is about to change due to user interaction. Set Cancel to true to prevent the change.
		/// </summary>
		public event MyraEventHandler<ValueChangingEventArgs<bool>> PressedChangingByUser;


		/// <summary>
		/// Simulates a click on the button by firing touch down and touch up events.
		/// </summary>
		public void DoClick()
		{
			OnTouchDown();
			OnTouchUp();
		}

		/// <summary>
		/// Raises the pressed changed event and updates the content if it implements IPressable.
		/// </summary>
		public virtual void OnPressedChanged()
		{
			PressedChanged.Invoke(this, InputEventType.PressedChanged);

			var asPressable = Content as IPressable;
			if (asPressable != null)
			{
				asPressable.IsPressed = IsPressed;
			}
		}

		/// <summary>
		/// Sets the pressed state by user interaction, raising the PressedChangingByUser event.
		/// </summary>
		/// <param name="value">The new pressed state value.</param>
		protected void SetValueByUser(bool value)
		{
			if (value != IsPressed && PressedChangingByUser != null)
			{
				var args = new ValueChangingEventArgs<bool>(_isPressed, value);
				PressedChangingByUser(this, args);

				if (args.Cancel)
				{
					return;
				}
			}

			IsPressed = value;
		}

		/// <summary>
		/// Called when a touch point is released on the button. Derived classes should override to implement custom touch up behavior.
		/// </summary>
		protected abstract void InternalOnTouchUp();
		/// <summary>
		/// Called when a touch point is pressed on the button. Derived classes should override to implement custom touch down behavior.
		/// </summary>
		protected abstract void InternalOnTouchDown();

		/// <summary>
		/// Handles touch up events on the button.
		/// </summary>
		public override void OnTouchUp()
		{
			base.OnTouchUp();

			if (!Enabled)
			{
				return;
			}

			InternalOnTouchUp();

			if (_isClicked)
			{
				Click.Invoke(this, InputEventType.TouchUp);
				_isClicked = false;
			}
		}

		/// <summary>
		/// Handles touch down events on the button.
		/// </summary>
		public override void OnTouchDown()
		{
			base.OnTouchDown();

			if (!Enabled)
			{
				return;
			}

			InternalOnTouchDown();

			_isClicked = true;
		}

		/// <summary>
		/// Gets the current background brush based on button state (pressed, hovered, disabled, or normal).
		/// </summary>
		/// <returns>The background brush to use for rendering.</returns>
		public override IBrush GetCurrentBackground()
		{
			var result = base.GetCurrentBackground();

			if (Enabled)
			{
				if (IsPressed && PressedBackground != null)
				{
					result = PressedBackground;
				}
				else if (UseOverBackground && OverBackground != null)
				{
					result = OverBackground;
				}
			}
			else
			{
				if (DisabledBackground != null)
				{
					result = DisabledBackground;
				}
			}

			return result;
		}

		/// <summary>
		/// Applies the specified button style to the button.
		/// </summary>
		/// <param name="style">The style to apply.</param>
		public void ApplyButtonStyle(ButtonStyle style)
		{
			ApplyWidgetStyle(style);

			PressedBackground = style.PressedBackground;
		}

		/// <summary>
		/// Applies the specified image button style to the button.
		/// </summary>
		/// <param name="style">The image button style to apply.</param>
		public void ApplyImageButtonStyle(ImageButtonStyle style)
		{
			ApplyButtonStyle(style);

			if (style.ImageStyle != null)
			{
				var image = (Image)Content;
				image.ApplyPressableImageStyle(style.ImageStyle);
			}
		}

		/// <summary>
		/// Applies a named button style from the stylesheet to the button.
		/// </summary>
		/// <param name="stylesheet">The stylesheet containing the style.</param>
		/// <param name="name">The name of the button style to apply.</param>
		protected override void InternalSetStyle(Stylesheet stylesheet, string name)
		{
			ApplyButtonStyle(stylesheet.ButtonStyles.SafelyGetStyle(name));
		}

		/// <summary>
		/// Copies the button properties from another button.
		/// </summary>
		/// <param name="w">The source button to copy from.</param>
		protected internal override void CopyFrom(Widget w)
		{
			base.CopyFrom(w);

			var buttonBase = (ButtonBase2)w;
			PressedBackground = buttonBase.PressedBackground;
			IsPressed = buttonBase.IsPressed;
		}
	}
}
