using OpenSekai.SceneFlow;

namespace Sekai.CoopLive
{
	/// <summary>Which co-op round the Result scene shows (the room's 上一局结果). Without one it shows the live just played.</summary>
	public sealed class CoopResultSceneArgs
	{
		public int Round { get; private set; }

		/// <summary>Opens the Result scene for <paramref name="round"/>.</summary>
		public static void Open(int round)
		{
			SceneSwitcher.SetSceneArgs(new CoopResultSceneArgs { Round = round });
			SceneManager.Instance.RequestScene(SceneManager.Scene.LiveResult);
		}

		public static CoopResultSceneArgs Take() => SceneSwitcher.TakeSceneArgs<CoopResultSceneArgs>();
	}
}
