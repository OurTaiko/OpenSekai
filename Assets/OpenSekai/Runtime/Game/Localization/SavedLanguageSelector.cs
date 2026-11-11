using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Sekai
{
	/// <summary>
	/// The first startup locale selector in the Localization Settings: the language picked in the option dialog
	/// (<see cref="ApplicationLocalSettings.Language"/>), or else the system's (Chinese of either script is 简体中文, any
	/// language the game does not have is English).
	/// </summary>
	[Serializable]
	public sealed class SavedLanguageSelector : IStartupLocaleSelector
	{
		public Locale GetStartupLocale(ILocalesProvider availableLocales)
		{
			string code = L10n.Supported(ApplicationLocalSettings.LoadFromStorage().Language) ?? L10n.SystemLanguage();
			return availableLocales.GetLocale(code);
		}
	}
}
