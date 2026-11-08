// Ported from OurTaikoPlay (https://github.com/OurTaiko/OurTaikoPlay, GPLv3),
// Assets/OurTaiko/Runtime/Core/GameTimeline.cs. See NOTICE.md.
using System.Diagnostics;
using UnityEngine;

namespace OpenSekai.Audio
{
	/// <summary>
	/// One high-resolution time source for the application (Stopwatch based, unaffected by Time.timeScale).
	/// <see cref="FrameTime"/> is sampled once per Unity frame so every system sees the same value in a frame.
	/// </summary>
	public static class GameTimeline
	{
		private static long origin = Stopwatch.GetTimestamp();
		private static int sampledFrame = -1;
		private static double frameTime;

		/// <summary>Seconds since startup, read live.</summary>
		public static double Realtime => (Stopwatch.GetTimestamp() - origin) / (double)Stopwatch.Frequency;

		/// <summary>Seconds since startup, sampled at the first read of each frame.</summary>
		public static double FrameTime
		{
			get
			{
				if (!Application.isPlaying)
				{
					return Time.realtimeSinceStartupAsDouble;
				}

				UpdateFrame();
				return frameTime;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Reset()
		{
			origin = Stopwatch.GetTimestamp();
			sampledFrame = -1;
			frameTime = 0;
		}

		internal static void UpdateFrame()
		{
			if (sampledFrame == Time.frameCount)
			{
				return;
			}

			sampledFrame = Time.frameCount;
			frameTime = Realtime;
		}
	}
}
