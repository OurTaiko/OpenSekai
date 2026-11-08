# OpenSekai

OpenSekai 是一个用于学习和研究目的的 Project Sekai 音乐游戏玩法复刻项目，目前提供可用的**谱面编辑器**、**第三方歌曲包支持**，以及**从编辑器进入 live 测试游玩**的完整流程。

## 开发环境

- Unity: 2022.3.62f2
- 渲染管线: URP 14
- 主要依赖: TextMesh Pro、UniTask、MessagePack、uPalette、UI Particle、SoftMaskForUGUI、ManagedBass（BASS 音频，git submodule，克隆后需执行 `git submodule update --init`）

## 目录说明

- `Assets/OpenSekai/Game`：主要游戏逻辑（程序集 `OpenSekai.Game`），`Sekai/` 下按功能分目录：`App`（启动与场景请求）、`Screen`、`UI`、`Live`、`MusicScoreMaker`、`Sound`、`AssetLoading`、`Network`、`Data`、`Domain`、`Rendering` 等。
- `Assets/OpenSekai/Runtime`：独立运行时模块——`Audio`（BASS 音频引擎，`OpenSekai.Audio`）、`SceneFlow`（`SceneSwitcher` 场景切换）、`AcbDecoder`（ACB/HCA 解码）。
- `Assets/OpenSekai/Editor`：编辑器工具（`OpenSekai.Editor`），包括本地 AssetBundle 构建流程 `OpenSekaiAssetBundleBuildPipeline.cs`。
- `Assets/ThirdParty`：第三方源码（ManagedBass 子模块、ZString、StandaloneFileBrowser、UniSafeAreaAdjuster、UiEffect），各自独立程序集。
- `Assets/Resources`：运行时通过 `Resources` 加载的 Prefab、文本、字体、特效和界面数据。
- `Assets/Sekai/assetbundle/resources`：原样放置的 AssetBundle 源资源，资源本身应保留 AssetBundle 名称，之后通过构建流程打包。
- `Assets/StreamingAssets`：打包后的 AssetBundle 与本地运行数据输出位置，主要用于构建后的运行环境。

## 资源说明

本仓库中的代码以 GPLv3 协议发布（见 `LICENSE`）。项目包含移植自 MajdataPlay 与 OurTaikoPlay（均为 GPLv3）的代码，因此由 MIT 改为 GPLv3；详细来源见 `NOTICE.md`。音频使用的 BASS 库仅限非商业使用。第三方库、字体、音频、贴图、prefab、shader 参考资源以及任何可能来源于原作或其他权利方的资源，不自动包含在本项目授权范围内，请分别遵循其原始许可和权利归属。

本项目与任何原作游戏、发行方或权利方没有官方关联。
