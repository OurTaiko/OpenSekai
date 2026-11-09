using UnityEngine;

namespace Sekai
{
	/// <summary>Starts the Result scene: shows the result screen authored in Result.unity.</summary>
	public sealed class LiveResultEntryPoint : MenuSceneEntryPoint
	{
		[SerializeField]
		private ScreenLayerLiveResult resultScreen;

		protected override void OnSceneStart()
		{
			LiveTransitioner.SafeForceFinish(null);
			ShowSceneScreen(MenuScreenType.LiveResult, resultScreen);
		}
	}
}
