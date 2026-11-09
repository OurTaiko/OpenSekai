# AGENTS.md

Notes for anyone (human or agent) working on OpenSekai.

## Option dialog

The option dialog (`Assets/OpenSekai/Runtime/Game/UI/Option/OptionDialog.cs`, prefab
`Assets/OpenSekai/Resources/dialog/OptionDialog.prefab`) copies the official オプション dialog. Its ライブ, 音量 and システム
tabs match the original. The fourth tab, コミュニケーション in the original, is **Additionals** and holds OpenSekai's own
settings.

It is built the way OurTaikoPlay builds its settings overlay:

- `OptionCatalog` declares every page, section, block (its layout) and row (its kind, labels, range and the getter and
  setter on `OptionSettings`). To add or move a setting, edit the catalog.
- `OptionSettings` loads `LiveSettingData` and `ApplicationLocalSettings`, and `Commit()` saves and applies them.
- `OptionPageView` builds a page from the catalog. Each row is an instance of the prefab for its kind, listed in
  `Assets/OpenSekai/Prefabs/Option/OptionRowLibrary.asset`. The row prefabs (`OptionStepperRow`, `OptionRadioRow`, ...)
  are in the same folder; change a row's look there. Gaps and widths between rows are constants in `OptionPageView`,
  measured from a recording of the original.
- `OptionDialog` and `LiveNoteSettingDialog` (プレビューで詳細設定, built from `OptionCatalog.NoteDetail()`) only connect these.

## Option dialog: placeholder settings

Some settings exist in the original game but OpenSekai has nothing behind them yet. They are in the dialog anyway: you can
change them and they are saved, but **they have no effect**. Each one is marked with `.Placeholder(reason)` in
`OptionCatalog`. When you implement one, remove the marker and its row in this list.

| Tab | Setting | Stored in | Why it has no effect |
| --- | --- | --- | --- |
| ライブ | カットイン演出 | `LiveSettingData.UseCutIn` | Nothing reads it. |
| ライブ | FEVER演出 (標準 / 軽量) | `LiveSettingData.FeverEffectTypeIndex` | `LiveConfig.SetFeverEffectName` sets a bundle name, but no `effect_asset/live/fever/*` bundle exists and nothing loads one. |
| ライブ | スキル・活躍表示 | `LiveSettingData.HiddenSkillAndPraise` (inverted) | Nothing reads it. |
| ライブ | MV設定 > モード (3DMV / 2DMV / 原曲MV / 軽量) | `LiveSettingData.LiveMode` | Custom-chart lives take their background from Additionals > ライブ背景 (`CustomMusicScoreLiveBackgroundMode`). `LiveMode` only feeds the official-song paths (`LiveUtility`, `LiveModeButton`). |
| ライブ | プレビューで詳細設定: the lane preview and tap-to-check timing | none | The detail dialog (`LiveNoteSettingDialog`) only has the steppers. The preview box in the main tab is a static picture, and the dialog's 「音に合わせてタップし…」 hint refers to a tap check that does not exist yet. |
| システム | デフォルト楽曲 (セカイver. / バーチャル・シンガーver.) | `ApplicationLocalSettings.SystemOptions.DefaultVocalType` | Nothing reads it. |
| システム | 画面固定 | `SystemOptions.LockScreenOrientation` | Nothing reads it. |
| システム | ライブ前設定確認 | `SystemOptions.ConfirmSettingsBeforeLive` | Nothing reads it. |
| システム | オンライン表示 | `SystemOptions.ShowOnlineStatus` | Nothing reads it. |
| システム | ライブボーナス全回復通知 | `SystemOptions.NotifyLiveBonusFullRecovery` | Nothing reads it. |
| システム | データ管理 > 容量管理 (ボーカルver.データ, MVデータ) | none | The buttons have no handler. |
| システム | 一括ダウンロード (基本ボイス, 楽曲, 3D・2D・原曲MV) | none | The buttons have no handler. |

Also note:

- 滑らかさ(FPS) offers 60 / 120 / Unlimited instead of the original's 高品質 / 標準. It is stored in
  `LiveSettingData.FrameRateMode` (old saves fall back to `Use120FPS`). `BaseLiveController` applies it with
  `FramerateUtility.SetLiveFrameRate`, which turns vSync off for the live; the menu scenes' `SetFrameRate()` puts the
  quality level's vSync back.

- ノーツスキン and ノーツのSE list only the two skins (`live/note/custom01`, `custom02`) and the two tap-SE sets
  (`live/tap_se/custom01`, `custom02`) that ship in the project. The original has more.
- The ノーツスキン page for custom01 is put together from that skin's note sprites. custom02 uses the official
  `note_preview/custom02` strip.
