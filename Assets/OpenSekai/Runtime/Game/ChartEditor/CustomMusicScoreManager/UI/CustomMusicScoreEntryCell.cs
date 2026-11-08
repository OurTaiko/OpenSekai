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

		[SerializeField]
		private CustomMusicScoreDifficultyBadge difficultyBadge;

		[SerializeField]
		private Image jacketImage;

		[SerializeField]
		private GameObject selectedFrame;

		[SerializeField]
		private GameObject separator;

		public Image Background => background;

		public Button Button => button;

		public TextMeshProUGUI TitleText => titleText;

		public TextMeshProUGUI MetaText => metaText;

		public TextMeshProUGUI StatusText => statusText;

		public CustomMusicScoreDifficultyBadge DifficultyBadge => difficultyBadge;

		public Image JacketImage => jacketImage;

		public GameObject SelectedFrame => selectedFrame;

		public GameObject Separator => separator;

		internal void SetReferences(
			Image background,
			Button button,
			TextMeshProUGUI titleText,
			TextMeshProUGUI metaText,
			TextMeshProUGUI statusText,
			CustomMusicScoreDifficultyBadge difficultyBadge,
			Image jacketImage,
			GameObject selectedFrame,
			GameObject separator)
		{
			this.background = background;
			this.button = button;
			this.titleText = titleText;
			this.metaText = metaText;
			this.statusText = statusText;
			this.difficultyBadge = difficultyBadge;
			this.jacketImage = jacketImage;
			this.selectedFrame = selectedFrame;
			this.separator = separator;
		}
	}
}
