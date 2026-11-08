using System.Linq;
using UnityEngine;

namespace Sekai
{
	/// <summary>A <see cref="OptionRowKind.Selector"/> row: the label, then "&lt; value &gt;" (ノーツのSE).</summary>
	public sealed class OptionSelectorRowView : OptionRowView
	{
		[SerializeField]
		private OptionLabelView label;

		[SerializeField]
		private OptionSelector selector;

		protected override void OnBind(OptionRowStyle style)
		{
			label.Setup(Row.Label, style.Width);
			selector.SetLabels(Row.Options.ToArray());
			selector.OnValueChanged -= OnValueChanged;
			selector.OnValueChanged += OnValueChanged;
		}

		public override void Refresh(OptionSettings settings)
		{
			selector.SetValueWithoutNotify(Row.GetIndex(settings));
		}

		private void OnValueChanged(int index)
		{
			Row.SetIndex(Host.Settings, index);
			Changed();
		}
	}
}
