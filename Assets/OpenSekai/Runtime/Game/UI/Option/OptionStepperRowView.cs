using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// A <see cref="OptionRowKind.Stepper"/> row: the label, then delta buttons around the value box. Buttons are made from
	/// the templates for the row's deltas ("-1 -0.1 [value] +0.1 +1"); percentages use the bar glyph buttons ("− [50%] +").
	/// </summary>
	public sealed class OptionStepperRowView : OptionRowView
	{
		[SerializeField]
		private OptionLabelView label;

		[SerializeField]
		private LayoutElement labelGap;

		[SerializeField]
		private HorizontalLayoutGroup control;

		[SerializeField]
		private LayoutElement controlLayout;

		[SerializeField]
		private LayoutElement valueBox;

		[SerializeField]
		private TMP_Text valueText;

		[SerializeField]
		private Button textButtonTemplate;

		[SerializeField]
		private Button glyphButtonTemplate;

		[SerializeField]
		private OptionStepper stepper;

		private readonly List<Button> buttons = new List<Button>();

		protected override void OnBind(OptionRowStyle style)
		{
			bool showLabel = style.ShowLabel && !string.IsNullOrEmpty(Row.Label);
			label.gameObject.SetActive(showLabel);
			labelGap.gameObject.SetActive(showLabel);
			label.Setup(Row.Label, style.Width);
			labelGap.preferredHeight = labelGap.minHeight = style.LabelGap;

			control.spacing = style.Spacing;
			control.childAlignment = style.Alignment;
			controlLayout.preferredHeight = controlLayout.minHeight = style.Height;
			controlLayout.preferredWidth = controlLayout.minWidth = style.Width;
			valueBox.preferredWidth = valueBox.minWidth = style.ValueWidth;
			valueBox.preferredHeight = valueBox.minHeight = style.Height;

			bool glyph = Row.Suffix == "%";
			int half = Row.Deltas.Count / 2;
			foreach (Button button in buttons)
			{
				Destroy(button.gameObject);
			}
			buttons.Clear();
			for (int i = 0; i < Row.Deltas.Count; i++)
			{
				float delta = Row.Deltas[i];
				Button button = Instantiate(glyph ? glyphButtonTemplate : textButtonTemplate, control.transform, false);
				button.gameObject.SetActive(true);
				button.name = "Step" + i;
				var layoutElement = button.GetComponent<LayoutElement>();
				layoutElement.preferredWidth = layoutElement.minWidth = style.ButtonWidth;
				layoutElement.preferredHeight = layoutElement.minHeight = style.Height;
				if (glyph)
				{
					button.transform.Find("Vertical")?.gameObject.SetActive(delta > 0f);
				}
				else
				{
					TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
					text.text = Label(delta);
					text.fontSize = text.text.Length > 4 ? 28f : 38f;
				}
				buttons.Add(button);
			}
			valueBox.transform.SetSiblingIndex(half);

			stepper.Setup(buttons.ToArray(), ToArray(Row.Deltas), valueText, Row.Minimum, Row.Maximum, Row.Format, Row.Suffix);
			stepper.OnValueChanged -= OnValueChanged;
			stepper.OnValueChanged += OnValueChanged;
		}

		public override void Refresh(OptionSettings settings)
		{
			stepper.SetValueWithoutNotify(Row.GetNumber(settings));
		}

		private void OnValueChanged(float value)
		{
			Row.SetNumber(Host.Settings, value);
			Changed();
		}

		private static string Label(float delta)
		{
			float size = Mathf.Abs(delta);
			string format = size < 0.1f ? "0.00" : size < 1f ? "0.0" : "0";
			return (delta > 0f ? "+" : string.Empty) + delta.ToString(format, CultureInfo.InvariantCulture);
		}

		private static float[] ToArray(IReadOnlyList<float> values)
		{
			var array = new float[values.Count];
			for (int i = 0; i < values.Count; i++)
			{
				array[i] = values[i];
			}
			return array;
		}
	}
}
