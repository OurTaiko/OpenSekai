using UnityEngine;

namespace Sekai
{
	/// <summary>
	/// The <see cref="OptionRowKind.Skin"/> row: the ノーツスキン label overlapping the top of the carousel panel, one preview
	/// page per skin (authored in the prefab, in skin order).
	/// </summary>
	public sealed class OptionSkinRowView : OptionRowView
	{
		[SerializeField]
		private OptionLabelView label;

		[SerializeField]
		private OptionSelector carousel;

		protected override void OnBind(OptionRowStyle style)
		{
			label.Setup(Row.Label, 593f);
			carousel.OnValueChanged -= OnValueChanged;
			carousel.OnValueChanged += OnValueChanged;
		}

		public override void Refresh(OptionSettings settings)
		{
			carousel.SetValueWithoutNotify(Row.GetIndex(settings));
		}

		private void OnValueChanged(int index)
		{
			Row.SetIndex(Host.Settings, index);
			Changed();
		}
	}
}
