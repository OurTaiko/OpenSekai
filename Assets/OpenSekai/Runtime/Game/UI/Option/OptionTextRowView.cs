using TMPro;
using UnityEngine;

namespace Sekai
{
	/// <summary>A <see cref="OptionRowKind.Text"/> row: a centred line of text, pink when it is a hint.</summary>
	public sealed class OptionTextRowView : OptionRowView
	{
		private static readonly Color TextColor = new Color32(0x4A, 0x48, 0x64, 0xFF);
		private static readonly Color HintColor = new Color32(0xF0, 0x50, 0x8C, 0xFF);

		[SerializeField]
		private TMP_Text text;

		protected override void OnBind(OptionRowStyle style)
		{
			text.text = Row.Label;
			text.color = Row.IsHint ? HintColor : TextColor;
			int lines = Row.Label.Split('\n').Length;
			if (layout != null)
			{
				layout.preferredHeight = layout.minHeight = lines > 1 ? 78f : 34f;
			}
		}

		public override void Refresh(OptionSettings settings)
		{
		}
	}
}
