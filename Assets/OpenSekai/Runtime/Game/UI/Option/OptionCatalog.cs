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

		/// <summary>Split: goes to the right column.</summary>
		public bool IsSide { get; private set; }
		/// <summary>Columns: takes two cells.</summary>
		public bool IsWide { get; private set; }
		/// <summary>Radio: width of each option; 0 packs them tight (滑らかさ).</summary>
		public float SlotWidth { get; private set; } = 159f;
		/// <summary>Columns / Halves: hidden on this platform (フルスクリーン off the desktop).</summary>
		public bool IsHidden { get; private set; }

		/// <summary>Why the setting is stored but does nothing yet (see AGENTS.md); null when it works.</summary>
		public string NoEffect { get; private set; }

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
		public const string ActionNone = "none";

		public const int NoteSkinCount = 2;
		public const int NoteSeCount = 2;
		private const float MinNoteAlphaPercent = 10f;

		private static readonly string[] OnOff = { "ON", "OFF" };

		public static IReadOnlyList<OptionPage> Pages()
		{
			return new[] { Live(), Volume(), SystemPage(), Additionals() };
		}

		private static OptionPage Live()
		{
			return new OptionPage("live", "ライブ",
				new OptionSection("ノーツ設定",
					new OptionBlock(OptionBlockLayout.Split,
						OptionRow.Stepper("note-speed", "ノーツの速さ", new[] { -1f, -0.1f, 0.1f, 1f }, 1f, 12f, "0.00", "",
							s => s.Live.NoteSpeed, (s, v) => s.Live.NoteSpeed = v),
						OptionRow.Stepper("timing", "タイミング調整", new[] { -1f, -0.1f, 0.1f, 1f }, -20f, 20f, "0.00", "",
							s => s.Live.TimingAdjustData, (s, v) => s.Live.TimingAdjustData = v),
						OptionRow.NotePreview("note-preview", "プレビューで詳細設定", ActionNoteDetail).Side()),
					new OptionBlock(OptionBlockLayout.Full,
						OptionRow.Skin("note-skin", "ノーツスキン", NoteSkinCount, s => s.Live.NoteSkinIndex, (s, i) => s.Live.NoteSkinIndex = i)),
					new OptionBlock(OptionBlockLayout.Split,
						OptionRow.Percent("long-alpha", "ロングノーツの不透明度", MinNoteAlphaPercent,
							s => s.Live.GetNoteAlpha() * 100f, (s, v) => s.Live.NoteAlpha = v / 100f),
						OptionRow.LanePreview("long-preview", "long-alpha", "longNoteLine_0").Side()),
					new OptionBlock(OptionBlockLayout.Split,
						OptionRow.Percent("guide-alpha", "ガイドの不透明度", MinNoteAlphaPercent,
							s => s.Live.GetGuideAlpha() * 100f, (s, v) => s.Live.GuideAlpha = v / 100f),
						OptionRow.LanePreview("guide-preview", "guide-alpha", "longNoteLine_1").Side()),
					new OptionBlock(OptionBlockLayout.Split,
						OptionRow.Selector("note-se", "ノーツのSE", SoundNames(), s => s.Live.NoteSeIndex, (s, i) => s.Live.NoteSeIndex = i),
						OptionRow.ButtonGrid("se-test", new[] { "♪ タップ", "♪ フリック", "♪ ロング", "♪ トレース" },
							new[] { ActionSeTap, ActionSeFlick, ActionSeLong, ActionSeTrace }).Side())),
				new OptionSection("見やすさ・演出設定",
					new OptionBlock(OptionBlockLayout.Columns,
						OptionRow.Percent("brightness", "背景の明るさ", 50f, s => s.Live.Brightness * 100f, (s, v) => s.Live.Brightness = v / 100f),
						OptionRow.Percent("lane-alpha", "レーンの不透明度", 0f, s => s.Live.LaneTransparent * 100f, (s, v) => s.Live.LaneTransparent = v / 100f),
						OptionRow.Radio("tap-effect", "タップエフェクト", new[] { "標準", "控えめ" }, s => s.Live.NoteEffect, (s, i) => s.Live.NoteEffect = i),
						OptionRow.Toggle("cut-in", "カットイン演出", s => s.Live.UseCutIn, (s, on) => s.Live.UseCutIn = on)
							.Placeholder("Nothing reads LiveSettingData.UseCutIn."),
						OptionRow.Radio("fever", "FEVER演出", new[] { "標準", "軽量" }, s => s.Live.FeverEffectTypeIndex, (s, i) => s.Live.FeverEffectTypeIndex = i)
							.Placeholder("No effect_asset/live/fever bundle exists."),
						OptionRow.Toggle("skill-display", "スキル・活躍表示", s => !s.Live.HiddenSkillAndPraise, (s, on) => s.Live.HiddenSkillAndPraise = !on)
							.Placeholder("Nothing reads LiveSettingData.HiddenSkillAndPraise."),
						OptionRow.Radio("fps", "滑らかさ(FPS)", new[] { "高品質", "標準" }, s => s.Live.Use120FPS ? 0 : 1, (s, i) => s.Live.Use120FPS = i == 0)
							.Slots(0f).Placeholder("Nothing reads LiveSettingData.Use120FPS."))),
				new OptionSection("ライブ補助設定",
					new OptionBlock(OptionBlockLayout.Columns,
						OptionRow.Toggle("simultaneous-line", "同時押しライン", s => s.Live.UseSimultaneousPushingLine, (s, on) => s.Live.UseSimultaneousPushingLine = on),
						OptionRow.Toggle("ap-effect", "AP継続演出", s => s.Live.UseAllPerfectEffect, (s, on) => s.Live.UseAllPerfectEffect = on),
						OptionRow.Toggle("fast-late-flick", "LATE/FAST/FLICK", s => s.Live.IsFastLateFlick, (s, on) => s.Live.IsFastLateFlick = on),
						OptionRow.Toggle("mirror", "ミラー譜面", s => s.Live.IsMirror, (s, on) => s.Live.IsMirror = on),
						OptionRow.Toggle("vibration", "タップ時の振動", s => s.Live.UseVibration, (s, on) => s.Live.UseVibration = on))),
				new OptionSection("MV設定",
					new OptionBlock(OptionBlockLayout.Inline,
						OptionRow.Radio("mv-mode", "モード", new[] { "3DMV", "2DMV", "原曲MV", "軽量" },
							s => LiveModeToIndex(s.Live.LiveMode), (s, i) => s.Live.LiveMode = IndexToLiveMode(i))
							.Slots(205f).Placeholder("Custom charts take their background from Additionals > ライブ背景."),
						OptionRow.Radio("mv-quality", "3DMV品質", new[] { "高品質", "標準" },
							s => s.Live.QualityType == MVQualityType.High ? 0 : 1, (s, i) => s.Live.QualityType = i == 0 ? MVQualityType.High : MVQualityType.Default)
							.Slots(205f))));
		}

		private static OptionPage Volume()
		{
			return new OptionPage("volume", "音量",
				new OptionSection("ライブ音量",
					new OptionBlock(OptionBlockLayout.Volume,
						OptionRow.Volume("live-bgm", "BGM", s => s.LiveVolume.Bgm, (s, v) => s.LiveVolume.Bgm = v),
						OptionRow.Volume("live-se", "SE", s => s.LiveVolume.Se, (s, v) => s.LiveVolume.Se = v),
						OptionRow.Volume("live-voice", "ボイス", s => s.LiveVolume.Voice, (s, v) => s.LiveVolume.Voice = v))),
				new OptionSection("システム音量",
					new OptionBlock(OptionBlockLayout.Volume,
						OptionRow.Volume("system-bgm", "BGM", s => s.SystemVolume.Bgm, (s, v) => s.SystemVolume.Bgm = v),
						OptionRow.Volume("system-se", "SE", s => s.SystemVolume.Se, (s, v) => s.SystemVolume.Se = v),
						OptionRow.Volume("system-voice", "ボイス", s => s.SystemVolume.Voice, (s, v) => s.SystemVolume.Voice = v))));
		}

		private static OptionPage SystemPage()
		{
			const string unused = "Stored in ApplicationLocalSettings.SystemOptions; nothing reads it.";
			const string noHandler = "The button has no handler.";
			return new OptionPage("system", "システム",
				new OptionSection("システム",
					new OptionBlock(OptionBlockLayout.Columns,
						OptionRow.Radio("default-vocal", "デフォルト楽曲", new[] { "セカイver.", "バーチャル・シンガーver." },
							s => s.SystemOptions.DefaultVocalType, (s, i) => s.SystemOptions.DefaultVocalType = i).Wide(229f).Placeholder(unused),
						OptionRow.Toggle("screen-lock", "画面固定", s => s.SystemOptions.LockScreenOrientation, (s, on) => s.SystemOptions.LockScreenOrientation = on)
							.Placeholder(unused),
						OptionRow.Toggle("confirm-before-live", "ライブ前設定確認", s => s.SystemOptions.ConfirmSettingsBeforeLive, (s, on) => s.SystemOptions.ConfirmSettingsBeforeLive = on)
							.Placeholder(unused),
						OptionRow.Toggle("online-status", "オンライン表示", s => s.SystemOptions.ShowOnlineStatus, (s, on) => s.SystemOptions.ShowOnlineStatus = on)
							.Placeholder(unused),
						OptionRow.Toggle("live-bonus-notify", "ライブボーナス全回復通知", s => s.SystemOptions.NotifyLiveBonusFullRecovery, (s, on) => s.SystemOptions.NotifyLiveBonusFullRecovery = on)
							.Placeholder(unused))),
				new OptionSection("データ管理",
					new OptionBlock(OptionBlockLayout.Columns,
						OptionRow.Button("vocal-data", "ボーカルver.データ", "容量管理", ActionNone).Placeholder(noHandler),
						OptionRow.Button("mv-data", "MVデータ", "容量管理", ActionNone).Placeholder(noHandler))),
				new OptionSection("一括ダウンロード",
					new OptionBlock(OptionBlockLayout.Columns,
						OptionRow.Button("download-voice", "基本ボイス", "一括ダウンロード", ActionNone).Placeholder(noHandler),
						OptionRow.Button("download-music", "楽曲", "一括ダウンロード", ActionNone).Placeholder(noHandler),
						OptionRow.Button("download-mv", "3D・2D・原曲MV", "一括ダウンロード", ActionNone).Placeholder(noHandler))));
		}

		private static OptionPage Additionals()
		{
			return new OptionPage("additionals", "Additionals",
				new OptionSection("OpenSekai",
					new OptionBlock(OptionBlockLayout.Halves,
						OptionRow.Radio("music-info", "曲情報の表示", new[] { "自作譜面", "通常" },
							s => s.Live.UsesCustomMusicScoreMusicInfoDisplay ? 0 : 1,
							(s, i) => s.Live.CustomMusicScoreMusicInfoDisplayMode = i == 0
								? LiveSettingData.MusicInfoDisplayModeCustomScore
								: LiveSettingData.MusicInfoDisplayModeNormal).Slots(229f),
						OptionRow.Radio("live-background", "ライブ背景", new[] { "ジャケット", "2DMV" },
							s => (s.Live.CustomMusicScoreLiveBackgroundMode ?? LiveSettingData.CustomMusicScoreBackgroundMode2DMV)
								== LiveSettingData.CustomMusicScoreBackgroundModeJacket ? 0 : 1,
							(s, i) => s.Live.CustomMusicScoreLiveBackgroundMode = i == 0
								? LiveSettingData.CustomMusicScoreBackgroundModeJacket
								: LiveSettingData.CustomMusicScoreBackgroundMode2DMV).Slots(250f),
						OptionRow.Toggle("fullscreen", "フルスクリーン",
							s => s.Local.FullscreenEnabled ?? Screen.fullScreen, (s, on) => s.Local.FullscreenEnabled = on)
							.Slots(229f).HiddenWhen(!OptionSettings.SupportsFullscreen))));
		}

		/// <summary>The プレビューで詳細設定 dialog: note speed and timing down to 0.01, and ノーツの開始位置.</summary>
		public static OptionPage NoteDetail()
		{
			return new OptionPage("note-detail", string.Empty,
				new OptionSection("ノーツの速さ",
					new OptionBlock(OptionBlockLayout.Centered,
						OptionRow.Stepper("note-speed", string.Empty, new[] { -1f, -0.1f, -0.01f, 0.01f, 0.1f, 1f }, 1f, 12f, "0.00", "",
							s => s.Live.NoteSpeed, (s, v) => s.Live.NoteSpeed = v))),
				new OptionSection("ノーツの開始位置",
					new OptionBlock(OptionBlockLayout.Centered,
						OptionRow.Stepper("start-position", string.Empty, new[] { -10f, -1f, 1f, 10f }, 0f, 100f, "0", "",
							s => s.Live._noteShowRate * 100f, (s, v) => s.Live.SetNoteShowRate(v)))),
				new OptionSection("タイミング調整",
					new OptionBlock(OptionBlockLayout.Centered,
						OptionRow.Text("timing-help", "音に合わせてタップし、タイミングをあわせてください。"),
						OptionRow.Stepper("timing", string.Empty, new[] { -1f, -0.1f, -0.01f, 0.01f, 0.1f, 1f }, -20f, 20f, "0.00", "",
							s => s.Live.TimingAdjustData, (s, v) => s.Live.TimingAdjustData = v),
						OptionRow.Text("timing-hint", "判定ラインより上の場合は「＋」に、\n下の場合は「－」に調整してください。", true))));
		}

		private static string[] SoundNames()
		{
			var names = new string[NoteSeCount];
			for (int i = 0; i < NoteSeCount; i++)
			{
				names[i] = string.Format("サウンド{0:00}", i + 1);
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
