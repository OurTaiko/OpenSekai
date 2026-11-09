using UnityEngine;

namespace CP
{
	public static class FramerateUtility
	{
		public const int DefaultFrameRate = 60;

		// The quality level's own vSync, put back when a menu scene sets its frame rate after a live turned it off.
		private static int? projectVSyncCount;

		/// <summary>Menus: the given rate, or the display's refresh rate, with the quality level's own vSync.</summary>
		public static void SetFrameRate(int targetFrameRate = -1)
		{
			if (projectVSyncCount.HasValue)
			{
				QualitySettings.vSyncCount = projectVSyncCount.Value;
				projectVSyncCount = null;
			}
			Application.targetFrameRate = targetFrameRate > 0 ? targetFrameRate : GetFeasibleFramerate();
		}

		/// <summary>
		/// A live's 滑らかさ(FPS): a fixed rate, or as fast as possible when <paramref name="targetFrameRate"/> is 0 or less.
		/// vSync is turned off, since with it on the desktop ignores the target and runs at the refresh rate.
		/// Mobile always syncs to the display, so there "as fast as possible" is the highest refresh rate it offers.
		/// </summary>
		public static void SetLiveFrameRate(int targetFrameRate)
		{
			if (!projectVSyncCount.HasValue)
			{
				projectVSyncCount = QualitySettings.vSyncCount;
			}
			QualitySettings.vSyncCount = 0;

			if (targetFrameRate > 0)
			{
				Application.targetFrameRate = targetFrameRate;
			}
			else
			{
				Application.targetFrameRate = Application.isMobilePlatform ? GetHighestRefreshRate() : -1;
			}
		}

		private static int GetFeasibleFramerate()
		{
			int refreshRate = GetRefreshRate(Screen.currentResolution);
			return refreshRate > 0 ? refreshRate : DefaultFrameRate;
		}

		private static int GetHighestRefreshRate()
		{
			int highest = GetFeasibleFramerate();
			foreach (Resolution resolution in Screen.resolutions)
			{
				highest = Mathf.Max(highest, GetRefreshRate(resolution));
			}
			return highest;
		}

		private static int GetRefreshRate(Resolution resolution)
		{
#if UNITY_2022_2_OR_NEWER
			RefreshRate refreshRateRatio = resolution.refreshRateRatio;
			if (refreshRateRatio.numerator > 0 && refreshRateRatio.denominator > 0)
			{
				double refreshRate = (double)refreshRateRatio.numerator / refreshRateRatio.denominator;
				return Mathf.Max(1, Mathf.RoundToInt((float)refreshRate));
			}
#endif

#pragma warning disable CS0618
			return resolution.refreshRate;
#pragma warning restore CS0618
		}
	}
}
