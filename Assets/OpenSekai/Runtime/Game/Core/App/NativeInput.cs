using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.Utilities;

namespace Sekai
{
	/// <summary>
	/// Input System front for the game. Every read of keyboard, mouse and touch goes through here,
	/// so the project runs with the Input System only (no legacy Input Manager).
	/// Touches come from EnhancedTouch; mouse button 0 also follows the primary touch, as the
	/// legacy Input.simulateMouseWithTouches did, so mouse-style call sites keep working on phones.
	/// </summary>
	public static class NativeInput
	{
		public static ReadOnlyArray<UnityEngine.InputSystem.EnhancedTouch.Touch> ActiveTouches
		{
			get
			{
				EnsureEnhancedTouchEnabled();
				return UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
			}
		}

		public static ReadOnlyArray<Finger> Fingers
		{
			get
			{
				EnsureEnhancedTouchEnabled();
				return UnityEngine.InputSystem.EnhancedTouch.Touch.fingers;
			}
		}

		public static int TouchCount => ActiveTouches.Count;

		public static bool TouchSupported => Touchscreen.current != null;

		/// <summary>Screen position of the last used pointer (mouse, pen or primary touch).</summary>
		public static Vector2 PointerPosition
		{
			get
			{
				Pointer pointer = Pointer.current;
				return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
			}
		}

		/// <summary>Escape on keyboards; Android's back button arrives as Escape too.</summary>
		public static bool BackKeyDown => GetKeyDown(Key.Escape);

		public static bool GetKeyDown(Key key)
		{
			Keyboard keyboard = Keyboard.current;
			return keyboard != null && keyboard[key].wasPressedThisFrame;
		}

		public static bool GetMouseButtonDown(int button)
		{
			ButtonControl control = MouseButton(button);
			return (control != null && control.wasPressedThisFrame)
				|| (button == 0 && PrimaryTouchPress() is { } press && press.wasPressedThisFrame);
		}

		public static bool GetMouseButton(int button)
		{
			ButtonControl control = MouseButton(button);
			return (control != null && control.isPressed)
				|| (button == 0 && PrimaryTouchPress() is { } press && press.isPressed);
		}

		public static bool GetMouseButtonUp(int button)
		{
			ButtonControl control = MouseButton(button);
			return (control != null && control.wasReleasedThisFrame)
				|| (button == 0 && PrimaryTouchPress() is { } press && press.wasReleasedThisFrame);
		}

		/// <summary>Turns on EnhancedTouch so touches are tracked from the first frame.</summary>
		public static void Initialize()
		{
			EnsureEnhancedTouchEnabled();
		}

		public static void Enable()
		{
			EnsureEnhancedTouchEnabled();
			foreach (InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
			{
				UnityEngine.InputSystem.InputSystem.ResetDevice(device, false);
			}
		}

		public static void Disable()
		{
			if (EnhancedTouchSupport.enabled)
			{
				EnhancedTouchSupport.Disable();
			}
		}

		private static ButtonControl MouseButton(int button)
		{
			Mouse mouse = Mouse.current;
			if (mouse == null)
			{
				return null;
			}

			return button switch
			{
				0 => mouse.leftButton,
				1 => mouse.rightButton,
				2 => mouse.middleButton,
				_ => null,
			};
		}

		private static ButtonControl PrimaryTouchPress()
		{
			Touchscreen touchscreen = Touchscreen.current;
			return touchscreen != null ? touchscreen.primaryTouch.press : null;
		}

		private static void EnsureEnhancedTouchEnabled()
		{
			if (!EnhancedTouchSupport.enabled)
			{
				EnhancedTouchSupport.Enable();
			}
		}
	}
}
