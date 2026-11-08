using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// The official option stepper: delta buttons around a value box
	/// (ノーツの速さ "-1 -0.1 [10.75] +0.1 +1", 不透明度 "- [50%] +", ...).
	/// A button that cannot move the value any further is made non-interactable, as in the original.
	/// </summary>
	public sealed class OptionStepper : MonoBehaviour
	{
		private const float Epsilon = 0.0001f;

		[SerializeField]
		private Button[] buttons;

		[SerializeField]
		private float[] deltas;

		[SerializeField]
		private TMP_Text valueText;

		[SerializeField]
		private float min;

		[SerializeField]
		private float max = 100f;

		[SerializeField]
		private string format = "0.00";

		[SerializeField]
		private string suffix;

		private float value;
		private bool bound;

		public event Action<float> OnValueChanged;

		public float Value => value;

		private void Awake()
		{
			Bind();
		}

		/// <summary>Wires buttons a row built at runtime from its catalog entry.</summary>
		public void Setup(Button[] stepButtons, float[] stepDeltas, TMP_Text text, float minimum, float maximum, string valueFormat, string valueSuffix)
		{
			buttons = stepButtons;
			deltas = stepDeltas;
			valueText = text;
			min = minimum;
			max = maximum;
			format = valueFormat;
			suffix = valueSuffix ?? string.Empty;
			bound = false;
			Bind();
		}

		public void SetValueWithoutNotify(float newValue)
		{
			Bind();
			value = Clamp(newValue);
			Refresh();
		}

		private void Bind()
		{
			if (bound || buttons == null)
			{
				return;
			}

			bound = true;
			for (int i = 0; i < buttons.Length; i++)
			{
				if (buttons[i] == null || deltas == null || i >= deltas.Length)
				{
					continue;
				}

				float delta = deltas[i];
				buttons[i].onClick.AddListener(() => Step(delta));
			}
		}

		private void Step(float delta)
		{
			float next = Clamp(value + delta);
			if (Mathf.Abs(next - value) < Epsilon)
			{
				return;
			}

			value = next;
			Refresh();
			OnValueChanged?.Invoke(value);
		}

		private float Clamp(float raw)
		{
			// Two decimals cover every step the dialogs use (0.01 is the finest).
			return Mathf.Clamp((float)Math.Round(raw, 2, MidpointRounding.AwayFromZero), min, max);
		}

		private void Refresh()
		{
			if (valueText != null)
			{
				valueText.text = value.ToString(format, CultureInfo.InvariantCulture) + suffix;
			}

			if (buttons == null || deltas == null)
			{
				return;
			}

			for (int i = 0; i < buttons.Length && i < deltas.Length; i++)
			{
				if (buttons[i] != null)
				{
					buttons[i].interactable = deltas[i] < 0f ? value > min + Epsilon : value < max - Epsilon;
				}
			}
		}
	}
}
