using OpenSekai.SceneFlow;
using Sekai.MusicScoreMaker.Common;

namespace Sekai.MusicScoreMaker.Ingame.Presenters
{
	/// <summary>
	/// What the chart editor scene should open. Set by the caller before switching to the MusicScoreMaker scene and
	/// taken by <see cref="MusicScoreMakerEntryPoint"/> (same hand-off as <see cref="Sekai.Core.CoreSceneArgs"/>).
	/// </summary>
	public sealed class MusicScoreMakerSceneArgs
	{
		private const string LastCustomScorePathKey = "OpenSekai.MusicScoreMaker.LastCustomMusicScorePath";

		public MusicScoreMakerSceneArgs(ScreenLayerMusicScoreMaker.BootArg bootArg)
		{
			BootArg = bootArg;
		}

		public ScreenLayerMusicScoreMaker.BootArg BootArg { get; }

		/// <summary>Opens <paramref name="bootArg"/> in the next MusicScoreMaker scene.</summary>
		public static void Prepare(ScreenLayerMusicScoreMaker.BootArg bootArg)
		{
			SceneSwitcher.SetSceneArgs(new MusicScoreMakerSceneArgs(bootArg));
#if UNITY_EDITOR
			string path = bootArg?.CustomMusicScoreEntry?.RootDirectory;
			if (!string.IsNullOrEmpty(path))
			{
				UnityEditor.EditorPrefs.SetString(LastCustomScorePathKey, path);
			}
#endif
		}

		public static MusicScoreMakerSceneArgs Take()
		{
			return SceneSwitcher.TakeSceneArgs<MusicScoreMakerSceneArgs>();
		}

		/// <summary>Boot argument for editing a local custom chart opened from the manager.</summary>
		public static ScreenLayerMusicScoreMaker.BootArg CreateBootArg(CustomMusicScoreEntry entry)
		{
			return new ScreenLayerMusicScoreMaker.BootArg
			{
				musicId = entry.MusicId,
				difficulty = entry.Manifest.musicDifficultyType,
				vocalId = 0,
				baseMusicDifficultyId = -1,
				MusicScoreMakerData = entry.LoadScore(),
				LastSavedDataHash = null,
				CurrentMusicScoreScale = 1f,
				FromScreenType = MenuScreenType.MusicScoreMakerTop,
				CustomMusicScoreEntry = entry
			};
		}

		/// <summary>Folder of the custom chart last opened in the editor from the Unity Editor, for direct scene runs.</summary>
		public static string LastCustomMusicScorePath
		{
			get
			{
#if UNITY_EDITOR
				return UnityEditor.EditorPrefs.GetString(LastCustomScorePathKey, string.Empty);
#else
				return string.Empty;
#endif
			}
		}
	}
}
