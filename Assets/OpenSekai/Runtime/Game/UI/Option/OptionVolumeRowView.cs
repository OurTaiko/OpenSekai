using UnityEngine;

namespace Sekai
{
	/// <summary>A <see cref="OptionRowKind.Volume"/> row of the 音量 tab: label, − , the slider with its value, +.</summary>
	public sealed class OptionVolumeRowView : OptionRowView
	{
		[SerializeField]
		private OptionLabelView label;

		[SerializeField]
		private OptionVolumeSlider slider;

		protected override void OnBind(OptionRowStyle style)
		{
			label.Setup(Row.Label, 135f, 46f);
			slider.OnValueChanged -= OnValueChanged;
			slider.OnValueChanged += OnValueChanged;
		}

		public override void Refresh(OptionSettings settings)
		{
			slider.SetValueWithoutNotify(Mathf.RoundToInt(Row.GetNumber(settings)));
		}

		private void OnValueChanged(int value)
		{
			Row.SetNumber(Host.Settings, value);
			Changed();
		}
	}
}
