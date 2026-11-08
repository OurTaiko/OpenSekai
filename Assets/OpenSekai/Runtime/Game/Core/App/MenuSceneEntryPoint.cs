using CP;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sekai
{
	/// <summary>
	/// Shared start-up for the uGUI menu scenes (custom chart manager, chart editor): volume, menu sounds, the
	/// ScreenManager base camera and the overlay screens. Subclasses push the screen authored in their scene.
	/// </summary>
	public abstract class MenuSceneEntryPoint : SceneEntryPoint
	{
		private const string MenuCommonSoundBundle = "sound/menu/menu_common";

		public ApplicationLocalSettings LocalSettings { get; private set; }

		protected override void Awake()
		{
			base.Awake();
			GameBootstrap.Initialize();
			TextMeshProUtility.SetupBuiltinFontAsset();
			FramerateUtility.SetFrameRate();
			GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
		}

		protected override void Start()
		{
			LocalSettings = ApplicationLocalSettings.LoadFromStorage();
			if (LocalSettings.LiveVolume == null)
			{
				LocalSettings.LiveVolume = LocalSettings.SetupLiveVolume();
			}

			SoundManager.Instance.SetupVolume(
				1f,
				LocalSettings.SystemVolume.Bgm,
				LocalSettings.SystemVolume.Se,
				LocalSettings.SystemVolume.Voice);

			if (!SoundManager.Instance.IsLoadedSoundBundle(MenuCommonSoundBundle))
			{
				SoundManager.Instance.LoadSoundBundle(MenuCommonSoundBundle, true);
			}

			ScreenManager.Instance.CreateEmptyBaseCamera();

			// Scene transitions are covered by the global SceneSwitcher, which opens once this scene is ready.
			ScreenManager.Instance.FadeIn(0f, 0f, null);

			ScreenManager.Instance.AddScreen(MenuScreenType.Header);
			ScreenManager.Instance.AddScreen(MenuScreenType.TouchEffect);
			ScreenManager.Instance.AddScreen(MenuScreenType.InsertNoti);

			OnSceneStart();
		}

		/// <summary>Shows this scene's main screen. Runs after every Awake in the scene.</summary>
		protected abstract void OnSceneStart();

		/// <summary>Shows a screen layer that is authored in this scene.</summary>
		protected static void ShowSceneScreen(MenuScreenType screenType, ScreenLayer screen, ScreenLayer.BootArgBase bootArg = null)
		{
			if (screen == null)
			{
				Debug.LogError($"The {screenType} screen is not assigned on this scene's entry point.");
			}

			ScreenManager.Instance.RegisterSceneLayer(screenType, screen);
			if (bootArg == null)
			{
				ScreenManager.Instance.PushUIScreen(screenType, false);
			}
			else
			{
				ScreenManager.Instance.PushUIScreen(screenType, bootArg, false);
			}
		}

		public void OnApplicationPause(bool pause)
		{
			if (pause)
			{
				SoundManager.Instance.Pause();
			}
			else
			{
				SoundManager.Instance.Resume();
			}
		}
	}
}
