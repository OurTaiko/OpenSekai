using System.Collections;
using Sekai.Live;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// The official オプション dialog (<see cref="DialogType.OptionDialog"/>), rebuilt for OpenSekai.
	/// Tabs: ライブ / 音量 / システム like the original, and Additionals for OpenSekai's own settings in place of
	/// the original コミュニケーション tab. Every change is saved and applied at once; the footer button only closes.
	/// Settings OpenSekai stores but does not use yet are listed in AGENTS.md.
	/// </summary>
	public sealed class OptionDialog : Common1ButtonDialog
	{
		private const float MinNoteAlphaPercent = 10f;

		// The original dialog is 1033 units tall (measured on a 4:3 screen) and leaves at least this much screen above
		// and below it on shorter screens.
		private const float FullDialogHeight = 1033f;
		private const float MinScreenMargin = 40f;
		private const float MinContentHeight = 300f;

		// Tab band (72) + gap under the tabs (64) + footer (144).
		private const float DialogChromeHeight = 280f;
		private const float SeTestLongDuration = 0.6f;
		private static readonly string[] SeTestCues =
		{
			LiveSoundDefine.SE_LIVE_PERFECT,
			LiveSoundDefine.SE_LIVE_FLICK,
			LiveSoundDefine.SE_LIVE_LONG,
			LiveSoundDefine.SE_LIVE_TRACE,
		};

		[Header("Layout")]
		[SerializeField]
		private LayoutElement contentRoot;

		[Header("Tabs")]
		[SerializeField]
		private UIPartsDialogTabGroup tabGroup;

		[SerializeField]
		private GameObject[] tabPages;

		[SerializeField]
		private ScrollRect[] tabScrolls;

		[Header("ライブ: ノーツ設定")]
		[SerializeField]
		private OptionStepper noteSpeed;

		[SerializeField]
		private OptionStepper timingAdjust;

		[SerializeField]
		private Button previewDetailButton;

		[SerializeField]
		private OptionSelector noteSkin;

		[SerializeField]
		private OptionStepper longNoteAlpha;

		[SerializeField]
		private OptionStepper guideAlpha;

		[SerializeField]
		private Graphic longNotePreview;

		[SerializeField]
		private Graphic guidePreview;

		[SerializeField]
		private OptionSelector noteSe;

		[SerializeField]
		private Button[] seTestButtons;

		[Header("ライブ: 見やすさ・演出設定")]
		[SerializeField]
		private OptionStepper brightness;

		[SerializeField]
		private OptionStepper laneAlpha;

		[SerializeField]
		private OptionRadioGroup tapEffect;

		[SerializeField]
		private OptionRadioGroup cutIn;

		[SerializeField]
		private OptionRadioGroup fever;

		[SerializeField]
		private OptionRadioGroup skillDisplay;

		[SerializeField]
		private OptionRadioGroup fps;

		[Header("ライブ: ライブ補助設定")]
		[SerializeField]
		private OptionRadioGroup simultaneousLine;

		[SerializeField]
		private OptionRadioGroup apEffect;

		[SerializeField]
		private OptionRadioGroup fastLateFlick;

		[SerializeField]
		private OptionRadioGroup mirror;

		[SerializeField]
		private OptionRadioGroup vibration;

		[Header("ライブ: MV設定")]
		[SerializeField]
		private OptionRadioGroup mvMode;

		[SerializeField]
		private OptionRadioGroup mvQuality;

		[Header("音量")]
		[SerializeField]
		private OptionVolumeSlider liveBgm;

		[SerializeField]
		private OptionVolumeSlider liveSe;

		[SerializeField]
		private OptionVolumeSlider liveVoice;

		[SerializeField]
		private OptionVolumeSlider systemBgm;

		[SerializeField]
		private OptionVolumeSlider systemSe;

		[SerializeField]
		private OptionVolumeSlider systemVoice;

		[Header("システム")]
		[SerializeField]
		private OptionRadioGroup defaultVocal;

		[SerializeField]
		private OptionRadioGroup screenLock;

		[SerializeField]
		private OptionRadioGroup confirmBeforeLive;

		[SerializeField]
		private OptionRadioGroup onlineStatus;

		[SerializeField]
		private OptionRadioGroup liveBonusNotify;

		[Header("Additionals")]
		[SerializeField]
		private OptionRadioGroup musicInfoDisplay;

		[SerializeField]
		private OptionRadioGroup liveBackground;

		[SerializeField]
		private OptionRadioGroup fullscreen;

		[SerializeField]
		private GameObject fullscreenRow;

		private LiveSettingData liveSetting;
		private ApplicationLocalSettings localSettings;
		private string loadedSeBundle;
		private uint longSePlaybackId;
		private Coroutine stopLongSeRoutine;

		public override void Initialize(DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true)
		{
			// The footer button is 閉じる: it only closes, because every change is already saved.
			Initialize(null, null, null, dialogSize, allowCloseExternal);
			FitContentHeight(this, contentRoot, DialogChromeHeight);
			SetupTabs();
			Load();
			BindEvents();
		}

		/// <summary>
		/// Sizes a dialog's scrolling content so the whole dialog is as tall as the original's
		/// (<see cref="FullDialogHeight"/>) when the screen allows, and keeps <see cref="MinScreenMargin"/> otherwise.
		/// </summary>
		public static void FitContentHeight(Component dialog, LayoutElement content, float chromeHeight)
		{
			Canvas canvas = content != null ? dialog.GetComponentInParent<Canvas>() : null;
			if (canvas == null)
			{
				return;
			}

			float screenHeight = ((RectTransform)canvas.rootCanvas.transform).rect.height;
			float dialogHeight = Mathf.Min(FullDialogHeight, screenHeight - MinScreenMargin * 2f);
			content.preferredHeight = Mathf.Max(MinContentHeight, dialogHeight - chromeHeight);
		}

		private void SetupTabs()
		{
			if (tabGroup == null)
			{
				return;
			}

			tabGroup.Setup();
			Sekai.UI.CustomIndexToggleGroup group = tabGroup.ToggleGroup;
			if (group != null)
			{
				group.OnSelectedIndexChanged += ShowTab;
				group.SelectedIndex = 0;
			}
			ShowTab(0);
		}

		private void ShowTab(int index)
		{
			if (tabPages != null)
			{
				for (int i = 0; i < tabPages.Length; i++)
				{
					tabPages[i]?.SetActive(i == index);
				}
			}
			if (tabScrolls != null && index >= 0 && index < tabScrolls.Length && tabScrolls[index] != null)
			{
				// Content is pivoted at its top, so a zero position shows the top of the page even before layout runs.
				ScrollRect scroll = tabScrolls[index];
				scroll.StopMovement();
				if (scroll.content != null)
				{
					scroll.content.anchoredPosition = Vector2.zero;
				}
			}
		}

		private void Load()
		{
			liveSetting = LiveSettingData.LoadFromStorage();
			localSettings = ApplicationLocalSettings.LoadFromStorage();
			ApplicationLocalSettings.VolumeSettings live = localSettings.LiveVolume ?? localSettings.SetupLiveVolume();
			ApplicationLocalSettings.VolumeSettings system = localSettings.SystemVolume;
			ApplicationLocalSettings.SystemOptionSettings systemOptions = localSettings.SystemOptions;

			noteSpeed?.SetValueWithoutNotify(liveSetting.NoteSpeed);
			timingAdjust?.SetValueWithoutNotify(liveSetting.TimingAdjustData);
			noteSkin?.SetValueWithoutNotify(liveSetting.NoteSkinIndex);
			longNoteAlpha?.SetValueWithoutNotify(liveSetting.GetNoteAlpha() * 100f);
			guideAlpha?.SetValueWithoutNotify(liveSetting.GetGuideAlpha() * 100f);
			noteSe?.SetValueWithoutNotify(liveSetting.NoteSeIndex);
			RefreshAlphaPreviews();

			brightness?.SetValueWithoutNotify(liveSetting.Brightness * 100f);
			laneAlpha?.SetValueWithoutNotify(liveSetting.LaneTransparent * 100f);
			tapEffect?.SetValueWithoutNotify(liveSetting.NoteEffect);
			cutIn?.SetOnWithoutNotify(liveSetting.UseCutIn);
			fever?.SetValueWithoutNotify(liveSetting.FeverEffectTypeIndex);
			skillDisplay?.SetOnWithoutNotify(!liveSetting.HiddenSkillAndPraise);
			fps?.SetValueWithoutNotify(liveSetting.Use120FPS ? 0 : 1);

			simultaneousLine?.SetOnWithoutNotify(liveSetting.UseSimultaneousPushingLine);
			apEffect?.SetOnWithoutNotify(liveSetting.UseAllPerfectEffect);
			fastLateFlick?.SetOnWithoutNotify(liveSetting.IsFastLateFlick);
			mirror?.SetOnWithoutNotify(liveSetting.IsMirror);
			vibration?.SetOnWithoutNotify(liveSetting.UseVibration);
			mvMode?.SetValueWithoutNotify(LiveModeToIndex(liveSetting.LiveMode));
			mvQuality?.SetValueWithoutNotify(liveSetting.QualityType == MVQualityType.High ? 0 : 1);

			liveBgm?.SetValueWithoutNotify(ToPercent(live.Bgm));
			liveSe?.SetValueWithoutNotify(ToPercent(live.Se));
			liveVoice?.SetValueWithoutNotify(ToPercent(live.Voice));
			systemBgm?.SetValueWithoutNotify(ToPercent(system.Bgm));
			systemSe?.SetValueWithoutNotify(ToPercent(system.Se));
			systemVoice?.SetValueWithoutNotify(ToPercent(system.Voice));

			defaultVocal?.SetValueWithoutNotify(systemOptions.DefaultVocalType);
			screenLock?.SetOnWithoutNotify(systemOptions.LockScreenOrientation);
			confirmBeforeLive?.SetOnWithoutNotify(systemOptions.ConfirmSettingsBeforeLive);
			onlineStatus?.SetOnWithoutNotify(systemOptions.ShowOnlineStatus);
			liveBonusNotify?.SetOnWithoutNotify(systemOptions.NotifyLiveBonusFullRecovery);

			musicInfoDisplay?.SetValueWithoutNotify(liveSetting.UsesCustomMusicScoreMusicInfoDisplay ? 0 : 1);
			liveBackground?.SetValueWithoutNotify(
				(liveSetting.CustomMusicScoreLiveBackgroundMode ?? LiveSettingData.CustomMusicScoreBackgroundMode2DMV)
				== LiveSettingData.CustomMusicScoreBackgroundModeJacket ? 0 : 1);
			fullscreenRow?.SetActive(SupportsFullscreenSetting);
			fullscreen?.SetOnWithoutNotify(localSettings.FullscreenEnabled ?? Screen.fullScreen);
		}

		private void BindEvents()
		{
			Listen(noteSpeed, v => liveSetting.NoteSpeed = v);
			Listen(timingAdjust, v => liveSetting.TimingAdjustData = v);
			Listen(noteSkin, v => liveSetting.NoteSkinIndex = v);
			Listen(longNoteAlpha, v => liveSetting.NoteAlpha = Mathf.Max(v, MinNoteAlphaPercent) / 100f);
			Listen(guideAlpha, v => liveSetting.GuideAlpha = Mathf.Max(v, MinNoteAlphaPercent) / 100f);
			Listen(noteSe, v => liveSetting.NoteSeIndex = v);
			previewDetailButton?.onClick.AddListener(OpenNoteDetail);
			if (seTestButtons != null)
			{
				for (int i = 0; i < seTestButtons.Length && i < SeTestCues.Length; i++)
				{
					string cue = SeTestCues[i];
					seTestButtons[i]?.onClick.AddListener(() => PlaySeTest(cue));
				}
			}

			Listen(brightness, v => liveSetting.Brightness = v / 100f);
			Listen(laneAlpha, v => liveSetting.LaneTransparent = v / 100f);
			Listen(tapEffect, v => liveSetting.NoteEffect = v);
			Listen(cutIn, v => liveSetting.UseCutIn = v == 0);
			Listen(fever, v => liveSetting.FeverEffectTypeIndex = v);
			Listen(skillDisplay, v => liveSetting.HiddenSkillAndPraise = v != 0);
			Listen(fps, v => liveSetting.Use120FPS = v == 0);

			Listen(simultaneousLine, v => liveSetting.UseSimultaneousPushingLine = v == 0);
			Listen(apEffect, v => liveSetting.UseAllPerfectEffect = v == 0);
			Listen(fastLateFlick, v => liveSetting.IsFastLateFlick = v == 0);
			Listen(mirror, v => liveSetting.IsMirror = v == 0);
			Listen(vibration, v => liveSetting.UseVibration = v == 0);
			Listen(mvMode, v => liveSetting.LiveMode = IndexToLiveMode(v));
			Listen(mvQuality, v => liveSetting.QualityType = v == 0 ? MVQualityType.High : MVQualityType.Default);

			Listen(liveBgm, v => LiveVolume.Bgm = v / 100f);
			Listen(liveSe, v => LiveVolume.Se = v / 100f);
			Listen(liveVoice, v => LiveVolume.Voice = v / 100f);
			Listen(systemBgm, v => localSettings.SystemVolume.Bgm = v / 100f);
			Listen(systemSe, v => localSettings.SystemVolume.Se = v / 100f);
			Listen(systemVoice, v => localSettings.SystemVolume.Voice = v / 100f);

			Listen(defaultVocal, v => localSettings.SystemOptions.DefaultVocalType = v);
			Listen(screenLock, v => localSettings.SystemOptions.LockScreenOrientation = v == 0);
			Listen(confirmBeforeLive, v => localSettings.SystemOptions.ConfirmSettingsBeforeLive = v == 0);
			Listen(onlineStatus, v => localSettings.SystemOptions.ShowOnlineStatus = v == 0);
			Listen(liveBonusNotify, v => localSettings.SystemOptions.NotifyLiveBonusFullRecovery = v == 0);

			Listen(musicInfoDisplay, v => liveSetting.CustomMusicScoreMusicInfoDisplayMode = v == 0
				? LiveSettingData.MusicInfoDisplayModeCustomScore
				: LiveSettingData.MusicInfoDisplayModeNormal);
			Listen(liveBackground, v => liveSetting.CustomMusicScoreLiveBackgroundMode = v == 0
				? LiveSettingData.CustomMusicScoreBackgroundModeJacket
				: LiveSettingData.CustomMusicScoreBackgroundMode2DMV);
			Listen(fullscreen, v => localSettings.FullscreenEnabled = v == 0);
		}

		private ApplicationLocalSettings.VolumeSettings LiveVolume => localSettings.LiveVolume ?? localSettings.SetupLiveVolume();

		private static bool SupportsFullscreenSetting
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

		private void Listen(OptionStepper stepper, System.Action<float> write)
		{
			if (stepper != null)
			{
				stepper.OnValueChanged += v => Commit(() => write(v));
			}
		}

		private void Listen(OptionRadioGroup group, System.Action<int> write)
		{
			if (group != null)
			{
				group.OnValueChanged += v => Commit(() => write(v));
			}
		}

		private void Listen(OptionSelector selector, System.Action<int> write)
		{
			if (selector != null)
			{
				selector.OnValueChanged += v => Commit(() => write(v));
			}
		}

		private void Listen(OptionVolumeSlider slider, System.Action<int> write)
		{
			if (slider != null)
			{
				slider.OnValueChanged += v => Commit(() => write(v));
			}
		}

		/// <summary>Writes one change, saves both stores and applies what takes effect outside a live.</summary>
		private void Commit(System.Action write)
		{
			write();
			LiveSettingData.SaveToStorage(liveSetting);
			ApplicationLocalSettings.SaveToStorage(localSettings);

			ApplicationLocalSettings.VolumeSettings system = localSettings.SystemVolume;
			SoundManager.Instance.SetupVolume(1f, system.Bgm, system.Se, system.Voice);
			LiveConfig.LongNoteAlpha = liveSetting.GetNoteAlpha();
			LiveConfig.GuideAlpha = liveSetting.GetGuideAlpha();
			LiveConfig.SetNoteSkinAssetBundleName(liveSetting.NoteSkinIndex);
			LiveConfig.SetNoteSeName(liveSetting.NoteSeIndex);
			LiveConfig.SetNoteEffectName(liveSetting.NoteEffect);
			LiveConfig.SetFeverEffectName(liveSetting.FeverEffectTypeIndex);
			if (SupportsFullscreenSetting && localSettings.FullscreenEnabled.HasValue
				&& Screen.fullScreen != localSettings.FullscreenEnabled.Value)
			{
				ScreenConfig.SetStandaloneFullscreen(localSettings.FullscreenEnabled.Value);
			}
			RefreshAlphaPreviews();
		}

		private void RefreshAlphaPreviews()
		{
			SetAlpha(longNotePreview, liveSetting?.GetNoteAlpha() ?? 1f);
			SetAlpha(guidePreview, liveSetting?.GetGuideAlpha() ?? 0.6f);
		}

		private static void SetAlpha(Graphic graphic, float alpha)
		{
			if (graphic == null)
			{
				return;
			}

			Color color = graphic.color;
			color.a = alpha;
			graphic.color = color;
		}

		private void OpenNoteDetail()
		{
			var dialog = ScreenManager.Instance?.ShowDialog<LiveNoteSettingDialog>(DialogType.LiveNoteSettingDialog);
			dialog?.Setup(
				liveSetting.NoteSpeed,
				liveSetting._noteShowRate * 100f,
				liveSetting.TimingAdjustData,
				(speed, startPosition, timing) => Commit(() =>
				{
					liveSetting.NoteSpeed = speed;
					liveSetting.SetNoteShowRate(startPosition);
					liveSetting.TimingAdjustData = timing;
					noteSpeed?.SetValueWithoutNotify(speed);
					timingAdjust?.SetValueWithoutNotify(timing);
				}));
		}

		private void PlaySeTest(string cue)
		{
			string bundle = "live/tap_se/" + LiveConfig.GetNoteSeName(liveSetting.NoteSeIndex);
			if (loadedSeBundle != bundle)
			{
				SoundManager.Instance.LoadSoundBundle(bundle, false);
				loadedSeBundle = bundle;
			}

			if (cue != LiveSoundDefine.SE_LIVE_LONG)
			{
				SoundManager.Instance.PlaySE(cue);
				return;
			}

			// The long-note SE loops in a live; the test plays a short stretch of it.
			StopLongSeTest();
			longSePlaybackId = SoundManager.Instance.PlaySE(cue);
			stopLongSeRoutine = StartCoroutine(StopLongSeAfterDelay());
		}

		private IEnumerator StopLongSeAfterDelay()
		{
			yield return new WaitForSecondsRealtime(SeTestLongDuration);
			stopLongSeRoutine = null;
			StopLongSeTest();
		}

		private void StopLongSeTest()
		{
			if (stopLongSeRoutine != null)
			{
				StopCoroutine(stopLongSeRoutine);
				stopLongSeRoutine = null;
			}
			if (longSePlaybackId != 0)
			{
				SoundManager.Instance.StopSE(longSePlaybackId);
				longSePlaybackId = 0;
			}
		}

		protected override void OnDisable()
		{
			StopLongSeTest();
			base.OnDisable();
		}

		private static int ToPercent(float volume)
		{
			return Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f);
		}

		// MV設定 > モード: 3DMV / 2DMV / 原曲MV / 軽量.
		private static int LiveModeToIndex(LiveSettingData.LiveModeType mode)
		{
			return mode switch
			{
				LiveSettingData.LiveModeType.Mode2D => 1,
				LiveSettingData.LiveModeType.OriginalMV => 2,
				LiveSettingData.LiveModeType.Low => 3,
				_ => 0,
			};
		}

		private static LiveSettingData.LiveModeType IndexToLiveMode(int index)
		{
			return index switch
			{
				1 => LiveSettingData.LiveModeType.Mode2D,
				2 => LiveSettingData.LiveModeType.OriginalMV,
				3 => LiveSettingData.LiveModeType.Low,
				_ => LiveSettingData.LiveModeType.Default3D,
			};
		}
	}
}
