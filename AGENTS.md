# AGENTS.md

Notes for anyone (human or agent) working on OpenSekai.

## Option dialog: placeholder settings

The option dialog (`Assets/OpenSekai/Runtime/Game/UI/Option/OptionDialog.cs`, prefab
`Assets/OpenSekai/Resources/dialog/OptionDialog.prefab`) copies the official オプション dialog. Its ライブ, 音量 and システム
tabs match the original. The fourth tab, コミュニケーション in the original, is **Additionals** and holds OpenSekai's own
settings.

Some settings exist in the original game but OpenSekai has nothing behind them yet. They are in the dialog anyway: you can
change them and they are saved, but **they have no effect**. When you implement one, remove it from this list.

| Tab | Setting | Stored in | Why it has no effect |
| --- | --- | --- | --- |
| ライブ | カットイン演出 | `LiveSettingData.UseCutIn` | Nothing reads it. |
| ライブ | FEVER演出 (標準 / 軽量) | `LiveSettingData.FeverEffectTypeIndex` | `LiveConfig.SetFeverEffectName` sets a bundle name, but no `effect_asset/live/fever/*` bundle exists and nothing loads one. |
| ライブ | スキル・活躍表示 | `LiveSettingData.HiddenSkillAndPraise` (inverted) | Nothing reads it. |
| ライブ | 滑らかさ(FPS) (高品質 / 標準) | `LiveSettingData.Use120FPS` | Nothing reads it. The frame rate comes from `FramerateUtility.GetFeasibleFramerate`. |
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

- ノーツスキン and ノーツのSE list only the two skins (`live/note/custom01`, `custom02`) and the two tap-SE sets
  (`live/tap_se/custom01`, `custom02`) that ship in the project. The original has more.
- The ノーツスキン page for custom01 is put together from that skin's note sprites. custom02 uses the official
  `note_preview/custom02` strip.
