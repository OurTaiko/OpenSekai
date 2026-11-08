using System.IO;
using CP;
using Sekai.MusicScoreMaker.Common;
using UnityEngine;

namespace Sekai.MusicScoreMaker.Ingame.Presenters
{
	/// <summary>
	/// Starts the chart editor scene. The editor screen is authored in MusicScoreMaker.unity; this takes the
	/// <see cref="MusicScoreMakerSceneArgs"/> the previous scene prepared and shows it. Opened directly in the Unity
	/// Editor it reopens the last edited custom chart, or returns to the custom chart manager.
	/// </summary>
	public class MusicScoreMakerEntryPoint : MenuSceneEntryPoint
	{
		public class MusicScoreMakerBootData
		{
			public ScreenLayerMusicScoreMaker.BootArg bootData { get; set; }
		}

		/// <summary>The editor state saved before a test play; read by the live and restored when it returns.</summary>
		public static MusicScoreMakerBootData BootData { get; set; }

		[SerializeField]
		private ScreenLayerMusicScoreMaker editorScreen;

		protected override void Awake()
		{
			LogUtility.WriteLog(1, "譜面ツールへ遷移しました", System.Array.Empty<object>());
			base.Awake();
			BootData ??= new MusicScoreMakerBootData();
		}

		protected override void OnSceneStart()
		{
			ScreenLayerMusicScoreMaker.BootArg bootArg = MusicScoreMakerSceneArgs.Take()?.BootArg;
			if (bootArg == null && Application.isEditor)
			{
				bootArg = CreateLastCustomChartBootArg();
			}

			if (bootArg?.FinishTransitionCallback == null)
			{
				LiveTransitioner.SafeForceFinish(null);
			}

			if (bootArg == null)
			{
				Debug.LogWarning("The chart editor was opened without a chart; returning to the custom chart manager.", this);
				SceneManager.Instance.RequestScene(SceneManager.Scene.CustomMusicScoreManager);
				return;
			}

			ShowSceneScreen(MenuScreenType.MusicScoreMaker, editorScreen, bootArg);
		}

		/// <summary>Editor convenience: reopens the custom chart that was last opened in the chart editor.</summary>
		private ScreenLayerMusicScoreMaker.BootArg CreateLastCustomChartBootArg()
		{
			string path = MusicScoreMakerSceneArgs.LastCustomMusicScorePath;
			if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
			{
				return null;
			}

			CustomMusicScoreEntry entry = CustomMusicScoreStorage.LoadEntry(path);
			if (entry == null)
			{
				Debug.LogWarning($"Could not load the last edited custom chart: {path}", this);
				return null;
			}

			Debug.Log($"Chart editor opened directly: reopening \"{entry.Manifest.scoreTitle}\".", this);
			return MusicScoreMakerSceneArgs.CreateBootArg(entry);
		}
	}
}
