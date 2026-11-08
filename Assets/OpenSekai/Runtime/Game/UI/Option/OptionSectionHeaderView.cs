using TMPro;
using UnityEngine;

namespace Sekai
{
	/// <summary>A section title over a full-width rule (ノーツ設定, 見やすさ・演出設定, ...).</summary>
	public sealed class OptionSectionHeaderView : MonoBehaviour
	{
		[SerializeField]
		private TMP_Text title;

		public void Setup(string text)
		{
			name = "Section_" + text;
			if (title != null)
			{
				title.text = text;
			}
		}
	}
}
