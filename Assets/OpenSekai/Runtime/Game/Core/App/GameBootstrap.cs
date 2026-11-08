using OpenSekai.Audio;
using OpenSekai.SceneFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Sekai
{
	/// <summary>
	/// One-time application startup, run before the first scene loads (OurTaikoPlay's self-installing service pattern).
	/// Order: audio engine and scene switcher (engine services) -> asset bundles -> sound -> global SE settings -> input.
	/// Per-scene setup (frame rate, font atlas reset) stays in the scene entry points.
	/// Scene entry points call <see cref="Initialize"/> too; it runs only once per play session.
	/// </summary>
	public static class GameBootstrap
	{
		private const string EventSystemPrefabPath = "Common/Input/EventSystem";

		private static bool initialized;

		public static bool IsInitialized => initialized;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			// Keeps "Enter Play Mode Options" (domain reload disabled) working: start every session from scratch.
			initialized = false;
			SoundManager.Instance.ResetForNewSession();
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void OnBeforeSceneLoad()
		{
			Initialize();
		}

		public static void Initialize()
		{
			if (initialized)
			{
				return;
			}

			initialized = true;
			AudioEngine.EnsureInstance();
			SceneSwitcher.EnsureInstance();
			AssetBundleMetaManager.Instance.Initialize();
			AssetBundleManager.Instance.Initialize();
			SoundManager.Instance.Initialize();
			SoundUtility.SetupGlobalSeSettings(GlobalSeSettings.Normal);
			NativeInput.Initialize();
			EnsureEventSystem();
			Debug.Log("[Boot] OpenSekai services initialized");
		}

		private static void EnsureEventSystem()
		{
			if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null)
			{
				return;
			}

			GameObject prefab = Resources.Load<GameObject>(EventSystemPrefabPath);
			GameObject eventSystemObject = prefab != null
				? Object.Instantiate(prefab)
				: new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
			eventSystemObject.name = prefab != null ? prefab.name : "EventSystem";
			Object.DontDestroyOnLoad(eventSystemObject);
		}
	}
}
