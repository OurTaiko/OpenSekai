using System;
using System.Collections.Generic;

namespace Sekai.Live
{
	/// <summary>
	/// Scroll position S(t): the hi-speed ratio integrated over song time (seconds).
	/// A note's distance from the judge line is (S(noteTime) - S(now)) * note.speedRatio, measured in
	/// seconds at 1x speed, so it can be compared directly with the note display time. Hi-speed may be
	/// zero (notes stop) or negative (notes scroll back away from the judge line).
	/// </summary>
	public sealed class NoteScrollTimeline
	{
		// Segment i starts at times[i] with scroll position positions[i] and runs at speeds[i] until times[i + 1].
		// Before times[0] the first speed is extended backwards; after the last start the last speed runs on.
		private readonly double[] times;
		private readonly double[] positions;
		private readonly float[] speeds;

		private NoteScrollTimeline(double[] times, double[] positions, float[] speeds)
		{
			this.times = times;
			this.positions = positions;
			this.speeds = speeds;
		}

		public static NoteScrollTimeline Create(MusicScoreInfo[] scoreInfos)
		{
			if (scoreInfos == null || scoreInfos.Length == 0)
			{
				return new NoteScrollTimeline(new[] { 0d }, new[] { 0d }, new[] { 1f });
			}

			// Score infos are generated in time order; sort defensively (stable, so a later entry at the same time wins).
			var ordered = new List<MusicScoreInfo>(scoreInfos);
			for (int i = 1; i < ordered.Count; i++)
			{
				MusicScoreInfo value = ordered[i];
				int j = i - 1;
				while (j >= 0 && ordered[j].time > value.time)
				{
					ordered[j + 1] = ordered[j];
					j--;
				}
				ordered[j + 1] = value;
			}

			var timeList = new List<double>(ordered.Count);
			var speedList = new List<float>(ordered.Count);
			foreach (MusicScoreInfo info in ordered)
			{
				int last = timeList.Count - 1;
				if (last >= 0 && timeList[last] == info.time)
				{
					speedList[last] = info.speedRatio;
					continue;
				}
				if (last >= 0 && speedList[last] == info.speedRatio)
				{
					continue;
				}

				timeList.Add(info.time);
				speedList.Add(info.speedRatio);
			}

			var positionArray = new double[timeList.Count];
			for (int i = 1; i < positionArray.Length; i++)
			{
				positionArray[i] = positionArray[i - 1] + (timeList[i] - timeList[i - 1]) * speedList[i - 1];
			}

			return new NoteScrollTimeline(timeList.ToArray(), positionArray, speedList.ToArray());
		}

		public double PositionAt(float time)
		{
			int i = FindSegment(time);
			return positions[i] + (time - times[i]) * speeds[i];
		}

		public float SpeedAt(float time)
		{
			return time < times[0] ? speeds[0] : speeds[FindSegment(time)];
		}

		/// <summary>The first hi-speed change strictly after <paramref name="time"/>, or positive infinity.</summary>
		public float NextChangeAfter(float time)
		{
			if (time < times[0])
			{
				return (float)times[0];
			}

			int next = FindSegment(time) + 1;
			return next < times.Length ? (float)times[next] : float.PositiveInfinity;
		}

		/// <summary>
		/// Screen progress at time <paramref name="now"/> of a point that reaches the judge line at
		/// <paramref name="scoreTime"/>: 1 on the judge line, 0 at the spawn line. Values outside [0, 1] are off screen.
		/// </summary>
		public float ProgressAt(float now, float scoreTime, float speedRatio, float displayTime)
		{
			if (displayTime <= 0f)
			{
				return 1f;
			}

			double distance = (PositionAt(scoreTime) - PositionAt(now)) * speedRatio;
			return (float)(1d - distance / displayTime);
		}

		/// <summary>
		/// Earliest time t ≤ notAfter at which a note at scroll position <paramref name="notePosition"/> with
		/// per-note speed <paramref name="noteSpeedRatio"/> is on screen, i.e. its distance from the judge
		/// line is within [0, displayTime]. A note is always on the judge line at its own time, so the result
		/// never exceeds notAfter. Returns negative infinity when the note is on screen from the very start.
		/// </summary>
		public float FirstVisibleTime(double notePosition, float noteSpeedRatio, float displayTime, float notAfter)
		{
			if (noteSpeedRatio == 0f)
			{
				return float.NegativeInfinity;
			}

			// 0 ≤ (notePosition - S) * k ≤ displayTime, solved for S.
			double reach = displayTime / (double)noteSpeedRatio;
			double low = Math.Min(notePosition, notePosition - reach);
			double high = Math.Max(notePosition, notePosition - reach);

			for (int i = -1; i < times.Length; i++)
			{
				double start = i < 0 ? double.NegativeInfinity : times[i];
				if (start > notAfter)
				{
					break;
				}

				double end = i + 1 < times.Length ? Math.Min(times[i + 1], notAfter) : notAfter;
				int anchor = Math.Max(i, 0);
				double anchorTime = times[anchor];
				double anchorPosition = positions[anchor];
				float speed = speeds[anchor];

				if (speed == 0f)
				{
					if (anchorPosition >= low && anchorPosition <= high)
					{
						return (float)start;
					}
					continue;
				}

				double tLow = anchorTime + (low - anchorPosition) / speed;
				double tHigh = anchorTime + (high - anchorPosition) / speed;
				double enter = Math.Max(Math.Min(tLow, tHigh), start);
				double exit = Math.Min(Math.Max(tLow, tHigh), end);
				if (enter <= exit)
				{
					return (float)enter;
				}
			}

			return notAfter;
		}

		// Index of the segment containing time; times before the first segment map to segment 0.
		private int FindSegment(float time)
		{
			int low = 0;
			int high = times.Length - 1;
			while (low < high)
			{
				int mid = (low + high + 1) >> 1;
				if (times[mid] <= time)
				{
					low = mid;
				}
				else
				{
					high = mid - 1;
				}
			}
			return low;
		}
	}
}
