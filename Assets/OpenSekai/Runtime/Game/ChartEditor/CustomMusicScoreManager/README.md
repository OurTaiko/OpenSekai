# Custom Music Score Manager

Local-only custom music score manager. Its code is part of the `OpenSekai.Game` assembly.

- `Models`: manifest and custom score entry data types.
- `Services`: filesystem scanning, import/export, and entry loading.
- `UI`: the manager screen and its list cell.
- `CustomMusicScoreManagerEntryPoint`: starts `Assets/Scenes/CustomMusicScoreManager.unity`, the first scene.

The manager screen is `Assets/OpenSekai/Prefabs/CustomMusicScoreManager/ScreenLayerMusicScoreMakerTop.prefab`.
Its UI is authored in the prefab and placed in the scene; `ScreenLayerCustomMusicScoreManager` only binds the
serialized references and button events in `Awake`. List rows are clones of the inactive `EntryCellTemplate`.
Each part of the screen is its own prefab, nested in the screen prefab: `CustomMusicScoreHeader`,
`CustomMusicScoreDifficultyRail`, `CustomMusicScoreListArea` (holding `CustomMusicScoreEntryCell` as the template),
`CustomMusicScoreDetailWindow`, `CustomMusicScoreActionButtons` and `CustomMusicScoreCreateMenuOverlay`. Edit a part in
its own prefab. In the detail window only `EmptyState` is active in the prefab; the screen turns on the overview or the
form at run time.
It reuses the existing `MenuScreenType.MusicScoreMakerTop` entry so no new screen enum is needed.

The screen follows the official "CREATE SCORE" style: a header with the watermark, the `新建谱面` button
(opens a two-card create menu: blank chart or ZIP/folder import), refresh and settings; a difficulty
filter rail (local filtering only); the chart list with difficulty badges and jackets; and a window-style
detail panel whose `编辑信息` toggle swaps the overview for the manifest form. `CustomMusicScoreDifficultyBadge`
holds the difficulty colours. Its sprites come from the official atlases (`Art/Sprites/txt_create_score_outline`,
`logo_score_maker_wh`, `icon_score_create_*`, `jacket_placeholder_disc`) plus generated round shapes in
`Art/Textures/CustomMusicScoreManager` (the canvases use 1 pixel per unit, so sliced sprites are 1 PPU).

The settings button opens the shared option dialog (`UI/Option/OptionDialog`, `DialogType.OptionDialog`), a copy of the
official オプション dialog. Every change is saved and applied at once. Its structure is described in the repository's
`AGENTS.md`, which also lists the settings that are stored but not used yet.

Scene flow (all through `SceneSwitcher`):

- Edit: `MusicScoreMakerSceneArgs.Prepare(...)`, then the `MusicScoreMaker` scene.
- Play / Auto: `CoreSceneArgs.Prepare(...)`, then the `Core` scene; the live returns here when it ends.
- Opening `MusicScoreMaker` or `Core` directly in the Unity Editor reopens the last custom chart.

Local score entries are stored under:

`Application.persistentDataPath/CustomMusicScores/<title>_<shortId>/`

Normal non-auto play results are appended to:

`Application.persistentDataPath/CustomMusicScores/PlayHistory.jsonl`

Expected entry files:

- `manifest.json`
- `score.json`
- audio file named by `manifest.audioFileName`
- jacket image named by `manifest.jacketFileName`
- optional 2DMV mp4 named by `manifest.videoFileName`

The manager's Score selector accepts `score.json` directly or an official SUS `.txt` / `.sus`;
SUS files are converted to the local `score.json` format on import.

`manifest.title` is the song title shown in live MusicInfo. `manifest.scoreTitle`
is the custom score title shown in the score-info area.
`manifest.collaborationLabel` controls the MusicInfo collaboration ribbon; leave it
empty to hide the ribbon.
