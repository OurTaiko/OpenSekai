# OpenSekai

OpenSekai 是一个用于学习和研究目的的 Project Sekai 音乐游戏玩法复刻项目，目前提供可用的**谱面编辑器**、**第三方歌曲包支持**，以及**从编辑器进入 live 测试游玩**的完整流程。

## 开发环境

- Unity: 6000.6.8f1
- 渲染管线: URP 17（RenderGraph）
- 主要依赖: TextMesh Pro、UniTask、MessagePack、uPalette、UI Particle、SoftMaskForUGUI、ManagedBass（BASS 音频，git submodule，克隆后需执行 `git submodule update --init`）

## 目录说明

目录结构参考 OurTaikoPlay：项目自己的内容都在 `Assets/OpenSekai/` 下，场景和渲染设置放在 `Assets/Scenes`、`Assets/Settings`。

```
Assets/
├── OpenSekai/
│   ├── Runtime/                 # 脚本
│   │   ├── Audio/               # BASS 音频引擎（OpenSekai.Audio）
│   │   ├── SceneFlow/           # SceneSwitcher 场景切换（OpenSekai.SceneFlow）
│   │   ├── AcbDecoder/          # ACB/HCA 解码（OpenSekai.AcbDecoder）
│   │   └── Game/                # 游戏逻辑（OpenSekai.Game）
│   │       ├── Core/            #   启动(App)、资源加载、通用工具、CP、兼容 shim
│   │       ├── Audio/           #   SoundManager、CueSheet
│   │       ├── Play/            #   live 玩法：CoreScene（Core 场景控制器）、Live、SUS、多人
│   │       ├── ChartEditor/     #   谱面编辑器 MusicScoreMaker、自制谱管理 CustomMusicScoreManager
│   │       ├── UI/              #   UI 组件、Screen 层、对话框、调色板、模糊
│   │       ├── Data/            #   主数据（Master）、用户数据（User）、枚举（Domain）
│   │       ├── Online/          #   API / 网络层
│   │       ├── Rendering/       #   URP RenderGraph 渲染 Pass
│   │       └── Legacy/          #   尚未接通流程的外围功能（Mysekai、商店、称号等）
│   ├── Editor/                  # 编辑器工具（OpenSekai.Editor），含 AssetBundle 构建流程
│   ├── Art/                     # 动画、材质、模型、shader、精灵、图集、贴图、图标
│   ├── Prefabs/                 # 通用 prefab；Live/ 为 Core 场景里摆放的 live 视图和暂停界面；CustomMusicScoreManager/Resources 为自制谱管理界面
│   ├── Resources/               # 运行时通过 Resources 加载的 prefab、字体、特效、界面数据
│   └── AssetBundles/            # AssetBundle 源资源（保留 AssetBundle 名称，由构建流程打包）
├── Scenes/                      # MusicScoreMaker（首场景）、Core（live，视图直接摆在场景里；在编辑器里直接运行会自动游玩上次的自制谱）
├── Settings/                    # URP 管线资源、Renderer、Volume Profile
├── ThirdParty/                  # 第三方源码（ManagedBass 子模块、ZString、StandaloneFileBrowser 等）
├── Plugins/                     # 原生库（BASS）与 DOTween
├── StreamingAssets/             # 打包后的 AssetBundle 与本地运行数据
├── TextMesh Pro/                # TMP 资源（着色器已更新到 Unity 6 版本）
└── Resources/DOTweenSettings.asset  # DOTween 固定读取此路径，勿移动
```

## 资源说明

本仓库中的代码以 GPLv3 协议发布（见 `LICENSE`）。项目包含移植自 MajdataPlay 与 OurTaikoPlay（均为 GPLv3）的代码，因此由 MIT 改为 GPLv3；详细来源见 `NOTICE.md`。音频使用的 BASS 库仅限非商业使用。第三方库、字体、音频、贴图、prefab、shader 参考资源以及任何可能来源于原作或其他权利方的资源，不自动包含在本项目授权范围内，请分别遵循其原始许可和权利归属。

本项目与任何原作游戏、发行方或权利方没有官方关联。
