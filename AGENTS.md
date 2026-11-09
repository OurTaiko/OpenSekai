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

The first scene is `Home` (navigation: 在线谱面, 自制谱管理, and ☰ for the option dialog). The
community chart browser is the `Charts.Online` scene (`SceneManager.Scene.CommunityCharts`); its charts come from SekaiFanmade
(https://sekai.ourtaiko.org, separate repository `~/Repos/SekaiFanmade`, whose README lists the API).

- Code is in `Assets/OpenSekai/Runtime/Game/Community/`: `CommunityClient` (site API), `CommunityAccount` (sign-in),
  `CommunityLibrary` (download, import, `CustomMusicScores/CommunityInstalls.json`, play reports),
  `CommunityImageCache`, and `UI/` (`ScreenLayerOpenSekaiHome`, `ScreenLayerCommunityCharts`, `CommunityLoginPanel`
  and the two scene entry points). Screen types `OpenSekaiHome` (9000) and `CommunityCharts` (9001).
- Prefabs are in `Assets/OpenSekai/Prefabs/Community/`. They were made from the manager screen's parts: in
  `ScreenLayerCommunityCharts` the `DifficultyRail` and `ListArea` are still nested instances of the manager's part
  prefabs (so the rail and list cells look the same in both), the rest is its own. The list rows are
  `CustomMusicScoreEntryCell`. `CommunityLoginPanel` is nested in `ScreenLayerCommunityCharts`.
- Signing in lives in the option dialog: Additionals > アカウント has an OurTaiko 账号 button (the nickname when signed
  in) that opens `CommunityLoginPanel` over the dialog (`OptionDialog.loginPanelPrefab`). Home and `Charts.Online` have
  no account button, only the manager's ☰; the charts screen's nested panel still pops up when voting or マイリスト needs
  a sign-in.
- Sign-in works like OurTaikoPlay: the password goes only to OurTaiko SSO (`/api/v1/game/account/login`); the
  account token is kept in `community_account.dat` under `GamePaths.Root`, encrypted with a key of this device, and
  traded for a game session of the site (`/api/v1/game/tokens`, service from `/api/v1/meta`), sent as a Bearer token.
  The SSO Application needs 允许游戏登录和会话接口 and 允许游戏账号换取本应用的游戏会话 turned on.
- Downloads are imported with `CustomMusicScoreManagerService.ImportZip`, so they also show up in the manager.
  Lives started from either screen go through `CustomMusicScorePlayLauncher`; `ReturnScreenType` decides whether the
  live goes back to the manager (`Charts.Local`) or to `Charts.Online`. A finished, non-auto live of a downloaded chart reports one
  play to the site (`CustomMusicScorePlayHistoryStorage` calls `CommunityLibrary.OnLocalChartPlayed`).
- The site keeps no chart ZIPs. A single-chart read (`CommunityClient.GetAsync`) carries `Package`: the manifest and
  the files it names, each with a URL on the CDN, size and SHA-256. `CommunityLibrary.DownloadAsync` writes
  manifest.json, downloads and checks each file into a folder under `GamePaths.Root/CommunityDownloads`, imports it with
  `CustomMusicScoreManagerService.ImportFolder`, deletes the temporary folder and counts the download
  (`POST /api/v1/charts/<id>/downloads`).
- Votes work like the site (Reddit style): ▲ / ▼ in the chart window send `PUT /api/v1/charts/<id>/vote` with 1, -1, or
  0 to take the vote back; `Score` is upvotes minus downvotes. おすすめ and 検索 > 人気 use `CommunitySort.Hot` (Reddit's
  hot ranking), ランキング 総合 uses `Top` (score) and デイリー uses `Daily` (plays in the last 24 hours).
- 自制谱管理 › 上传 sends the selected chart to the site: `CustomMusicScoreManagerService.ExportZip` into
  `GamePaths.Root/CommunityUploads` (deleted afterwards), then `CommunityClient.UploadChartAsync` (`PUT /api/v1/uploads`
  with the game session). The local folder is then linked in `CommunityInstalls.json` (`CommunityLibrary.RememberUpload`),
  so a later 上传 of a chart linked to the player's own site chart replaces it (`?chart=<id>`); a chart downloaded from
  someone else goes up as a new chart. The button is an added object in `ScreenLayerMusicScoreMakerTop.prefab`, like
  导出SUS.
- A chart's 谱师署名 is `noteDesigner` in manifest.json: the chart manager's 谱师署名 field, the 谱师 shown in the manager and
  the live, and SUS `#DESIGNER`. A new chart takes the signed-in nickname, otherwise the system user name. The site keeps it
  in `charts.note_designer`, and its chart management page can change it. The old `userName` field is no longer read.
- Chart ids are 32 random hex digits (`CommunityChart.Id`); `NumberId` is the short number 譜面ID検索 takes. Install
  records written when ids were numbers hold the number and are re-keyed to the id on first use.
- The signed-in account remembers which community server it belongs to: pointed at another server (a local
  SekaiFanmade) the game shows itself signed out and leaves `community_account.dat` alone.
- `CommunityImageCache` loads jackets and avatars through `ImageDecoder`: PNG and JPEG with `Texture2D.LoadImage`, WebP (the
  SSO's avatars) with libwebp from the UPM package `com.netpyoung.webp` (unity.webp, git tag 0.3.22; native libraries for all five
  platforms, iOS linked statically through `__Internal`). An animated WebP shows its first frame. `OpenSekai.Game` allows unsafe
  code for the libwebp calls. The site stores jackets as JPEG; a chart's own jacket file must still be PNG or JPEG.
- To point the game at a local SekaiFanmade, set `OPENSEKAI_COMMUNITY_URL` (e.g. `http://127.0.0.1:3000`) in the
  environment of the Editor or player. In an open Editor, `System.Environment.SetEnvironmentVariable` from a
  `unity command eval` works until the Editor restarts.

## Co-op live (協力ライブ)

Online co-op lives with up to 5 players, like the original's マルチライブ, over gRPC to SekaiFanmade's co-op server (protocol
`proto/sekai/multiplay/v1/multiplay.proto` in `~/Repos/SekaiFanmade`; its README has the room rules, timers, FEVER and result
rules). Home's 協力ライブ card opens the `CoopLive` scene (`SceneManager.Scene.CoopLive` 13, screen type `CoopLive` 9002,
screen data `Resources/screen/data/ScreenLayerCoopLive.asset` in `EntryScreenLayers`).

