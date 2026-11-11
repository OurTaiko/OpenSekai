using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	/// <summary>
	/// The original game's wording lookup (keys such as WORD_OK, MSG_PAUSE_LIVE), read from the <c>Wording</c> string table
	/// (Localization/Strings/Wording.csv: the original's Japanese and Chinese wording, with English for the keys OpenSekai
	/// shows; the rest fall back to Japanese). A key the table does not have reads as an empty string, as in the original.
	/// </summary>
	public static class WordingManager
	{
		public const string Table = "Wording";

		private static bool dispKeyFlag;

		static WordingManager()
		{
			L10n.LanguageChanged += UpdateTexts;
		}

		public static bool DispKeyFlag => dispKeyFlag;

		public static string Get(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return string.Empty;
			}

			if (dispKeyFlag)
			{
				return key;
			}

			return L10n.Has(Table, key) ? L10n.Get(Table, key) : string.Empty;
		}

		public static string GetFormat(string key, params object[] args)
		{
			var format = Get(key);
			return args != null && args.Length > 0 ? string.Format(format, args) : format;
		}

		/// <summary>Shows the keys instead of the text (a debugging aid of the original).</summary>
		public static void SetDispKey(bool flag)
		{
			if (dispKeyFlag == flag)
			{
				return;
			}

			dispKeyFlag = flag;
			UpdateTexts();
		}

		private static void UpdateTexts()
		{
			foreach (var customText in Object.FindObjectsByType<CustomText>(FindObjectsSortMode.None))
			{
				customText.UpdateWordingText();
			}
			foreach (var customText in Object.FindObjectsByType<CustomTextMesh>(FindObjectsSortMode.None))
			{
				customText.UpdateWordingText();
			}
		}
	}
}
