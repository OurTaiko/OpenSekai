using Cysharp.Threading.Tasks;
using OpenSekai.SceneFlow;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Sekai
{
	/// <summary>
	/// Game-level scene requests. Transitions run through <see cref="SceneSwitcher"/>: the cover closes, the current
	/// scene exits, memory is released, and the next scene loads asynchronously (this replaces the TransitionBlank scene).
	/// </summary>
	public class SceneManager : MonoBehaviour
	{
		public enum Scene
		{
			None = -1,
			Title = 0,
			OutGame = 1,
			VRMode = 2,
			TransitionBlank = 3,
			Core = 4,
			VirtualLiveLobby = 5,
			Splash = 6,
			SuperVirtualLiveLobby = 7,
			Mysekai = 8,
			MusicScoreMaker = 9,
			CustomMusicScoreManager = 10,
			// OpenSekai's own scenes.
			Home = 11,
			CommunityCharts = 12,
			CoopLive = 13,
			CoopLiveResult = 14
		}

		private const string CustomMusicScoreManagerSceneName = "Charts.Local";
		private const string MusicScoreMakerSceneName = "MusicScoreMaker";
		private const string CoreSceneName = "Core";
		private const string HomeSceneName = "Home";
		private const string CommunityChartsSceneName = "Charts.Online";
		private const string CoopLiveSceneName = "CoopLive";
		private const string CoopLiveResultSceneName = "CoopLive.Result";

		private static SceneManager instance;

		public event System.Action OnExitScene;

		private void Awake()
		{
			if (instance != null && instance != this)
			{
				Destroy(gameObject);
				return;
			}

			instance = this;
			DontDestroyOnLoad(gameObject);
			CurrentScene = GetActiveScene();
			PrevScene = CurrentScene;
			NextScene = CurrentScene;
		}

		public static SceneManager Instance
		{
			get
			{
				if (instance == null)
				{
					var go = GameObject.Find("SceneManager") ?? new GameObject("SceneManager");
					instance = go.GetComponent<SceneManager>() ?? go.AddComponent<SceneManager>();
				}

				return instance;
			}
		}

		public Scene CurrentScene { get; private set; } = Scene.MusicScoreMaker;
		public Scene PrevScene { get; private set; } = Scene.None;
		public Scene NextScene { get; private set; } = Scene.None;

		/// <summary>True while a requested scene switch is running.</summary>
		public bool IsSwitching => SceneSwitcher.Instance != null && SceneSwitcher.Instance.IsSwitching;

		public void RequestScene(Scene scene)
		{
			string sceneName = GetSceneName(scene);
			if (string.IsNullOrEmpty(sceneName))
			{
				Debug.LogWarningFormat("[Scene] No Unity scene is mapped to {0}", scene);
				return;
			}

			SceneSwitcher switcher = SceneSwitcher.EnsureInstance();
			if (switcher.IsSwitching)
			{
				Debug.LogWarningFormat("[Scene] Ignored request for {0}: a scene switch is already running", scene);
				return;
			}

			NextScene = scene;
			var options = new SceneSwitcher.SwitchOptions
			{
				// The live white-out already hides the screen while loading into Core.
				ShowCover = !LiveTransitioner.Exists,
				WhileCovered = () =>
				{
					ExitCurrentScene(scene);
					return UniTask.CompletedTask;
				}
			};
			switcher.SwitchScene(sceneName, options);
		}

		private void ExitCurrentScene(Scene nextScene)
		{
			if (nextScene != Scene.Core)
			{
				SoundManager.Instance.StopIngame();
			}

			if (ScreenManager.ExistsInstance && ScreenManager.Instance != null)
			{
				ScreenManager.Instance.ExitScene();
			}

			OnExitScene?.Invoke();
			PrevScene = CurrentScene;
			CurrentScene = nextScene;
		}

		private static string GetSceneName(Scene scene)
		{
			switch (scene)
			{
				case Scene.Core:
					return CoreSceneName;
				// OpenSekai's out-game (title, menus) starts at the Home navigation scene.
				case Scene.Title:
				case Scene.OutGame:
				case Scene.Home:
					return HomeSceneName;
				case Scene.CustomMusicScoreManager:
					return CustomMusicScoreManagerSceneName;
				case Scene.CommunityCharts:
					return CommunityChartsSceneName;
				case Scene.CoopLive:
					return CoopLiveSceneName;
				case Scene.CoopLiveResult:
					return CoopLiveResultSceneName;
				case Scene.MusicScoreMaker:
					return MusicScoreMakerSceneName;
				default:
					return string.Empty;
			}
		}

		/// <summary>Scenes that have a file, in lookup order (Title and OutGame also open Home).</summary>
		private static readonly Scene[] NamedScenes =
		{
			Scene.Home, Scene.Core, Scene.CustomMusicScoreManager, Scene.CommunityCharts, Scene.CoopLive, Scene.CoopLiveResult, Scene.MusicScoreMaker,
		};

		private static Scene GetActiveScene()
		{
			// Scene file names (Charts.Local, Charts.Online, ...) are not the enum names; look them up.
			string sceneName = UnitySceneManager.GetActiveScene().name;
			foreach (Scene scene in NamedScenes)
			{
				if (GetSceneName(scene) == sceneName)
				{
					return scene;
				}
			}

			return Scene.None;
		}
	}
}
