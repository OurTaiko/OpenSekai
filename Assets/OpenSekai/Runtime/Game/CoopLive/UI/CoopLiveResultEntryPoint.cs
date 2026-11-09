using UnityEngine;

namespace Sekai.CoopLive
{
	/// <summary>Starts the CoopLive.Result scene: shows the result screen authored in CoopLive.Result.unity.</summary>
	public sealed class CoopLiveResultEntryPoint : MenuSceneEntryPoint
	{
		[SerializeField]
		private ScreenLayerCoopResult resultScreen;

		protected override void OnSceneStart()
		{
			LiveTransitioner.SafeForceFinish(null);
			ShowSceneScreen(MenuScreenType.CoopLiveResult, resultScreen);
		}
	}
}
