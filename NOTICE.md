# Third-party notices

OpenSekai is distributed under the GNU General Public License v3.0 (see `LICENSE`).
It was relicensed from MIT to GPLv3 because it incorporates code from the GPLv3 projects below.

## MajdataPlay
- Source: https://github.com/LingFeng-bbben/MajdataPlay (TeamMajdata)
- License: GPLv3
- Reference revision: `33250cf75ba2d3256d48583797b31b45fcf9e987`
- Used for: BASS audio design (WASAPI native mixer pull, single mixer thread, output latency handling),
  SceneSwitcher design.

## OurTaikoPlay
- Source: https://github.com/OurTaiko/OurTaikoPlay (OurTaiko)
- License: GPLv3
- Reference revision: `25f3feb2f89b15f8a0a81742e96f8226906814ce`
- Used for: audio engine (`AudioEngine`, `AudioOptions`, `SoundSettings`, `NativeAudioSample`,
  `BackgroundAudioDecode`), song clock (`SongClock`, `GameTimeline`), `SceneSwitcher`, and the
  BASS build settings editor hook. Ported files carry a header naming their origin.

## ManagedBass
- Source: https://github.com/TeamMajdata/ManagedBass (fork of https://github.com/ManagedBass/ManagedBass)
- License: MIT (see `Assets/ThirdParty/ManagedBass/LICENSE.md`)
- Included as a git submodule at `Assets/ThirdParty/ManagedBass` (revision `5944aad3`).

## BASS audio library (un4seen developments)
- Source: https://www.un4seen.com/
- Native libraries in `Assets/Plugins/Un4seen.Bass/` (copied from OurTaikoPlay, originally from MajdataPlay
  revision `dc19722d`; see `provenance.json`).
- **BASS is free for non-commercial use only.** Commercial use requires a licence from un4seen.
  See `Assets/Plugins/Un4seen.Bass/bass.txt` and the other `*.txt` files there.
- BASS_AAC is additionally covered by `bass_aac-gpl.txt`.
