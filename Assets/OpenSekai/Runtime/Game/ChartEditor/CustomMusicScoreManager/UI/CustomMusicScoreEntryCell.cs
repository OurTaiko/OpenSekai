using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>One row of the local chart list. The template lives in the manager prefab and is cloned per chart.</summary>
	public sealed class CustomMusicScoreEntryCell : MonoBehaviour
	{
		[SerializeField]
		private Image background;

		[SerializeField]
		private Button button;

		[SerializeField]
		private TextMeshProUGUI titleText;

		[SerializeField]
		private TextMeshProUGUI metaText;

		[SerializeField]
		private TextMeshProUGUI statusText;

		public Image Background => background;

		public Button Button => button;

		public TextMeshProUGUI TitleText => titleText;

		public TextMeshProUGUI MetaText => metaText;

		public TextMeshProUGUI StatusText => statusText;

		internal void SetReferences(Image background, Button button, TextMeshProUGUI titleText, TextMeshProUGUI metaText, TextMeshProUGUI statusText)
		{
			this.background = background;
			this.button = button;
			this.titleText = titleText;
			this.metaText = metaText;
			this.statusText = statusText;
		}
	}
}
