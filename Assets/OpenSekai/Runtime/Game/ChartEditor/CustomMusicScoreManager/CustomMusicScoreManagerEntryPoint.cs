using UnityEngine;

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>
	/// Starts the custom chart manager scene (the first scene). The manager screen is authored in
	/// Charts.Local.unity; this shows it once the shared menu start-up is done.
	/// </summary>
	public sealed class CustomMusicScoreManagerEntryPoint : MenuSceneEntryPoint
	{
		[SerializeField]
		private ScreenLayerCustomMusicScoreManager managerScreen;

		protected override void OnSceneStart()
		{
			LiveTransitioner.SafeForceFinish(null);
			ShowSceneScreen(MenuScreenType.MusicScoreMakerTop, managerScreen);
		}
	}
}
