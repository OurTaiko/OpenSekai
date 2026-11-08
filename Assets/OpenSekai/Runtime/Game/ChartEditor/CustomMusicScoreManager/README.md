# Custom Music Score Manager

Local-only custom music score manager. Its code is part of the `OpenSekai.Game` assembly.

- `Models`: manifest and custom score entry data types.
- `Services`: filesystem scanning, import/export, and entry loading.
- `UI`: the manager screen and its list cell.
- `CustomMusicScoreManagerEntryPoint`: starts `Assets/Scenes/CustomMusicScoreManager.unity`, the first scene.

The manager screen is `Assets/OpenSekai/Prefabs/CustomMusicScoreManager/ScreenLayerMusicScoreMakerTop.prefab`.
Its UI is authored in the prefab and placed in the scene; `ScreenLayerCustomMusicScoreManager` only binds the
serialized references and button events in `Awake`. List rows are clones of the inactive `EntryCellTemplate`.
It reuses the existing `MenuScreenType.MusicScoreMakerTop` entry so no new screen enum is needed.

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
