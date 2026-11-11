using TMPro;
using UnityEngine;

namespace Sekai
{
	/// <summary>
	/// Shows one string-table entry on the TextMeshPro text beside it, and follows language changes. For fixed labels in
	/// prefabs; text that code sets goes through <see cref="L10n"/> instead. The prefab keeps the Chinese text so it
	/// reads the same in the Editor.
	/// </summary>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(TMP_Text))]
	public sealed class LocalizedText : MonoBehaviour
	{
		[SerializeField]
		private string table;

		[SerializeField]
		private string key;

		private TMP_Text text;

		public string Table => table;
		public string Key => key;

		public void Set(string tableName, string entryKey)
		{
			table = tableName;
			key = entryKey;
			// In the Editor outside Play mode the prefab keeps its own text.
			if (Application.isPlaying && isActiveAndEnabled)
			{
				Apply();
			}
		}

		private void OnEnable()
		{
			Apply();
			L10n.LanguageChanged += Apply;
		}

		private void OnDisable()
		{
			L10n.LanguageChanged -= Apply;
		}

		private void Apply()
		{
			if (string.IsNullOrEmpty(table) || string.IsNullOrEmpty(key))
			{
				return;
			}

			text ??= GetComponent<TMP_Text>();
			text.text = L10n.Get(table, key);
		}
	}
}