- Code is in `Assets/OpenSekai/Runtime/Game/CoopLive/`:
  - `Proto/Multiplay.cs` is generated, never edit it. After changing the proto in SekaiFanmade:
    `protoc --proto_path=proto --csharp_out=<OpenSekai>/Assets/OpenSekai/Runtime/Game/CoopLive/Proto proto/sekai/multiplay/v1/multiplay.proto`
    (protoc 36.x for Google.Protobuf 3.36.x). There is no generated client: `CoopTransport` declares the one duplex method by hand.
  - `CoopSession` is the connection. It is static and outlives scenes (room screen → Core → room screen), queues writes one at a
    time, raises its events on the main thread, pings every 15 s and, if the stream drops, joins the same room again (the
    server keeps the place 10 s).
  - `CoopRoundLauncher` handles LOADING: it downloads the chart the server assigned (`CommunityLibrary`) and starts the live through
    `CustomMusicScorePlayLauncher.PlayAsync(..., MenuScreenType.CoopLive, ..., PlayMode.MultiLive)`.
  - `CoopLiveBridge` is added by the Core `EntryPoint` for `PlayMode.MultiLive`. It sets `SoloLiveController.IsCoop` (no pause,
    retire or retry; life 0 keeps playing), `StartGate` (after the music is ready it sends `loaded` and waits for `start_live`, via
    `BaseLiveController.WaitBeforeMusicStart`) and `ScoreLogicFactory` (`CoopScoreLogic`, 1.5× during FEVER). It streams the
    score, draws the other players and the FEVER banner on its own overlay canvas, reports FEVER CHANCE and sends the final score
    from `SoloLiveController.LiveEnded`. A finished live goes to the `Result` scene (see "Result scene" below). A live left before
    it started (`AbortToMenu`) goes straight back to the room. `CoopSession.LastResult` keeps the result if the connection drops;
    the room's 上一局结果 opens the Result scene again through `CoopResultSceneArgs`.
  - `UI/ScreenLayerCoopLive` builds the room screen in code (it changes with every phase), with `CoopUi` helpers (`StampBar`,
    `Toast` and the builders are shared with the result screen); the two sprites of each screen are serialized on the scene object.
    Stamp texts are in `CoopStamps`.
- gRPC: NuGet `Grpc.Net.Client` 2.85 and `Google.Protobuf` 3.36 (`Assets/packages.config`). Unity's HttpClient has no HTTP/2, so
  the transport is **YetAnotherHttpHandler** (UPM `com.cysharp.yetanotherhttphandler`, native, all five platforms). Its code is in
  the separate assembly `Runtime/CoopLiveHttp2` (define constraint `OPENSEKAI_YAHH`, set by a version define when the package is
  present), which registers `CoopTransport.HandlerFactory`. Without the package everything compiles, and the co-op screen says
  HTTP/2 is missing. `link.xml` files keep IL2CPP from stripping gRPC and protobuf.
