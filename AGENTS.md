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

## Home and community charts

The first scene is `Home` (navigation: 在线谱面, 自制谱管理, the OurTaiko account page, the option dialog). The
community chart browser is the `CommunityCharts` scene; its charts come from SekaiFanmade
(https://sekai.ourtaiko.org, separate repository `~/Repos/SekaiFanmade`, whose README lists the API).

- Code is in `Assets/OpenSekai/Runtime/Game/Community/`: `CommunityClient` (site API), `CommunityAccount` (sign-in),
  `CommunityLibrary` (download, import, `CustomMusicScores/CommunityInstalls.json`, play reports),
  `CommunityImageCache`, and `UI/` (`ScreenLayerOpenSekaiHome`, `ScreenLayerCommunityCharts`, `CommunityLoginPanel`
  and the two scene entry points). Screen types `OpenSekaiHome` (9000) and `CommunityCharts` (9001).
- Prefabs are in `Assets/OpenSekai/Prefabs/Community/`. They were made from the manager screen's parts: in
  `ScreenLayerCommunityCharts` the `DifficultyRail` and `ListArea` are still nested instances of the manager's part
  prefabs (so the rail and list cells look the same in both), the rest is its own. The list rows are
  `CustomMusicScoreEntryCell`. `CommunityLoginPanel` is nested in both screens.
- Sign-in works like OurTaikoPlay: the password goes only to OurTaiko SSO (`/api/v1/game/account/login`); the
  account token is kept in `community_account.dat` under `GamePaths.Root`, encrypted with a key of this device, and
  traded for a game session of the site (`/api/v1/game/tokens`, service from `/api/v1/meta`), sent as a Bearer token.
  The SSO Application needs 允许游戏登录和会话接口 and 允许游戏账号换取本应用的游戏会话 turned on.
- Downloads are imported with `CustomMusicScoreManagerService.ImportZip`, so they also show up in the manager.
  Lives started from either screen go through `CustomMusicScorePlayLauncher`; `ReturnScreenType` decides whether the
  live goes back to the manager or to `CommunityCharts`. A finished, non-auto live of a downloaded chart reports one
  play to the site (`CustomMusicScorePlayHistoryStorage` calls `CommunityLibrary.OnLocalChartPlayed`).
- Votes work like the site (Reddit style): ▲ / ▼ in the chart window send `PUT /api/v1/charts/<id>/vote` with 1, -1, or
  0 to take the vote back; `Score` is upvotes minus downvotes. おすすめ and 検索 > 人気 use `CommunitySort.Hot` (Reddit's
  hot ranking), ランキング 総合 uses `Top` (score) and デイリー uses `Daily` (plays in the last 24 hours).
- Chart ids are 32 random hex digits (`CommunityChart.Id`); `NumberId` is the short number 譜面ID検索 takes. Install
  records written when ids were numbers hold the number and are re-keyed to the id on first use.
- The signed-in account remembers which community server it belongs to: pointed at another server (a local
  SekaiFanmade) the game shows itself signed out and leaves `community_account.dat` alone.
- Jackets must be PNG or JPEG (`Texture2D.LoadImage`); the site stores them as JPEG.
- To point the game at a local SekaiFanmade, set `OPENSEKAI_COMMUNITY_URL` (e.g. `http://127.0.0.1:3000`) in the
  environment of the Editor or player. In an open Editor, `System.Environment.SetEnvironmentVariable` from a
  `unity command eval` works until the Editor restarts.

## Platforms and builds

OpenSekai ships on Windows x64, macOS universal, Linux x64, Android and iOS, the way OurTaikoPlay and MajdataPlay do:

- `Assets/OpenSekai/Editor/PlayerBuilds.cs` is the only build entry point. `ConfigurePlatforms()` sets the
  per-platform Player settings (Mono on desktop so one host can build all three, IL2CPP on mobile, Android
  ARM64 + ARMv7, landscape only) and reapplies the BASS plugin import settings. The `OpenSekai/Build/` menu
  queues targets and switches platforms between them; CI calls `Build<Platform>()` with `-executeMethod`.
  Each build packs that target's AssetBundles first (`OpenSekaiAssetBundleBuildPipeline.BuildForTarget`).
- `.github/workflows/build.yml` runs those methods through game-ci (manual runs and `v*` tags).
- `IosBuildPostprocess` writes the iOS Info.plist keys (Files app sharing, ProMotion, export compliance).
  `AudioBuildSettings` handles the BASS frameworks and the `__STATIC_LINKING__` define.
- Save user data under `OpenSekai.Platform.GamePaths.Root`, never `Application.persistentDataPath` directly.
  On desktop players it is `UserData` beside the executable (`<name>.app/UserData` on macOS), so a copied game folder keeps its data.
- Open and save user files with `Sekai.PlatformFileDialog` (StandaloneFileBrowser on desktop and in the
  Editor, NativeFilePicker on Android and iOS) instead of calling either plugin.
- The Editor compiles only the active target's `#if` branches. To check the others without switching
  platform, compile with `PlayerBuildInterface.CompilePlayerScripts` for each target; it works even when
  the target's build module is not installed.

## Scene and prefab saves

UI that a layout component drives (HorizontalLayoutGroup, ContentSizeFitter, AspectRatioFitter, Scrollbar) makes Unity
record the driven anchors, sizes and positions as prefab instance overrides whenever the layout re-runs, for example after
the Canvas changes size. They are saved as `value: 0` and recomputed on load. `Assets/OpenSekai/Editor/DrivenOverrideStripper.cs`
deletes them before each scene or Prefab Mode save, so a save only diffs what you changed. The exception is an
AspectRatioFitter, which re-drives at once and records its overrides again; Unity records those on every load anyway,
so they stay in the scene unchanged (the three on the manager's background `Image`). If a save still adds
RectTransform overrides, check whether those properties are driven before changing values in the prefab.

`ScreenManager` runs in edit mode but leaves the CanvasScaler's match alone there; it picks it from the screen aspect only
in play mode.

The project has the Pipeline package (`com.unity.pipeline`), so the Unity CLI can drive an open Editor
(`unity command eval --file <script.cs>`).
