using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>The <see cref="OptionRowKind.NotePreview"/>: the dark lane preview and the プレビューで詳細設定 button under it.</summary>
	public sealed class OptionNotePreviewView : OptionRowView
	{
		[SerializeField]
		private Button button;

		[SerializeField]
		private TMP_Text buttonText;

		protected override void OnBind(OptionRowStyle style)
		{
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
