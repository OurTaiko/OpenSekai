using UnityEngine;

namespace Sekai.CoopLive
{
	/// <summary>Starts the CoopLive scene: shows the 協力ライブ room screen authored in CoopLive.unity.</summary>
	public sealed class CoopLiveEntryPoint : MenuSceneEntryPoint
	{
		[SerializeField]
		private ScreenLayerCoopLive coopScreen;

		protected override void OnSceneStart()
		{
			LiveTransitioner.SafeForceFinish(null);
			ShowSceneScreen(MenuScreenType.CoopLive, coopScreen);
		}
	}
}
