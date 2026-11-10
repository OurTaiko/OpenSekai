using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sekai.UI
{
	/// <summary>
	/// Gives a plain uGUI <see cref="Button"/> or <see cref="Toggle"/> the click sound that <see cref="CustomButton"/> and
	/// <see cref="CustomToggle"/> play: SE_DECIDE1, SE_CANCEL for close and back buttons, or a named cue.
	/// Whether the control could be clicked is read on pointer down, so a click handler that disables its own button
	/// (a stepper reaching its limit) still sounds.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class UIClickSound : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
	{
		public const string DialogOpenCue = "SE_UI_DIALOG_OPEN";

		[SerializeField]
		private SeType se = SeType.Decide;

		[SerializeField]
		private string otherSeName;

		private Selectable selectable;
		private bool armed;

		public SeType Se
		{
			get => se;
			set => se = value;
		}

		/// <summary>Adds (or retunes) the click sound of a control built in code.</summary>
		public static UIClickSound Add(Component target, SeType se = SeType.Decide, string otherSeName = null)
		{
			if (target == null)
			{
				return null;
			}

			UIClickSound sound = target.GetComponent<UIClickSound>();
			if (sound == null)
			{
				sound = target.gameObject.AddComponent<UIClickSound>();
			}
			sound.se = se;
			sound.otherSeName = otherSeName ?? string.Empty;
			return sound;
		}

		/// <summary>The sound of a panel or popup opening, as <see cref="DialogBase"/> plays it.</summary>
		public static void PlayOpen()
		{
			SoundManager.Instance?.PlaySEOneShot(DialogOpenCue, 0);
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			armed = eventData.button == PointerEventData.InputButton.Left && CanSound();
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (!armed || eventData.button != PointerEventData.InputButton.Left)
			{
				return;
			}

			armed = false;
			CustomSelectableDefine.PlaySE(se, otherSeName);
		}

		private bool CanSound()
		{
			if (selectable == null)
			{
				selectable = GetComponent<Selectable>();
			}
			if (selectable == null)
			{
				return true;
			}
			if (!selectable.IsActive() || !selectable.IsInteractable())
			{
				return false;
			}

			// A grouped toggle that is already on does not change, as CustomToggle stays quiet there.
			return !(selectable is Toggle toggle && toggle.isOn && toggle.group != null && !toggle.group.allowSwitchOff);
		}
	}
}
