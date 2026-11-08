# Custom Music Score Manager

Local-only custom music score manager. Its code is part of the `OpenSekai.Game` assembly.

- `Models`: manifest and custom score entry data types.
- `Services`: filesystem scanning, import/export, and entry loading.
- `UI`: UGUI screens and widgets.
- `Assets/OpenSekai/Prefabs/CustomMusicScoreManager/Resources/Screen/Prefabs`: screen-layer prefabs loaded by
  Sekai's `ScreenLayerData` through Unity `Resources`.

The first local manager screen is implemented as `ScreenLayerMusicScoreMakerTop.prefab`.
It intentionally reuses the existing `MenuScreenType.MusicScoreMakerTop` entry so we do not
need to add another global screen enum or modify `EntryScreenLayers.asset`.

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
