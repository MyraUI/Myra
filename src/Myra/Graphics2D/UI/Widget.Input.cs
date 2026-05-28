using Myra.Utility;
using System;
using System.ComponentModel;
using System.Xml.Serialization;
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
#endif

namespace Myra.Graphics2D.UI
{
	partial class Widget : IInputEventsProcessor
	{
		// Touch/double-click detection
		private DateTime? _lastTouchDown;  // Timestamp of last touch down for double-click detection
		private DateTime? _lastMouseMovement;  // Timestamp of last mouse movement (used by tooltip delay logic)
		private Point _lastLocalTouchPosition;  // Position of last touch down for distance checking

		// Current input positions in local widget coordinates (null if input not over widget)
		private Point? _localMousePosition;  // Mouse position relative to this widget, or null if mouse not over widget
		private Point? _localTouchPosition;  // Touch position relative to this widget, or null if not being touched

		/// <summary>
		/// Gets a value indicating whether the mouse pointer is currently inside this widget.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public bool IsMouseInside => _localMousePosition != null;

		/// <summary>
		/// Gets or sets the local coordinates of the mouse pointer relative to this widget, or null if the mouse is not over the widget.
		/// Setting this property automatically queues MouseEntered, MouseLeft, or MouseMoved events as appropriate.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Point? LocalMousePosition
		{
			get => _localMousePosition;
			private set
			{
				if (value == _localMousePosition)
				{
					return;
				}

				var oldValue = _localMousePosition;
				_localMousePosition = value;

				if (Desktop == null)
				{
					return;
				}

				// Detect state transition and queue appropriate event
				if (value != null && oldValue == null)
				{
					// Mouse entered widget
					InputEventsManager.Queue(this, InputEventType.MouseEntered);
				}
				else if (value == null && oldValue != null)
				{
					// Mouse left widget
					InputEventsManager.Queue(this, InputEventType.MouseLeft);
				}
				else if (value != null && oldValue != null && value.Value != oldValue.Value)
				{
					// Mouse moved within widget
					InputEventsManager.Queue(this, InputEventType.MouseMoved);
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether a touch point is currently inside this widget.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public bool IsTouchInside => _localTouchPosition != null;

		/// <summary>
		/// Gets or sets the local coordinates of the touch point relative to this widget, or null if there is no active touch on the widget.
		/// Setting this property automatically queues TouchDown, TouchUp, TouchEntered, TouchLeft, or TouchMoved events as appropriate.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Point? LocalTouchPosition
		{
			get => _localTouchPosition;
			private set
			{
				if (value == _localTouchPosition)
				{
					return;
				}

				var oldValue = _localTouchPosition;
				_localTouchPosition = value;

				if (Desktop == null)
				{
					return;
				}

				// Detect state transition and queue appropriate event
				if (value != null && oldValue == null)
				{
					if (Desktop.PreviousTouchPosition == null)
					{
						// Touch down: new touch starting (Desktop.PreviousTouchPosition null means this is new touch)
						InputEventsManager.Queue(this, InputEventType.TouchDown);
						ProcessDoubleClick(value.Value);  // Check for double-click
					}
					else
					{
						// Touch entered: touch moved onto widget from elsewhere
						InputEventsManager.Queue(this, InputEventType.TouchEntered);
					}
				}
				else if (value == null && oldValue != null)
				{
					if (Desktop.TouchPosition == null)
					{
						// Touch up: entire touch released
						InputEventsManager.Queue(this, InputEventType.TouchUp);
					}
					else
					{
						// Touch left: touch moved away from widget
						InputEventsManager.Queue(this, InputEventType.TouchLeft);
					}
				}
				else if (value != null && oldValue != null && value.Value != oldValue.Value)
				{
					// Touch moved within widget
					InputEventsManager.Queue(this, InputEventType.TouchMoved);
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether this widget accepts mouse wheel input.
		/// </summary>
		protected internal virtual bool AcceptsMouseWheel => false;

		/// <summary>
		/// Occurs when the widget's position has changed.
		/// </summary>
		public event MyraEventHandler PlacedChanged;

		/// <summary>
		/// Occurs when the widget's visibility has changed.
		/// </summary>
		public event MyraEventHandler VisibleChanged;

		/// <summary>
		/// Occurs when the widget's enabled state has changed.
		/// </summary>
		public event MyraEventHandler EnabledChanged;

		/// <summary>
		/// Occurs when the widget's location has changed.
		/// </summary>
		public event MyraEventHandler LocationChanged;

		/// <summary>
		/// Occurs when the widget's size has changed.
		/// </summary>
		public event MyraEventHandler SizeChanged;

		/// <summary>
		/// Occurs when the widget's layout arrangement has been updated.
		/// </summary>
		public event MyraEventHandler ArrangeUpdated;

		/// <summary>
		/// Occurs when the mouse pointer leaves the widget.
		/// </summary>
		public event MyraEventHandler MouseLeft;

		/// <summary>
		/// Occurs when the mouse pointer enters the widget.
		/// </summary>
		public event MyraEventHandler MouseEntered;

		/// <summary>
		/// Occurs when the mouse pointer moves within the widget.
		/// </summary>
		public event MyraEventHandler MouseMoved;

		/// <summary>
		/// Occurs when a touch point leaves the widget.
		/// </summary>
		public event MyraEventHandler TouchLeft;

		/// <summary>
		/// Occurs when a touch point enters the widget.
		/// </summary>
		public event MyraEventHandler TouchEntered;

		/// <summary>
		/// Occurs when a touch point moves within the widget.
		/// </summary>
		public event MyraEventHandler TouchMoved;

		/// <summary>
		/// Occurs when a touch point is pressed on the widget.
		/// </summary>
		public event MyraEventHandler TouchDown;

		/// <summary>
		/// Occurs when a touch point is released on the widget.
		/// </summary>
		public event MyraEventHandler TouchUp;

		/// <summary>
		/// Occurs when the widget receives a double-tap touch event.
		/// </summary>
		public event MyraEventHandler TouchDoubleClick;

		/// <summary>
		/// Occurs when the keyboard focus on the widget has changed.
		/// </summary>
		public event MyraEventHandler KeyboardFocusChanged;

		/// <summary>
		/// Occurs when the mouse wheel is scrolled while over the widget.
		/// </summary>
		public event MyraEventHandler<GenericEventArgs<float>> MouseWheelChanged;

		/// <summary>
		/// Occurs when a key is released while the widget has keyboard focus.
		/// </summary>
		public event MyraEventHandler<GenericEventArgs<Keys>> KeyUp;

		/// <summary>
		/// Occurs when a key is pressed while the widget has keyboard focus.
		/// </summary>
		public event MyraEventHandler<GenericEventArgs<Keys>> KeyDown;

		/// <summary>
		/// Occurs when a character is entered while the widget has keyboard focus.
		/// </summary>
		public event MyraEventHandler<GenericEventArgs<char>> Char;

		// Detects double-click: checks if second touch down occurs within interval and distance threshold
		private void ProcessDoubleClick(Point touchPos)
		{
			if (_lastTouchDown != null &&
				(DateTime.Now - _lastTouchDown.Value).TotalMilliseconds < MyraEnvironment.DoubleClickIntervalInMs &&
				Math.Abs(touchPos.X - _lastLocalTouchPosition.X) <= MyraEnvironment.DoubleClickRadius &&
				Math.Abs(touchPos.Y - _lastLocalTouchPosition.Y) <= MyraEnvironment.DoubleClickRadius)
			{
				// Double-click detected: second touch within time and distance threshold
				_lastTouchDown = null;  // Reset for next potential double-click
				InputEventsManager.Queue(this, InputEventType.TouchDoubleClick);
			}
			else
			{
				// Record first touch for double-click detection
				_lastTouchDown = DateTime.Now;
				_lastLocalTouchPosition = LocalTouchPosition.Value;
			}
		}

		/// <summary>
		/// Processes input events for this widget, including mouse and touch input.
		/// Performs hit-testing, updates input positions, and recursively processes children.
		/// Marks input as handled if widget consumes it, preventing propagation to parent widgets.
		/// </summary>
		/// <param name="inputContext">The input context containing the current input state and handling flags.</param>
		protected internal virtual void ProcessInput(InputContext inputContext)
		{
			if (!Visible || Desktop == null)
			{
				return;
			}

			if (!inputContext.MouseOrTouchHandled)
			{
				// Input not yet consumed: perform hit-testing and propagate to children
				var oldContainsMouse = inputContext.ParentContainsMouse;
				var oldContainsTouch = inputContext.ParentContainsTouch;

				// Hit-test mouse on non-mobile platforms
				if (!Desktop.IsMobile)
				{
					if (inputContext.ParentContainsMouse)
					{
						if (ContainsGlobalPoint(Desktop.MousePosition))
						{
							// Mouse is over this widget
							LocalMousePosition = ToLocal(Desktop.MousePosition);
						}
						else
						{
							// Mouse moved away from widget
							LocalMousePosition = null;
							inputContext.ParentContainsMouse = false;  // Stop testing deeper
						}
					}
					else
					{
						LocalMousePosition = null;
					}
				}

				// Hit-test touch
				if (Desktop.TouchPosition != null && inputContext.ParentContainsTouch)
				{
					if (ContainsGlobalPoint(Desktop.TouchPosition.Value))
					{
						// Touch is over this widget
						LocalTouchPosition = ToLocal(Desktop.TouchPosition.Value);
					}
					else
					{
						// Touch moved away from widget
						LocalTouchPosition = null;
						inputContext.ParentContainsTouch = false;  // Stop testing deeper
					}
				}
				else
				{
					LocalTouchPosition = null;
				}

				// Check if widget accepts mouse wheel
				if (IsMouseInside &&
					!Desktop.MouseWheelDelta.IsZero() &&
					AcceptsMouseWheel)
				{
					inputContext.MouseWheelWidget = this;
				}

				// Recursively process children (back-to-front order for proper z-order)
				for (var i = _childrenCopy.Count - 1; i >= 0; i--)
				{
					var child = _childrenCopy[i];
					child.ProcessInput(inputContext);
				}

				// Determine if input should be marked as handled
				if (IsModal)
				{
					// Modal widget blocks all input from reaching parent
					inputContext.MouseOrTouchHandled = true;
				}
				else
				{
					// Non-modal: mark as handled if input is over this widget and doesn't fall through
					if (!Desktop.IsMobile)
					{
						if (IsMouseInside && !InputFallsThrough(LocalMousePosition.Value))
						{
							inputContext.MouseOrTouchHandled = true;
						}
					}
					else
					{
						if (IsTouchInside && !InputFallsThrough(LocalTouchPosition.Value))
						{
							inputContext.MouseOrTouchHandled = true;
						}
					}
				}

				// Restore parent containment flags for sibling widgets
				inputContext.ParentContainsMouse = oldContainsMouse;
				inputContext.ParentContainsTouch = oldContainsTouch;
			}
			else
			{
				// Input already handled by another widget: clear local positions and continue recursion for children
				if (!Desktop.IsMobile)
				{
					LocalMousePosition = null;
				}

				LocalTouchPosition = null;

				// Still recursively process children in case they're also consuming input
				for (var i = _childrenCopy.Count - 1; i >= 0; i--)
				{
					var child = _childrenCopy[i];
					child.ProcessInput(inputContext);
				}
			}
		}

		// Routes input events to appropriate event handlers: calls virtual method and fires event
		void IInputEventsProcessor.ProcessEvent(InputEventType eventType)
		{
			switch (eventType)
			{
				case InputEventType.MouseLeft:
					// Hide tooltip if it belongs to this widget
					if (Desktop != null && Desktop.Tooltip != null && Desktop.Tooltip.Tag == this)
					{
						Desktop.HideTooltip();
					}

					_lastMouseMovement = null;

					// Update mouse cursor: check parent hierarchy for cursor to inherit from
					if (MyraEnvironment.SetMouseCursorFromWidget && MouseCursor != null)
					{
						Widget ancestor = Parent;
						while (ancestor != null && !ancestor.IsMouseInside)
						{
							ancestor = ancestor.Parent;
						}

						if (ancestor != null && ancestor.MouseCursor != null)
						{
							MyraEnvironment.MouseCursorType = ancestor.MouseCursor.Value;
						}
						else
						{
							MyraEnvironment.MouseCursorType = MyraEnvironment.DefaultMouseCursorType;
						}
					}

					OnMouseLeft();
					MouseLeft.Invoke(this, InputEventType.MouseLeft);
					break;

				case InputEventType.MouseEntered:
					_lastMouseMovement = DateTime.Now;  // For tooltip delay calculation
					if (MyraEnvironment.SetMouseCursorFromWidget && MouseCursor != null)
					{
						MyraEnvironment.MouseCursorType = MouseCursor.Value;
					}

					OnMouseEntered();
					MouseEntered.Invoke(this, InputEventType.MouseEntered);
					break;

				case InputEventType.MouseMoved:
					_lastMouseMovement = DateTime.Now;  // For tooltip delay calculation
					OnMouseMoved();
					MouseMoved.Invoke(this, InputEventType.MouseMoved);
					break;

				case InputEventType.MouseWheel:
					if (Desktop != null)
					{
						OnMouseWheel(Desktop.MouseWheelDelta);

						// Check again: OnMouseWheel might detach widget from Desktop
						if (Desktop != null)
						{
							MouseWheelChanged.Invoke(this, Desktop.MouseWheelDelta, InputEventType.MouseWheel);
						}
					}
					break;

				case InputEventType.TouchLeft:
					OnTouchLeft();
					TouchLeft.Invoke(this, InputEventType.TouchLeft);
					break;

				case InputEventType.TouchEntered:
					OnTouchEntered();
					TouchEntered.Invoke(this, InputEventType.TouchEntered);
					break;

				case InputEventType.TouchMoved:
					OnTouchMoved();
					TouchMoved.Invoke(this, InputEventType.TouchMoved);
					break;

				case InputEventType.TouchDown:
					if (Desktop != null)
					{
						// Auto-focus on touch if widget accepts keyboard focus
						if (Enabled && AcceptsKeyboardFocus)
						{
							Desktop.FocusedKeyboardWidget = this;
						}

						// Initialize drag tracking if widget has a drag handle
						if (DragHandle != null && DragHandle.IsTouchInside)
						{
							var parent = Parent != null ? (ITransformable)Parent : Desktop;
							_startPos = parent.ToLocal(new Vector2(Desktop.TouchPosition.Value.X, Desktop.TouchPosition.Value.Y));
							_startLeftTop = new Point(Left, Top);
						}
					}

					OnTouchDown();
					TouchDown.Invoke(this, InputEventType.TouchDown);
					break;

				case InputEventType.TouchUp:
					OnTouchUp();
					TouchUp.Invoke(this, InputEventType.TouchUp);
					break;

				case InputEventType.TouchDoubleClick:
					OnTouchDoubleClick();
					TouchDoubleClick.Invoke(this, InputEventType.TouchDoubleClick);
					break;
			}
		}

		/// <summary>
		/// Called when the mouse pointer leaves the widget.
		/// </summary>
		public virtual void OnMouseLeft()
		{
		}

		/// <summary>
		/// Called when the mouse pointer enters the widget.
		/// </summary>
		public virtual void OnMouseEntered()
		{
		}

		/// <summary>
		/// Called when the mouse pointer moves within the widget.
		/// </summary>
		public virtual void OnMouseMoved()
		{
		}

		/// <summary>
		/// Called when the mouse wheel is scrolled while over the widget.
		/// </summary>
		/// <param name="delta">The scroll delta value.</param>
		public virtual void OnMouseWheel(float delta)
		{
		}

		/// <summary>
		/// Called when a touch point leaves the widget.
		/// </summary>
		public virtual void OnTouchLeft()
		{
		}

		/// <summary>
		/// Called when a touch point enters the widget.
		/// </summary>
		public virtual void OnTouchEntered()
		{
		}

		/// <summary>
		/// Called when a touch point moves within the widget.
		/// </summary>
		public virtual void OnTouchMoved()
		{
		}

		/// <summary>
		/// Called when a touch point is pressed on the widget.
		/// </summary>
		public virtual void OnTouchDown()
		{
		}

		/// <summary>
		/// Called when a touch point is released on the widget.
		/// </summary>
		public virtual void OnTouchUp()
		{
		}

		/// <summary>
		/// Called when the widget receives a double-tap touch event.
		/// </summary>
		public virtual void OnTouchDoubleClick()
		{
		}
	}
}
