using System.IO;
using Cysharp.Threading.Tasks;
using Sekai.Core.Live;
using Sekai.CustomMusicScoreManager;
using Sekai.MusicScoreMaker.Common;
using Sekai.MusicScoreMaker.Ingame.Models;
using UnityEngine;

namespace Sekai.Core
{
	/// <summary>
	/// Starts the Core scene. The scene is authored with its views and controllers in place (as in OurTaikoPlay's
	/// play scene); this takes the <see cref="CoreSceneArgs"/> the previous scene prepared and boots the matching
	/// controller. Without arguments (Core opened directly in the Editor) it replays the last custom chart in auto
	/// mode, or returns to the custom chart manager.
	/// </summary>
	public class EntryPoint : MonoBehaviour
	{
		[SerializeField]
		private SoloLiveController soloLiveController;

		// Start runs after every Awake in the scene, so the views are initialised before the controller sets them up.
		private void Start()
		{
			BootAsync().Forget();
		}

		private async UniTaskVoid BootAsync()
		{
			await UniTask.WaitUntil(
				() => ShaderPreloader.Status != ShaderPreloader.PreloadStatus.Loading
					&& ShaderPreloader.Status != ShaderPreloader.PreloadStatus.Compiling,
				PlayerLoopTiming.Update,
				this.GetCancellationTokenOnDestroy());

			ShaderPreloader.Status = ShaderPreloader.PreloadStatus.None;
			SceneTransitionLoadingIndicator loadingIndicator = FindAnyObjectByType<SceneTransitionLoadingIndicator>();
			if (loadingIndicator != null)
			{
				Destroy(loadingIndicator.gameObject);
			}

			CoreSceneArgs args = CoreSceneArgs.Take();
			if (args == null && Application.isEditor)
			{
				args = await CreateLastCustomChartArgsAsync();
			}
			if (this == null)
			{
				return;
			}

			if (args == null)
			{
				Debug.LogWarning("Core was opened without a live to play; returning to the custom chart manager.", this);
				SceneManager.Instance.RequestScene(SceneManager.Scene.CustomMusicScoreManager);
				return;
			}

			switch (args.PlayMode)
			{
				case PlayMode.SoloLive:
					soloLiveController.Boot(args.BootData);
					break;
				default:
					Debug.LogWarning($"Unsupported Core PlayMode: {args.PlayMode}; returning to the custom chart manager.", this);
					SceneManager.Instance.RequestScene(SceneManager.Scene.CustomMusicScoreManager);
					break;
			}
		}

		/// <summary>Editor convenience: auto-plays the custom chart that was last sent to Core.</summary>
		private async UniTask<CoreSceneArgs> CreateLastCustomChartArgsAsync()
		{
			string path = CoreSceneArgs.LastCustomMusicScorePath;
			if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
			{
				return null;
			}

			CustomMusicScoreEntry entry = CustomMusicScoreStorage.LoadEntry(path);
			MusicScoreMakerData scoreData = entry?.LoadScore();
			if (scoreData == null || !await entry.RegisterAudioAsync(this.GetCancellationTokenOnDestroy()))
			{
				Debug.LogWarning($"Could not load the last custom chart for a direct Core run: {path}", this);
				return null;
			}

			FreeLiveBootData bootData = ScreenLayerCustomMusicScoreManager.CreateDirectPlayBootData(entry, scoreData, isAuto: true);
			if (bootData == null)
			{
				return null;
			}

			Debug.Log($"Core opened directly: auto-playing the last custom chart \"{entry.Manifest.scoreTitle}\".", this);
			return new CoreSceneArgs(PlayMode.SoloLive, bootData);
		}
	}
}
