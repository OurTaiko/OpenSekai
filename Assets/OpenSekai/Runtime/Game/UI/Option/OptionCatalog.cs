using System;
using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	/// <summary>How a row shows and edits its value.</summary>
	public enum OptionRowKind
	{
		/// <summary>Delta buttons around a value box (ノーツの速さ, 不透明度, ...).</summary>
		Stepper,
		/// <summary>Radio options (ON / OFF, 標準 / 控えめ, ...).</summary>
		Radio,
		/// <summary>The 音量 row: − slider +.</summary>
		Volume,
		/// <summary>"&lt; label &gt;" (ノーツのSE).</summary>
		Selector,
		/// <summary>The ノーツスキン carousel.</summary>
		Skin,
		/// <summary>A label with one capsule button (容量管理).</summary>
		Button,
		/// <summary>Capsule buttons in two columns (the SE tests).</summary>
		ButtonGrid,
		/// <summary>The ノーツ設定 preview with its プレビューで詳細設定 button.</summary>
		NotePreview,
		/// <summary>An opacity preview whose band follows another row's value.</summary>
		LanePreview,
		/// <summary>A centred line of text (the detail dialog's explanation and hint).</summary>
		Text,
	}

	/// <summary>How a block lays out its rows.</summary>
	public enum OptionBlockLayout
	{
		/// <summary>Rows in a 593-wide left column, side rows in a 400-wide right column.</summary>
		Split,
		/// <summary>One full-width row.</summary>
		Full,
		/// <summary>320-wide cells, three per line (a wide row takes two).</summary>
		Columns,
		/// <summary>Two 500-wide cells per line.</summary>
		Halves,
		/// <summary>Label and control on one line (MV設定).</summary>
		Inline,
		/// <summary>Full-width rows 14 apart (音量).</summary>
		Volume,
		/// <summary>Centred rows (the detail dialog).</summary>
		Centered,
	}

	/// <summary>
	/// One row of the option dialog. Number rows (Stepper, Volume) read and write a float, choice rows (Radio, Selector,
	/// Skin) an index. Rows are found by <see cref="Id"/>.
	/// </summary>
	public sealed class OptionRow
	{
		public string Id { get; }
		public OptionRowKind Kind { get; }
		public string Label { get; }

		public IReadOnlyList<string> Options { get; private set; } = Array.Empty<string>();
		public IReadOnlyList<float> Deltas { get; private set; } = Array.Empty<float>();
		public float Minimum { get; private set; }
		public float Maximum { get; private set; } = 100f;
		public string Format { get; private set; } = "0";
		public string Suffix { get; private set; } = string.Empty;
		/// <summary>Button and ButtonGrid: the action each button runs.</summary>
		public IReadOnlyList<string> Actions { get; private set; } = Array.Empty<string>();
		/// <summary>LanePreview: the row whose value (a percentage) sets the band's opacity.</summary>
		public string BoundRowId { get; private set; }
		/// <summary>LanePreview: the band sprite.</summary>
		public string Art { get; private set; }
		/// <summary>Text: drawn in the pink hint colour.</summary>
		public bool IsHint { get; private set; }
		/// <summary>Button: text that changes while the dialog is open (the account button); read on every refresh.</summary>
		public Func<string> ButtonText { get; private set; }

		/// <summary>Split: goes to the right column.</summary>
		public bool IsSide { get; private set; }
		/// <summary>Columns: takes two cells.</summary>
		public bool IsWide { get; private set; }
		/// <summary>Radio: width of each option; 0 packs them tight, which only works when they fit the row.</summary>
		public float SlotWidth { get; private set; } = 159f;
		/// <summary>Columns / Halves: hidden on this platform (フルスクリーン off the desktop).</summary>
		public bool IsHidden { get; private set; }

		/// <summary>Why the setting is stored but does nothing yet (see AGENTS.md); null when it works.</summary>
		public string NoEffect { get; private set; }

		/// <summary>Volume: the cue played at the new volume when it changes (the original's SE_VOLCHANGE_*); null plays the
		/// gauge slider's up / down sound instead.</summary>
		public string PreviewCue { get; private set; }
		/// <summary>Volume: <see cref="PreviewCue"/> is a voice and plays in the voice group.</summary>
		public bool PreviewIsVoice { get; private set; }

		private Func<OptionSettings, float> getNumber;
		private Action<OptionSettings, float> setNumber;
		private Func<OptionSettings, int> getIndex;
		private Action<OptionSettings, int> setIndex;

		private OptionRow(string id, OptionRowKind kind, string label)
		{
			Id = id;
			Kind = kind;
			Label = label;
		}

		public bool HasNumber => getNumber != null;
		public bool HasIndex => getIndex != null;

		public float GetNumber(OptionSettings settings) => getNumber(settings);
		public void SetNumber(OptionSettings settings, float value) => setNumber(settings, Mathf.Clamp(value, Minimum, Maximum));
		public int GetIndex(OptionSettings settings) => Mathf.Clamp(getIndex(settings), 0, Mathf.Max(0, Options.Count - 1));
		public void SetIndex(OptionSettings settings, int value) => setIndex(settings, Mathf.Clamp(value, 0, Mathf.Max(0, Options.Count - 1)));

		public static OptionRow Stepper(string id, string label, float[] deltas, float minimum, float maximum, string format, string suffix,
			Func<OptionSettings, float> get, Action<OptionSettings, float> set)
		{
			return new OptionRow(id, OptionRowKind.Stepper, label)
			{
				Deltas = deltas, Minimum = minimum, Maximum = maximum, Format = format, Suffix = suffix, getNumber = get, setNumber = set,
			};
		}

		/// <summary>A "− 50% +" stepper in steps of 10.</summary>
		public static OptionRow Percent(string id, string label, float minimum, Func<OptionSettings, float> get, Action<OptionSettings, float> set)
		{
			return Stepper(id, label, new[] { -10f, 10f }, minimum, 100f, "0", "%", get, set);
		}

		public static OptionRow Radio(string id, string label, string[] options, Func<OptionSettings, int> get, Action<OptionSettings, int> set)
		{
			return new OptionRow(id, OptionRowKind.Radio, label) { Options = options, getIndex = get, setIndex = set };
		}

		/// <summary>An ON / OFF radio; ON is index 0.</summary>
		public static OptionRow Toggle(string id, string label, Func<OptionSettings, bool> get, Action<OptionSettings, bool> set)
		{
			return Radio(id, label, new[] { "ON", "OFF" }, s => get(s) ? 0 : 1, (s, i) => set(s, i == 0));
		}

		/// <summary>A 0 to 100 volume over a 0 to 1 setting.</summary>
		public static OptionRow Volume(string id, string label, Func<OptionSettings, float> get, Action<OptionSettings, float> set)
		{
			return new OptionRow(id, OptionRowKind.Volume, label)
			{
				Minimum = 0f, Maximum = 100f, getNumber = s => Mathf.Round(Mathf.Clamp01(get(s)) * 100f), setNumber = (s, v) => set(s, v / 100f),
			};
		}

		public static OptionRow Selector(string id, string label, string[] options, Func<OptionSettings, int> get, Action<OptionSettings, int> set)
		{
			return new OptionRow(id, OptionRowKind.Selector, label) { Options = options, getIndex = get, setIndex = set };
		}

		public static OptionRow Skin(string id, string label, int skinCount, Func<OptionSettings, int> get, Action<OptionSettings, int> set)
		{
			var options = new string[skinCount];
			for (int i = 0; i < skinCount; i++)
			{
				options[i] = LiveConfig.GetNoteBundleName(i);
			}
			return new OptionRow(id, OptionRowKind.Skin, label) { Options = options, getIndex = get, setIndex = set };
		}

		public static OptionRow Button(string id, string label, string buttonLabel, string action)
		{
			return new OptionRow(id, OptionRowKind.Button, label) { Options = new[] { buttonLabel }, Actions = new[] { action } };
		}

		public static OptionRow ButtonGrid(string id, string[] labels, string[] actions)
		{
			return new OptionRow(id, OptionRowKind.ButtonGrid, string.Empty) { Options = labels, Actions = actions };
		}

		public static OptionRow NotePreview(string id, string buttonLabel, string action)
		{
			return new OptionRow(id, OptionRowKind.NotePreview, string.Empty) { Options = new[] { buttonLabel }, Actions = new[] { action } };
		}

		public static OptionRow LanePreview(string id, string boundRowId, string art)
		{
			return new OptionRow(id, OptionRowKind.LanePreview, string.Empty) { BoundRowId = boundRowId, Art = art };
		}

		public static OptionRow Text(string id, string text, bool hint = false)
		{
			return new OptionRow(id, OptionRowKind.Text, text) { IsHint = hint };
		}

		public OptionRow Side()
		{
			IsSide = true;
			return this;
		}

		public OptionRow Wide(float slotWidth)
		{
			IsWide = true;
			SlotWidth = slotWidth;
			return this;
		}

		public OptionRow Slots(float slotWidth)
		{
			SlotWidth = slotWidth;
			return this;
		}

		public OptionRow HiddenWhen(bool hidden)
		{
			IsHidden = hidden;
			return this;
		}

		public OptionRow Placeholder(string reason)
		{
			NoEffect = reason;
			return this;
		}

		public OptionRow ButtonTextFrom(Func<string> text)
		{
			ButtonText = text;
			return this;
		}

		public OptionRow PreviewWith(string cue, bool voice = false)
		{
			PreviewCue = cue;
			PreviewIsVoice = voice;
			return this;
		}

	}

	public sealed class OptionBlock
	{
		public OptionBlockLayout Layout { get; }
		public IReadOnlyList<OptionRow> Rows { get; }

		public OptionBlock(OptionBlockLayout layout, params OptionRow[] rows)
		{
			Layout = layout;
			Rows = rows;
		}
	}

	public sealed class OptionSection
	{
		public string Title { get; }
		public IReadOnlyList<OptionBlock> Blocks { get; }

		public OptionSection(string title, params OptionBlock[] blocks)
		{
			Title = title;
			Blocks = blocks;
		}
	}

	public sealed class OptionPage
	{
		public string Id { get; }
		public string Title { get; }
		public IReadOnlyList<OptionSection> Sections { get; }

		public OptionPage(string id, string title, params OptionSection[] sections)
		{
			Id = id;
			Title = title;
			Sections = sections;
		}
	}

	/// <summary>
	/// The option dialog's rows, page by page, as in the original オプション dialog (ライブ / 音量 / システム) plus
	/// OpenSekai's Additionals in place of コミュニケーション. Rows marked with <see cref="OptionRow.Placeholder"/> are saved
	/// but have nothing behind them yet; AGENTS.md lists them.
	/// </summary>
	public static class OptionCatalog
	{
		public const string ActionNoteDetail = "note-detail";
		public const string ActionSeTap = "se-tap";
		public const string ActionSeFlick = "se-flick";
		public const string ActionSeLong = "se-long";
		public const string ActionSeTrace = "se-trace";
		public const string ActionCommunityAccount = "community-account";
		public const string ActionNone = "none";

		public const int NoteSkinCount = 2;
		public const int NoteSeCount = 2;
		private const float MinNoteAlphaPercent = 10f;

		/// <summary>The string table of every label here (Localization/Strings/Option.csv).</summary>
		public const string Table = "Option";

		private static string T(string key) => L10n.Get(Table, key);

		/// <summary>Built for the selected language: build again after it changes.</summary>
		public static IReadOnlyList<OptionPage> Pages()
		{
			return new[] { Live(), Volume(), SystemPage(), Additionals() };
		}

		private static OptionPage Live()
		{
			return new OptionPage("live", T("tab.live"),
				new OptionSection(T("live.section.notes"),
					new OptionBlock(OptionBlockLayout.Split,
						OptionRow.Stepper("note-speed", T("live.noteSpeed"), new[] { -1f, -0.1f, 0.1f, 1f }, 1f, 12f, "0.00", "",
							s => s.Live.NoteSpeed, (s, v) => s.Live.NoteSpeed = v),
						OptionRow.Stepper("timing", T("live.timing"), new[] { -1f, -0.1f, 0.1f, 1f }, -20f, 20f, "0.00", "",
							s => s.Live.TimingAdjustData, (s, v) => s.Live.TimingAdjustData = v),
						OptionRow.NotePreview("note-preview", T("live.notePreview"), ActionNoteDetail).Side()),
					new OptionBlock(OptionBlockLayout.Full,
						OptionRow.Skin("note-skin", T("live.noteSkin"), NoteSkinCount, s => s.Live.NoteSkinIndex, (s, i) => s.Live.NoteSkinIndex = i)),
					new OptionBlock(OptionBlockLayout.Split,
						OptionRow.Percent("long-alpha", T("live.longAlpha"), MinNoteAlphaPercent,
							s => s.Live.GetNoteAlpha() * 100f, (s, v) => s.Live.NoteAlpha = v / 100f),
						OptionRow.LanePreview("long-preview", "long-alpha", "longNoteLine_0").Side()),
					new OptionBlock(OptionBlockLayout.Split,
						OptionRow.Percent("guide-alpha", T("live.guideAlpha"), MinNoteAlphaPercent,
							s => s.Live.GetGuideAlpha() * 100f, (s, v) => s.Live.GuideAlpha = v / 100f),
						OptionRow.LanePreview("guide-preview", "guide-alpha", "longNoteLine_1").Side()),
					new OptionBlock(OptionBlockLayout.Split,
						OptionRow.Selector("note-se", T("live.noteSe"), SoundNames(), s => s.Live.NoteSeIndex, (s, i) => s.Live.NoteSeIndex = i),
						OptionRow.ButtonGrid("se-test", new[] { T("live.seTest.tap"), T("live.seTest.flick"), T("live.seTest.long"), T("live.seTest.trace") },
							new[] { ActionSeTap, ActionSeFlick, ActionSeLong, ActionSeTrace }).Side())),
				new OptionSection(T("live.section.display"),
					new OptionBlock(OptionBlockLayout.Columns,
						OptionRow.Percent("brightness", T("live.brightness"), 50f, s => s.Live.Brightness * 100f, (s, v) => s.Live.Brightness = v / 100f),
						OptionRow.Percent("lane-alpha", T("live.laneAlpha"), 0f, s => s.Live.LaneTransparent * 100f, (s, v) => s.Live.LaneTransparent = v / 100f),
						OptionRow.Radio("tap-effect", T("live.tapEffect"), new[] { T("live.tapEffect.normal"), T("live.tapEffect.subtle") }, s => s.Live.NoteEffect, (s, i) => s.Live.NoteEffect = i),
						OptionRow.Toggle("cut-in", T("live.cutIn"), s => s.Live.UseCutIn, (s, on) => s.Live.UseCutIn = on)
							.Placeholder("Nothing reads LiveSettingData.UseCutIn."),
						OptionRow.Radio("fever", T("live.fever"), new[] { T("live.fever.normal"), T("live.fever.light") }, s => s.Live.FeverEffectTypeIndex, (s, i) => s.Live.FeverEffectTypeIndex = i)
							.Placeholder("No effect_asset/live/fever bundle exists."),
						OptionRow.Toggle("skill-display", T("live.skillDisplay"), s => !s.Live.HiddenSkillAndPraise, (s, on) => s.Live.HiddenSkillAndPraise = !on)
							.Placeholder("Nothing reads LiveSettingData.HiddenSkillAndPraise."),
						// OpenSekai offers 60 / 120 / Unlimited in place of the original's 高品質 / 標準. Three options need
						// about 410px (a 54px circle, 17px gap and the text each; "Unlimited" alone is ~143px), more than one
						// 320px cell, so the row takes two cells; it is alone on its line.
						OptionRow.Radio("fps", T("live.fps"), new[] { "60", "120", T("live.fps.unlimited") },
							s => s.Live.LiveFrameRateMode, (s, i) => s.Live.FrameRateMode = i).Wide(220f))),
				new OptionSection(T("live.section.assist"),
					new OptionBlock(OptionBlockLayout.Columns,
						OptionRow.Toggle("simultaneous-line", T("live.simultaneousLine"), s => s.Live.UseSimultaneousPushingLine, (s, on) => s.Live.UseSimultaneousPushingLine = on),
						OptionRow.Toggle("ap-effect", T("live.apEffect"), s => s.Live.UseAllPerfectEffect, (s, on) => s.Live.UseAllPerfectEffect = on),
						OptionRow.Toggle("fast-late-flick", "LATE/FAST/FLICK", s => s.Live.IsFastLateFlick, (s, on) => s.Live.IsFastLateFlick = on),
						OptionRow.Toggle("mirror", T("live.mirror"), s => s.Live.IsMirror, (s, on) => s.Live.IsMirror = on),
						OptionRow.Toggle("vibration", T("live.vibration"), s => s.Live.UseVibration, (s, on) => s.Live.UseVibration = on))),
				new OptionSection(T("live.section.mv"),
					new OptionBlock(OptionBlockLayout.Inline,
						OptionRow.Radio("mv-mode", T("live.mvMode"), new[] { "3DMV", "2DMV", T("live.mvMode.original"), T("live.mvMode.light") },
							s => LiveModeToIndex(s.Live.LiveMode), (s, i) => s.Live.LiveMode = IndexToLiveMode(i))
							.Slots(205f).Placeholder("Custom charts take their background from Additionals > ライブ背景."),
						OptionRow.Radio("mv-quality", T("live.mvQuality"), new[] { T("live.mvQuality.high"), T("live.mvQuality.normal") },
							s => s.Live.QualityType == MVQualityType.High ? 0 : 1, (s, i) => s.Live.QualityType = i == 0 ? MVQualityType.High : MVQualityType.Default)
							.Slots(205f))));
		}

		private static OptionPage Volume()
		{
			return new OptionPage("volume", T("tab.volume"),
				new OptionSection(T("volume.section.live"),
					new OptionBlock(OptionBlockLayout.Volume,
						OptionRow.Volume("live-bgm", "BGM", s => s.LiveVolume.Bgm, (s, v) => s.LiveVolume.Bgm = v),
						OptionRow.Volume("live-se", "SE", s => s.LiveVolume.Se, (s, v) => s.LiveVolume.Se = v)
							.PreviewWith("SE_VOLCHANGE_SE_INGAME"),
						OptionRow.Volume("live-voice", T("volume.voice"), s => s.LiveVolume.Voice, (s, v) => s.LiveVolume.Voice = v)
							.PreviewWith("SE_VOLCHANGE_VOX_INGAME", voice: true))),
				new OptionSection(T("volume.section.system"),
					new OptionBlock(OptionBlockLayout.Volume,
						OptionRow.Volume("system-bgm", "BGM", s => s.SystemVolume.Bgm, (s, v) => s.SystemVolume.Bgm = v),
						OptionRow.Volume("system-se", "SE", s => s.SystemVolume.Se, (s, v) => s.SystemVolume.Se = v)
							.PreviewWith("SE_VOLCHANGE_SE_UI"),
						OptionRow.Volume("system-voice", T("volume.voice"), s => s.SystemVolume.Voice, (s, v) => s.SystemVolume.Voice = v)
							.PreviewWith("SE_VOLCHANGE_VOX_SCENARIO", voice: true))));
		}

		private static OptionPage SystemPage()
		{
			const string unused = "Stored in ApplicationLocalSettings.SystemOptions; nothing reads it.";
			const string noHandler = "The button has no handler.";
			return new OptionPage("system", T("tab.system"),
				new OptionSection(T("system.section.system"),
					new OptionBlock(OptionBlockLayout.Columns,
						OptionRow.Radio("default-vocal", T("system.defaultVocal"), new[] { T("system.defaultVocal.sekai"), T("system.defaultVocal.virtualSinger") },
							s => s.SystemOptions.DefaultVocalType, (s, i) => s.SystemOptions.DefaultVocalType = i).Wide(229f).Placeholder(unused),
						OptionRow.Toggle("screen-lock", T("system.screenLock"), s => s.SystemOptions.LockScreenOrientation, (s, on) => s.SystemOptions.LockScreenOrientation = on)
							.Placeholder(unused),
						OptionRow.Toggle("confirm-before-live", T("system.confirmBeforeLive"), s => s.SystemOptions.ConfirmSettingsBeforeLive, (s, on) => s.SystemOptions.ConfirmSettingsBeforeLive = on)
							.Placeholder(unused),
						OptionRow.Toggle("online-status", T("system.onlineStatus"), s => s.SystemOptions.ShowOnlineStatus, (s, on) => s.SystemOptions.ShowOnlineStatus = on)
							.Placeholder(unused),
						OptionRow.Toggle("live-bonus-notify", T("system.liveBonusNotify"), s => s.SystemOptions.NotifyLiveBonusFullRecovery, (s, on) => s.SystemOptions.NotifyLiveBonusFullRecovery = on)
							.Placeholder(unused))),
				new OptionSection(T("system.section.data"),
					new OptionBlock(OptionBlockLayout.Columns,
						OptionRow.Button("vocal-data", T("system.vocalData"), T("system.manageStorage"), ActionNone).Placeholder(noHandler),
						OptionRow.Button("mv-data", T("system.mvData"), T("system.manageStorage"), ActionNone).Placeholder(noHandler))),
				new OptionSection(T("system.section.download"),
					new OptionBlock(OptionBlockLayout.Columns,
						OptionRow.Button("download-voice", T("system.downloadVoice"), T("system.download"), ActionNone).Placeholder(noHandler),
						OptionRow.Button("download-music", T("system.downloadMusic"), T("system.download"), ActionNone).Placeholder(noHandler),
						OptionRow.Button("download-mv", T("system.downloadMv"), T("system.download"), ActionNone).Placeholder(noHandler))));
		}

		private static OptionPage Additionals()
		{
			return new OptionPage("additionals", T("tab.additionals"),
				new OptionSection(T("additionals.section.opensekai"),
					new OptionBlock(OptionBlockLayout.Halves,
						OptionRow.Radio("music-info", T("additionals.musicInfo"), new[] { T("additionals.musicInfo.custom"), T("additionals.musicInfo.normal") },
							s => s.Live.UsesCustomMusicScoreMusicInfoDisplay ? 0 : 1,
							(s, i) => s.Live.CustomMusicScoreMusicInfoDisplayMode = i == 0
								? LiveSettingData.MusicInfoDisplayModeCustomScore
								: LiveSettingData.MusicInfoDisplayModeNormal).Slots(229f),
						OptionRow.Radio("live-background", T("additionals.liveBackground"), new[] { T("additionals.liveBackground.jacket"), T("additionals.liveBackground.mv2d") },
							s => (s.Live.CustomMusicScoreLiveBackgroundMode ?? LiveSettingData.CustomMusicScoreBackgroundMode2DMV)
								== LiveSettingData.CustomMusicScoreBackgroundModeJacket ? 0 : 1,
							(s, i) => s.Live.CustomMusicScoreLiveBackgroundMode = i == 0
								? LiveSettingData.CustomMusicScoreBackgroundModeJacket
								: LiveSettingData.CustomMusicScoreBackgroundMode2DMV).Slots(250f),
						OptionRow.Toggle("fullscreen", T("additionals.fullscreen"),
							s => s.Local.FullscreenEnabled ?? Screen.fullScreen, (s, on) => s.Local.FullscreenEnabled = on)
							.Slots(229f).HiddenWhen(!OptionSettings.SupportsFullscreen)),
					// Each language's own name; the game follows the system's language until one is picked here. Three
					// options need two cells, as 滑らかさ(FPS) does.
					new OptionBlock(OptionBlockLayout.Columns,
						OptionRow.Radio("language", T("additionals.language"), L10n.LanguageNames,
							s => Mathf.Max(0, Array.IndexOf(L10n.LanguageCodes, L10n.Supported(s.Local.Language) ?? L10n.Language)),
							(s, i) => s.Local.Language = L10n.LanguageCodes[i]).Wide(220f))),
				// Sign-in for the online charts (CommunityCharts); the button opens CommunityLoginPanel.
				new OptionSection(T("additionals.section.account"),
					new OptionBlock(OptionBlockLayout.Halves,
						OptionRow.Button("community-account", T("additionals.ourtaikoAccount"), T("additionals.signIn"), ActionCommunityAccount)
							.ButtonTextFrom(() => Community.CommunityAccount.IsSignedIn ? Community.CommunityAccount.Nickname : T("additionals.signIn")))));
		}

		/// <summary>The プレビューで詳細設定 dialog: note speed and timing down to 0.01, and ノーツの開始位置.</summary>
		public static OptionPage NoteDetail()
		{
			return new OptionPage("note-detail", string.Empty,
				new OptionSection(T("detail.noteSpeed"),
					new OptionBlock(OptionBlockLayout.Centered,
						OptionRow.Stepper("note-speed", string.Empty, new[] { -1f, -0.1f, -0.01f, 0.01f, 0.1f, 1f }, 1f, 12f, "0.00", "",
							s => s.Live.NoteSpeed, (s, v) => s.Live.NoteSpeed = v))),
				new OptionSection(T("detail.startPosition"),
					new OptionBlock(OptionBlockLayout.Centered,
						OptionRow.Stepper("start-position", string.Empty, new[] { -10f, -1f, 1f, 10f }, 0f, 100f, "0", "",
							s => s.Live._noteShowRate * 100f, (s, v) => s.Live.SetNoteShowRate(v)))),
				new OptionSection(T("detail.timing"),
					new OptionBlock(OptionBlockLayout.Centered,
						OptionRow.Text("timing-help", T("detail.timingHelp")),
						OptionRow.Stepper("timing", string.Empty, new[] { -1f, -0.1f, -0.01f, 0.01f, 0.1f, 1f }, -20f, 20f, "0.00", "",
							s => s.Live.TimingAdjustData, (s, v) => s.Live.TimingAdjustData = v),
						OptionRow.Text("timing-hint", T("detail.timingHint"), true))));
		}

		private static string[] SoundNames()
		{
			var names = new string[NoteSeCount];
			for (int i = 0; i < NoteSeCount; i++)
			{
				names[i] = L10n.Format(Table, "live.noteSeName", i + 1);
			}
			return names;
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