- The server address comes from `/api/v1/meta` (`multiplay.url`); `OPENSEKAI_MULTIPLAY_URL` overrides it (e.g.
  `http://127.0.0.1:50051`, plain h2c).

## Result scene

Every live started from 自制谱管理, 在线谱面 or a 協力ライブ room ends in the `Result` scene (`SceneManager.Scene.LiveResult` 14,
screen type `LiveResult` 9003, `Assets/OpenSekai/Runtime/Game/Result/`). It copies the original's two result pages under one
header (jacket, difficulty, the C/B/A/S gauge, SCORERANK), built in code by `ScreenLayerLiveResult`:

- Members (co-op only): one card per player, all the same size (no larger MVP card), in a row that scrolls sideways. Until
  everyone has finished it shows the scores as they come in; then MVP / SUPER STAR, TEAM SCORE and FEVER. The header gauge is the
  team's (thresholds times the number of players), one segment per player in their slot color. メンバーの成績 opens a table of
  everyone's judgements; スタンプ work here. There are no フレンド申請 buttons and no character art: the card shows the SSO avatar
  (`Member.avatar_url`, usually WebP), or the slot color with the nickname's first letter.
- Detail: this player's score, ハイスコア (NEW RECORD when beaten), judgements, COMBO and LATE / FAST / FLICK. The original's
  character is replaced by the jacket and how the live ended. There is no 達成報酬 button.

次へ on the members page goes to the detail page; on the detail page it goes back to the chart list after a solo live
(`LiveResultData.ReturnScreenType`) and, after a co-op live, asks whether to go back to the room (回到房间) or leave it (退出房间 →
Home). A co-op round starting meanwhile takes the player back to the room by itself.

`SoloLiveController.OnExit` fills `LiveResultData.Last` (score, chart, the best score read before this live's record is written)
for a finished or failed live and for every co-op live that started; a retired live (pause → リタイア) and editor test plays go
back without a result. Score ranks and the gauge use `ScoreGaugeCalculator`'s rates of 1,000,000 (`LiveResultData.RankOf`, `GaugeOf`).

## Scene names

Scene files are named by area, not by class: `Home`, `Charts.Local` (自制谱管理, `SceneManager.Scene.CustomMusicScoreManager`),
`Charts.Online` (在线谱面, `Scene.CommunityCharts`), `CoopLive`, `Result` (`Scene.LiveResult`), `MusicScoreMaker`, `Core`. The enum keeps
its old member names; `SceneManager.GetSceneName` maps each to its file, and `GetActiveScene` looks the active scene up through the
same table (a dotted name is not an enum name). A new scene needs a case there, an entry in `NamedScenes`, and a build-settings entry.

## Platforms and builds

OpenSekai ships on Windows x64, macOS universal, Linux x64, Android and iOS, the way OurTaikoPlay and MajdataPlay do:

- `Assets/OpenSekai/Editor/PlayerBuilds.cs` is the only build entry point. `ConfigurePlatforms()` sets the
  per-platform Player settings (Mono on desktop so one host can build all three, IL2CPP on mobile, Android
  ARM64 + ARMv7, landscape only) and reapplies the BASS plugin import settings. The `OpenSekai/Build/` menu
  queues targets and switches platforms between them; CI calls `Build<Platform>()` with `-executeMethod`.
  Each build packs that target's AssetBundles first (`OpenSekaiAssetBundleBuildPipeline.BuildForTarget`).
- `.github/workflows/build.yml` runs those methods through game-ci (manual runs and `v*` tags).
- `PlayerBuilds` holds the names: package `com.sparkbarry.sekai`, product `OpenSekai`, company `SparkBarry`
  (`ConfigurePlatforms()` sets them). iOS builds are signed automatically by team `253AX6B3P2`, the same as OurTaikoPlay. The Editor's `persistentDataPath`, and so its `GamePaths.Root`, is named after
  the company and product.
- `IosBuildPostprocess` writes the iOS Info.plist keys (Files app sharing, ProMotion, export compliance) and, like
  OurTaikoPlay's `IosProjectBranding`, renames the Xcode export from `Unity-iPhone` to `OpenSekai`: the project, app
  target, scheme, product (`OpenSekai.app`) and the `Unity-iPhone` / `Unity-iPhone Tests` folders. UnityFramework keeps
  its name. Before exporting into a folder that already holds a renamed export, `PrepareForExport` names it back
  (Unity's Append expects its own names) and the build appends; this runs in `PlayerBuilds`, the Build window handler
  and a preprocess callback. `AudioBuildSettings` handles the BASS frameworks and the `__STATIC_LINKING__` define.
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
