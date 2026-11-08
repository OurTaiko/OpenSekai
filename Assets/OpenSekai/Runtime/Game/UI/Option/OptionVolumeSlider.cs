using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// The official volume row on the 音量 tab: "[label] (-) ───●─── (+)" with the value (0 to 100) above the slider.
	/// </summary>
	public sealed class OptionVolumeSlider : MonoBehaviour
	{
		private const int MaxValue = 100;

		[SerializeField]
		private Slider slider;

		[SerializeField]
		private Button minusButton;

		[SerializeField]
		private Button plusButton;

		[SerializeField]
		private TMP_Text valueText;

		private int value;
		private bool bound;

		public event Action<int> OnValueChanged;

		public int Value => value;

		private void Awake()
		{
			Bind();
		}

		public void SetValueWithoutNotify(int newValue)
		{
			Bind();
			value = Mathf.Clamp(newValue, 0, MaxValue);
			Refresh();
		}

		private void Bind()
		{
			if (bound)
			{
				return;
			}

			bound = true;
			if (slider != null)
			{
				slider.minValue = 0f;
				slider.maxValue = MaxValue;
				slider.wholeNumbers = true;
				slider.onValueChanged.AddListener(v => Change(Mathf.RoundToInt(v)));
			}
			minusButton?.onClick.AddListener(() => Change(value - 1));
			plusButton?.onClick.AddListener(() => Change(value + 1));
		}

		private void Change(int newValue)
		{
			newValue = Mathf.Clamp(newValue, 0, MaxValue);
			if (newValue == value)
			{
				return;
			}

			value = newValue;
			Refresh();
			OnValueChanged?.Invoke(value);
		}

		private void Refresh()
		{
			slider?.SetValueWithoutNotify(value);
			if (valueText != null)
			{
				valueText.text = value.ToString(CultureInfo.InvariantCulture);
			}
			if (minusButton != null)
			{
				minusButton.interactable = value > 0;
			}
			if (plusButton != null)
			{
				plusButton.interactable = value < MaxValue;
			}
		}
	}
}
