using UnityEngine;

namespace Sekai.Community
{
	/// <summary>Starts the CommunityCharts scene: shows the online chart browser authored in CommunityCharts.unity.</summary>
	public sealed class CommunityChartsEntryPoint : MenuSceneEntryPoint
	{
		[SerializeField]
		private ScreenLayerCommunityCharts chartsScreen;

		protected override void OnSceneStart()
		{
			LiveTransitioner.SafeForceFinish(null);
			ShowSceneScreen(MenuScreenType.CommunityCharts, chartsScreen);
		}
	}
}
