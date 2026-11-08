// Ported from OurTaikoPlay (https://github.com/OurTaiko/OurTaikoPlay, GPLv3), Assets/OurTaiko/Runtime/Scenes/SceneSwitcher.cs,
// which follows MajdataPlay's SceneSwitcher (https://github.com/LingFeng-bbben/MajdataPlay, GPLv3). See NOTICE.md.
//
// Changes from the original:
//  - Game-specific session state (song/course/dan/2P selection, curtain styles) removed.
//  - Task/Awaitable replaced by UniTask; the overlay is built in code instead of a Resources prefab.
//  - While covered, the old scene is unloaded into an empty runtime scene and unused assets are released before
//    the target loads (the job of OpenSekai's former TransitionBlank scene).
//  - The cover can be skipped when another overlay (e.g. the live white-out) already hides the screen.
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenSekai.SceneFlow
{
	/// <summary>
	/// Global scene switcher: closes a full-screen cover, runs work while covered, loads the next scene asynchronously
	/// and opens the cover again. Naming follows MajdataPlay: FadeIn closes the cover, FadeOut reveals the scene.
	/// </summary>
	public sealed class SceneSwitcher : MonoBehaviour
	{
		public sealed class SwitchOptions
		{
			public static readonly SwitchOptions Default = new SwitchOptions();

			/// <summary>Show the cover. Disable when another overlay already hides the screen.</summary>
			public bool ShowCover = true;

			/// <summary>Open the cover automatically once the new scene is ready.</summary>
			public bool AutoFadeOut = true;

			/// <summary>Unload the old scene and release unused assets before loading the next one.</summary>
			public bool ReleaseMemory = true;

			public Color CoverColor = Color.black;

			/// <summary>Runs once the cover is closed, before the old scene is unloaded.</summary>
			public Func<UniTask> WhileCovered;
		}

		private const int CoverSortingOrder = 32767;

		private CanvasGroup cover;
		private Image coverImage;
		private TMP_Text loadingText;
		private UniTask switchTask = UniTask.CompletedTask;
		private CancellationTokenSource transitionCancellation;

		[SerializeField]
		[Min(0.01f)]
		private float closeDuration = 0.35f;

		[SerializeField]
		[Min(0.01f)]
		private float openDuration = 0.4f;

		public static SceneSwitcher Instance { get; private set; }

		public static string CurrentScene { get; private set; } = string.Empty;

		public static string LastScene { get; private set; } = string.Empty;

		/// <summary>Raised after Unity activates a new scene: (new scene, previous scene).</summary>
		public static event Action<string, string> OnSceneChanged;

		/// <summary>Raised when a switch starts, with the target scene name.</summary>
		public event Action<string> SceneChanging;

		public bool IsSwitching { get; private set; }

		public bool IsCovered => cover != null && cover.alpha >= 0.999f;

		public bool IsInputBlocked => IsSwitching || IsCovered;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instance = null;
			CurrentScene = LastScene = string.Empty;
			OnSceneChanged = null;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Bootstrap()
		{
			EnsureInstance();
		}

		public static SceneSwitcher EnsureInstance()
		{
			if (Instance != null)
			{
				return Instance;
			}

			// Also handles entering Play mode with scene/domain reload disabled.
			var existing = FindAnyObjectByType<SceneSwitcher>();
			if (existing != null)
			{
				existing.Initialize();
				return Instance;
			}

			new GameObject(nameof(SceneSwitcher)).AddComponent<SceneSwitcher>();
			return Instance;
		}

		private void Awake()
		{
			Initialize();
		}

		private void Initialize()
		{
			if (Instance == this)
			{
				return;
			}

			if (Instance != null)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
			DontDestroyOnLoad(gameObject);
			BuildOverlay();
			SceneManager.activeSceneChanged -= OnUnitySceneChanged;
			SceneManager.activeSceneChanged += OnUnitySceneChanged;
			CurrentScene = SceneManager.GetActiveScene().name;
			SetTransitionState(false);
		}

		private void BuildOverlay()
		{
			var canvas = gameObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = CoverSortingOrder;
			var scaler = gameObject.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920, 1080);
			scaler.matchWidthOrHeight = 0.5f;
			gameObject.AddComponent<GraphicRaycaster>();

			var coverObject = new GameObject("Cover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
			coverObject.transform.SetParent(transform, false);
			Stretch((RectTransform)coverObject.transform);
			coverImage = coverObject.GetComponent<Image>();
			coverImage.color = Color.black;
			cover = coverObject.GetComponent<CanvasGroup>();

			var textObject = new GameObject("LoadingText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
			textObject.transform.SetParent(coverObject.transform, false);
			var textRect = (RectTransform)textObject.transform;
			textRect.anchorMin = new Vector2(1, 0);
			textRect.anchorMax = new Vector2(1, 0);
			textRect.pivot = new Vector2(1, 0);
			textRect.anchoredPosition = new Vector2(-48, 40);
			textRect.sizeDelta = new Vector2(1200, 80);
			var text = textObject.GetComponent<TextMeshProUGUI>();
			text.alignment = TextAlignmentOptions.BottomRight;
			text.fontSize = 36;
			text.raycastTarget = false;
			text.text = string.Empty;
			loadingText = text;
		}

		private static void Stretch(RectTransform rect)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}

		private void OnUnitySceneChanged(Scene previous, Scene next)
		{
			if (string.IsNullOrEmpty(next.name) || next.name == TransientSceneName)
			{
				return;
			}

			LastScene = CurrentScene;
			CurrentScene = next.name;
			EventSystem.current?.SetSelectedGameObject(null);
			OnSceneChanged?.Invoke(CurrentScene, LastScene);
		}

		/// <summary>Fire-and-forget switch; errors are logged.</summary>
		public void SwitchScene(string sceneName, SwitchOptions options = null)
		{
			SwitchSceneAsync(sceneName, options).Forget(LogError);
		}

		/// <summary>
		/// Switches to <paramref name="sceneName"/>. While a switch is running, further requests return the running switch.
		/// </summary>
		public UniTask SwitchSceneAsync(string sceneName, SwitchOptions options = null)
		{
			if (IsSwitching)
			{
				return switchTask;
			}

			if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
			{
				return UniTask.FromException(new ArgumentException("Scene is not enabled in Build Settings: " + sceneName, nameof(sceneName)));
			}

			IsSwitching = true;
			switchTask = SwitchSceneInternalAsync(sceneName, options ?? SwitchOptions.Default).Preserve();
			return switchTask;
		}

		/// <summary>Closes the cover, waits for <paramref name="taskToRun"/>, then switches.</summary>
		public UniTask SwitchSceneAfterTaskAsync(string sceneName, UniTask taskToRun, SwitchOptions options = null)
		{
			var source = options ?? SwitchOptions.Default;
			var previous = source.WhileCovered;
			return SwitchSceneAsync(sceneName, new SwitchOptions
			{
				ShowCover = source.ShowCover,
				AutoFadeOut = source.AutoFadeOut,
				ReleaseMemory = source.ReleaseMemory,
				CoverColor = source.CoverColor,
				WhileCovered = async () =>
				{
					await taskToRun;
					if (previous != null)
					{
						await previous();
					}
				}
			});
		}

		private const string TransientSceneName = "__SceneSwitcherTransient";

		private async UniTask SwitchSceneInternalAsync(string sceneName, SwitchOptions options)
		{
			var lifetime = this.GetCancellationTokenOnDestroy();
			var covered = false;
			try
			{
				EventSystem.current?.SetSelectedGameObject(null);
				SceneChanging?.Invoke(sceneName);
				SetLoadingText(string.Empty);
				coverImage.color = options.CoverColor;
				if (options.ShowCover)
				{
					await StartTransitionAsync(true);
					covered = true;
				}

				if (options.WhileCovered != null)
				{
					await options.WhileCovered();
				}

				lifetime.ThrowIfCancellationRequested();
				Debug.LogFormat("[Scene] {0} -> {1}", CurrentScene, sceneName);
				if (options.ReleaseMemory)
				{
					await ReleaseCurrentSceneAsync(lifetime);
				}

				var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
				if (operation == null)
				{
					throw new InvalidOperationException("Could not load scene: " + sceneName);
				}

				await operation.ToUniTask(cancellationToken: lifetime);

				// MajdataPlay waits for the target to initialize, then another 50 ms before opening.
				await UniTask.Yield(PlayerLoopTiming.Update, lifetime);
				await UniTask.Delay(50, DelayType.Realtime, cancellationToken: lifetime);
				if (covered && options.AutoFadeOut)
				{
					await StartTransitionAsync(false);
				}
			}
			catch
			{
				// A failed or cancelled switch must not strand the old scene behind the cover.
				if (covered && !lifetime.IsCancellationRequested)
				{
					await StartTransitionAsync(false);
				}

				throw;
			}
			finally
			{
				IsSwitching = false;
			}
		}

		/// <summary>Moves to an empty scene so the old scene's assets can be released before the next scene loads.</summary>
		private static async UniTask ReleaseCurrentSceneAsync(CancellationToken cancellation)
		{
			var transient = SceneManager.CreateScene(TransientSceneName);
			SceneManager.SetActiveScene(transient);
			for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
			{
				var scene = SceneManager.GetSceneAt(i);
				if (scene == transient || !scene.isLoaded)
				{
					continue;
				}

				var unload = SceneManager.UnloadSceneAsync(scene);
				if (unload != null)
				{
					await unload.ToUniTask(cancellationToken: cancellation);
				}
			}

			await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: cancellation);
			GC.Collect();
		}

		public void FadeIn()
		{
			FadeInAsync().Forget(LogError);
		}

		public void FadeOut()
		{
			FadeOutAsync().Forget(LogError);
		}

		/// <summary>Closes the cover.</summary>
		public async UniTask FadeInAsync()
		{
			if (IsSwitching)
			{
				await switchTask;
			}

			SetLoadingText(string.Empty);
			await StartTransitionAsync(true);
		}

		/// <summary>Opens the cover.</summary>
		public async UniTask FadeOutAsync()
		{
			if (IsSwitching)
			{
				await switchTask;
			}

			await StartTransitionAsync(false);
		}

		public void SetLoadingText(string text)
		{
			SetLoadingText(text, Color.white);
		}

		public void SetLoadingText(string text, Color color)
		{
			if (loadingText == null)
			{
				return;
			}

			loadingText.text = text ?? string.Empty;
			loadingText.color = color;
		}

		private UniTask StartTransitionAsync(bool closing)
		{
			transitionCancellation?.Cancel();
			transitionCancellation?.Dispose();
			transitionCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
			return AnimateTransitionAsync(closing, transitionCancellation.Token);
		}

		private async UniTask AnimateTransitionAsync(bool closing, CancellationToken cancellation)
		{
			cover.gameObject.SetActive(true);
			cover.blocksRaycasts = true;
			var from = cover.alpha;
			var to = closing ? 1f : 0f;
			var duration = closing ? closeDuration : openDuration;
			var started = Time.unscaledTimeAsDouble;
			while (!Mathf.Approximately(from, to) && Time.unscaledTimeAsDouble - started < duration)
			{
				var progress = Mathf.Clamp01((float)((Time.unscaledTimeAsDouble - started) / duration));
				// The same OutQuint curve as MajdataPlay.
				cover.alpha = Mathf.Lerp(from, to, 1 - Mathf.Pow(1 - progress, 5));
				await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
			}

			SetTransitionState(closing);
		}

		private void SetTransitionState(bool closed)
		{
			cover.alpha = closed ? 1 : 0;
			cover.interactable = false;
			cover.blocksRaycasts = closed;
			cover.gameObject.SetActive(closed);
		}

		private static void LogError(Exception exception)
		{
			if (exception is OperationCanceledException)
			{
				return;
			}

			Debug.LogException(exception);
		}

		private void OnDestroy()
		{
			if (Instance != this)
			{
				return;
			}

			SceneManager.activeSceneChanged -= OnUnitySceneChanged;
			transitionCancellation?.Cancel();
			transitionCancellation?.Dispose();
			Instance = null;
		}
	}
}
