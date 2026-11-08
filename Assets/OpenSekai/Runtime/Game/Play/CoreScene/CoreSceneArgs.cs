using OpenSekai.SceneFlow;
using Sekai.Live;
using UnityEngine;

namespace Sekai.Core
{
	/// <summary>
	/// What the Core scene should play. Set by the caller before switching to Core and taken by <see cref="EntryPoint"/>
	/// (the OurTaikoPlay pattern of handing the selected song over through the scene switcher).
	/// </summary>
	public sealed class CoreSceneArgs
	{
		private const string LastCustomScorePathKey = "OpenSekai.Core.LastCustomMusicScorePath";

		public CoreSceneArgs(PlayMode playMode, LiveBootDataBase bootData)
		{
			PlayMode = playMode;
			BootData = bootData;
		}

		public PlayMode PlayMode { get; }

		public LiveBootDataBase BootData { get; }

		/// <summary>Hands <paramref name="bootData"/> to the next Core scene.</summary>
		public static void Prepare(PlayMode playMode, LiveBootDataBase bootData)
		{
			SceneSwitcher.SetSceneArgs(new CoreSceneArgs(playMode, bootData));
#if UNITY_EDITOR
			if (bootData is FreeLiveBootData freeLive && !string.IsNullOrEmpty(freeLive.CustomMusicScorePath))
			{
				UnityEditor.EditorPrefs.SetString(LastCustomScorePathKey, freeLive.CustomMusicScorePath);
			}
#endif
		}

		public static CoreSceneArgs Take()
		{
			return SceneSwitcher.TakeSceneArgs<CoreSceneArgs>();
		}

		/// <summary>Folder of the custom chart last sent to Core from the Editor, used when Core is played directly.</summary>
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
