using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>A <see cref="OptionRowKind.Button"/> row: the label, then one centred capsule button (容量管理).</summary>
	public sealed class OptionButtonRowView : OptionRowView
	{
		[SerializeField]
		private OptionLabelView label;

		[SerializeField]
		private Button button;

		[SerializeField]
		private TMP_Text buttonText;

		protected override void OnBind(OptionRowStyle style)
		{
			label.Setup(Row.Label, style.Width);
			buttonText.text = Row.Options.Count > 0 ? Row.Options[0] : string.Empty;
			button.onClick.RemoveAllListeners();
			string action = Row.Actions.Count > 0 ? Row.Actions[0] : OptionCatalog.ActionNone;
			button.onClick.AddListener(() => Host.RunAction(action));
		}

		public override void Refresh(OptionSettings settings)
		{
		}
	}
}
