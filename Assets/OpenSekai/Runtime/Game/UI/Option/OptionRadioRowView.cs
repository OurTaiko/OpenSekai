using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// A <see cref="OptionRowKind.Radio"/> row: the label, then one option per catalog option made from the template
	/// (a circle, its teal mark and the text), each in a slot of the row's <see cref="OptionRow.SlotWidth"/>.
	/// </summary>
	public sealed class OptionRadioRowView : OptionRowView
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
		private Button optionTemplate;

		[SerializeField]
		private OptionRadioGroup group;

		private readonly List<Button> options = new List<Button>();

		protected override void OnBind(OptionRowStyle style)
		{
			bool showLabel = style.ShowLabel && !string.IsNullOrEmpty(Row.Label);
			label.gameObject.SetActive(showLabel);
			labelGap.gameObject.SetActive(showLabel);
			label.Setup(Row.Label, style.Width);
			labelGap.preferredHeight = labelGap.minHeight = style.LabelGap;
			controlLayout.preferredWidth = controlLayout.minWidth = style.Width;
			controlLayout.preferredHeight = controlLayout.minHeight = style.Height;

			foreach (Button option in options)
			{
				Destroy(option.gameObject);
			}
			options.Clear();
			var marks = new List<GameObject>();
			foreach (string text in Row.Options)
			{
				Button option = Instantiate(optionTemplate, control.transform, false);
				option.gameObject.SetActive(true);
				option.name = "Option_" + text;
				var slot = option.GetComponent<LayoutElement>();
				if (Row.SlotWidth > 0f)
				{
					slot.preferredWidth = slot.minWidth = Row.SlotWidth;
				}
				else
				{
					slot.preferredWidth = slot.minWidth = -1f;
					option.GetComponent<HorizontalLayoutGroup>().spacing = 4f;
				}
				option.GetComponentInChildren<TMP_Text>(true).text = text;
				marks.Add(option.transform.Find("Circle/Mark").gameObject);
				options.Add(option);
			}

			group.Setup(options.ToArray(), marks.ToArray());
			group.OnValueChanged -= OnValueChanged;
			group.OnValueChanged += OnValueChanged;
		}

		public override void Refresh(OptionSettings settings)
		{
			group.SetValueWithoutNotify(Row.GetIndex(settings));
		}

		private void OnValueChanged(int index)
		{
			Row.SetIndex(Host.Settings, index);
			Changed();
		}
	}
}
