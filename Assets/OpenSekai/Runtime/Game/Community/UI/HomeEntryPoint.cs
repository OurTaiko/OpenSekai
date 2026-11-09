using UnityEngine;

namespace Sekai.Community
{
	/// <summary>Starts the Home scene, the first scene: shows the navigation screen authored in Home.unity.</summary>
	public sealed class HomeEntryPoint : MenuSceneEntryPoint
	{
		[SerializeField]
		private ScreenLayerOpenSekaiHome homeScreen;

		protected override void OnSceneStart()
		{
			LiveTransitioner.SafeForceFinish(null);
			ShowSceneScreen(MenuScreenType.OpenSekaiHome, homeScreen);
		}
	}
}
