# Third-party notices

OpenSekai is distributed under the GNU General Public License v3.0 (see `LICENSE`).
It was relicensed from MIT to GPLv3 because it incorporates code from the GPLv3 projects below.

## MajdataPlay
- Source: https://github.com/LingFeng-bbben/MajdataPlay (TeamMajdata)
- License: GPLv3
- Reference revision: `33250cf75ba2d3256d48583797b31b45fcf9e987`
- Used for: BASS audio design (WASAPI native mixer pull, single mixer thread, output latency handling),
  SceneSwitcher design, the portable data root (`MajEnv.RootPath`, adapted in `GamePaths`) and the iOS
  Info.plist file-sharing keys (`iOSPostprocessBuild`, adapted in `IosBuildPostprocess`).

## OurTaikoPlay
- Source: https://github.com/OurTaiko/OurTaikoPlay (OurTaiko)
- License: GPLv3
- Reference revision: `25f3feb2f89b15f8a0a81742e96f8226906814ce`
- Used for: audio engine (`AudioEngine`, `AudioOptions`, `SoundSettings`, `NativeAudioSample`,
  `BackgroundAudioDecode`), song clock (`SongClock`, `GameTimeline`), `SceneSwitcher`, the
  BASS build settings editor hook, the multi-platform build script (`PlayerBuilds`), its GitHub Actions
  workflow, `GamePaths`, the iOS Info.plist postprocess and Xcode project renaming (from `IosProjectBranding`) and
  the app icon setup (`PlayerBranding`). Ported files carry a header naming their origin.
- Also used for: the OurTaiko account store and account-center calls (`Runtime/Online/OurTaikoAccounts.cs`
  at `5a077a7`, adapted in `Community/CommunityAccount.cs`).

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

## Ojsk Community
- Source: https://github.com/kamcdev/OpenSekai_Community (Ojsk Community contributors)
- License: MIT (full text below)
- Reference revision: `0cd2e626` (`community/master`)
- Used for: opening the pause menu when the app goes to the background during a live (`52b30a8`),
  the confirmation dialog before clearing the chart in the editor (`d191614`), and showing
  hi-speed values with more than one decimal (`dfabfcf`).

```
MIT License

Copyright (c) 2026 Ojsk Community contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
