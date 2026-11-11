using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Sekai
{
	/// <summary>
	/// Looks up OpenSekai's text in the Unity Localization string tables
	/// (<c>Assets/OpenSekai/Localization/Strings/&lt;table&gt;.csv</c>, imported into the tables beside them). Every table is
	/// preloaded, so lookups are synchronous; the first one waits for the package to finish initializing.
	/// A key that is missing from its table comes back as the key itself, with a warning.
	/// </summary>
	public static class L10n
	{
		public const string ChineseSimplified = "zh-Hans";
		public const string Japanese = "ja";
		public const string English = "en";

		/// <summary>The languages the game offers, in the order the option dialog lists them.</summary>
		public static readonly string[] LanguageCodes = { ChineseSimplified, Japanese, English };

		/// <summary>Each language's own name, the same in every language.</summary>
		public static readonly string[] LanguageNames = { "简体中文", "日本語", "English" };

		/// <summary>Raised after the language changed and its tables are loaded.</summary>
		public static event Action LanguageChanged;

		private static bool ready;
		private static readonly HashSet<string> warnedMissing = new HashSet<string>();

		/// <summary>The selected language's code (<see cref="LanguageCodes"/>).</summary>
		public static string Language
		{
			get
			{
				EnsureReady();
				Locale locale = LocalizationSettings.SelectedLocale;
				return locale != null ? locale.Identifier.Code : English;
			}
		}

		public static void EnsureReady()
		{
			if (ready)
			{
				return;
			}

			ready = true;
			var initialization = LocalizationSettings.InitializationOperation;
			if (!initialization.IsDone)
			{
				initialization.WaitForCompletion();
			}
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
		}

		/// <summary>The text of <paramref name="key"/> in <paramref name="table"/> for the selected language.</summary>
		public static string Get(string table, string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return string.Empty;
			}

			EnsureReady();
			// Follows the locale's fallback (en -> ja) for entries a table leaves out (the Wording table's English).
			StringTableEntry entry = LocalizationSettings.StringDatabase.GetTableEntry(table, key).Entry;
			if (entry == null || entry.Value == null)
			{
				if (warnedMissing.Add(table + "/" + key))
				{
					Debug.LogWarningFormat("[L10n] Missing string {0}/{1} for {2}", table, key, Language);
				}
				return key;
			}
			return entry.Value;
		}

		/// <summary><see cref="Get"/> with <see cref="string.Format(string, object[])"/> placeholders ({0}, {1}, ...).</summary>
		public static string Format(string table, string key, params object[] args)
		{
			string format = Get(table, key);
			if (args == null || args.Length == 0)
			{
				return format;
			}

			try
			{
				return string.Format(format, args);
			}
			catch (FormatException exception)
			{
				Debug.LogWarningFormat("[L10n] Bad format in {0}/{1} for {2}: {3}", table, key, Language, exception.Message);
				return format;
			}
		}

		/// <summary>True when the table has <paramref name="key"/> (for keys built at run time).</summary>
		public static bool Has(string table, string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return false;
			}

			EnsureReady();
			return LocalizationSettings.StringDatabase.GetTableEntry(table, key).Entry != null;
		}

		/// <summary>Switches the game to <paramref name="code"/> (one of <see cref="LanguageCodes"/>).</summary>
		public static void SetLanguage(string code)
		{
			EnsureReady();
			Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
			if (locale == null || locale == LocalizationSettings.SelectedLocale)
			{
				return;
			}

			LocalizationSettings.SelectedLocale = locale;
		}

		/// <summary>The language a player who has not picked one gets: the system's, or English.</summary>
		public static string SystemLanguage()
		{
			switch (Application.systemLanguage)
			{
				case UnityEngine.SystemLanguage.Chinese:
				case UnityEngine.SystemLanguage.ChineseSimplified:
				case UnityEngine.SystemLanguage.ChineseTraditional:
					return ChineseSimplified;
				case UnityEngine.SystemLanguage.Japanese:
					return Japanese;
				default:
					return English;
			}
		}

		/// <summary><paramref name="code"/> if the game offers it, otherwise null.</summary>
		public static string Supported(string code)
		{
			return Array.IndexOf(LanguageCodes, code) >= 0 ? code : null;
		}

		private static void OnSelectedLocaleChanged(Locale locale)
		{
			// Changing the locale preloads the new locale's tables; wait for them so that listeners can read
			// strings synchronously.
			var preload = LocalizationSettings.StringDatabase.PreloadOperation;
			if (!preload.IsDone)
			{
				preload.WaitForCompletion();
			}
			LanguageChanged?.Invoke();
		}
	}
}
