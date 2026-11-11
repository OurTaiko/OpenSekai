using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	/// <summary>
	/// The settings the option dialog edits: <see cref="LiveSettingData"/> and <see cref="ApplicationLocalSettings"/>.
	/// <see cref="OptionCatalog"/> rows read and write through it; <see cref="Commit"/> saves both stores and applies what
	/// takes effect outside a live.
	/// </summary>
	public sealed class OptionSettings
	{
		public LiveSettingData Live { get; }
		public ApplicationLocalSettings Local { get; }

		public OptionSettings(LiveSettingData live, ApplicationLocalSettings local)
		{
			Live = live;
			Local = local;
		}

		public static OptionSettings Load()
		{
			return new OptionSettings(LiveSettingData.LoadFromStorage(), ApplicationLocalSettings.LoadFromStorage());
		}

		public ApplicationLocalSettings.VolumeSettings LiveVolume => Local.LiveVolume ?? Local.SetupLiveVolume();

		public ApplicationLocalSettings.VolumeSettings SystemVolume => Local.SystemVolume;

		public ApplicationLocalSettings.SystemOptionSettings SystemOptions => Local.SystemOptions;

		public static bool SupportsFullscreen
		{
			get
			{
#if UNITY_STANDALONE || UNITY_EDITOR
				return true;
#else
				return false;
#endif
			}
		}

		/// <summary>Saves both stores and applies the settings that matter in the menus.</summary>
		public void Commit()
		{
			LiveSettingData.SaveToStorage(Live);
			ApplicationLocalSettings.SaveToStorage(Local);

			ApplicationLocalSettings.VolumeSettings system = SystemVolume;
			SoundManager.Instance.SetupVolume(1f, system.Bgm, system.Se, system.Voice);
			LiveConfig.LongNoteAlpha = Live.GetNoteAlpha();
			LiveConfig.GuideAlpha = Live.GetGuideAlpha();
			LiveConfig.SetNoteSkinAssetBundleName(Live.NoteSkinIndex);
			LiveConfig.SetNoteSeName(Live.NoteSeIndex);
			LiveConfig.SetNoteEffectName(Live.NoteEffect);
			LiveConfig.SetFeverEffectName(Live.FeverEffectTypeIndex);
			if (SupportsFullscreen && Local.FullscreenEnabled.HasValue && Screen.fullScreen != Local.FullscreenEnabled.Value)
			{
				ScreenConfig.SetStandaloneFullscreen(Local.FullscreenEnabled.Value);
			}
			string language = L10n.Supported(Local.Language);
			if (language != null && language != L10n.Language)
			{
				L10n.SetLanguage(language);
			}
		}
	}
}
