using System;
using System.Collections.Generic;

namespace Sekai.Live
{
	/// <summary>One on-screen stretch of a hold or guide body between two hi-speed changes.</summary>
	public struct NoteBodyPiece
	{
		/// <summary>Score time of the body point where the stretch starts (nearest the start note).</summary>
		public float StartTime;

		/// <summary>Score time of the body point where the stretch ends.</summary>
		public float EndTime;

		/// <summary>Number of trapezoids the stretch is drawn with.</summary>
		public int Steps;

		/// <summary>True when the stretch continues the previous one on screen without a gap.</summary>
		public bool ConnectsToPrevious;
	}

	/// <summary>
	/// Splits the body between two consecutive notes of a hold or guide into the stretches that are on screen.
	/// Every point of a body reaches the judge line at its own score time, so its screen progress comes from the
	/// scroll timeline. The timeline is linear between hi-speed changes, so each piece between changes is
	/// clipped to the screen exactly and sampled in proportion to its on-screen length. Shared by the live line
	/// view (which draws the stretches) and the chart editor (which counts their trapezoids against the pool).
	/// </summary>
	public static class NoteBodySampler
	{
		/// <summary>
		/// Screen progress at <paramref name="now"/> of the body point that reaches the judge line at
		/// <paramref name="scoreTime"/>. The per-note speed ratio is interpolated between the two notes.
		/// </summary>
		public static float ProgressAt(NoteScrollTimeline timeline, float displayTime, float now, float scoreTime,
			float startTime, float endTime, float startSpeedRatio, float endSpeedRatio)
		{
			float duration = endTime - startTime;
			float rate = duration > 0f ? Clamp01((scoreTime - startTime) / duration) : 0f;
			float speedRatio = startSpeedRatio + (endSpeedRatio - startSpeedRatio) * rate;
			return timeline.ProgressAt(now, scoreTime, speedRatio, displayTime);
		}

		/// <summary>
		/// Appends the on-screen stretches of the body to <paramref name="pieces"/> (if given) and returns the
		/// total number of trapezoids. The part that already reached the judge line is not drawn.
		/// </summary>
		public static int CollectVisiblePieces(NoteScrollTimeline timeline, float displayTime, float now,
			float startTime, float endTime, float startSpeedRatio, float endSpeedRatio, int splitCount,
			List<NoteBodyPiece> pieces)
		{
			if (timeline == null || endTime <= startTime)
			{
				return 0;
			}

			float pieceStart = Math.Max(startTime, now);
			int total = 0;
			bool previousReachedEnd = false;
			while (pieceStart < endTime)
			{
				float pieceEnd = Math.Min(timeline.NextChangeAfter(pieceStart), endTime);
				float startProgress = ProgressAt(timeline, displayTime, now, pieceStart, startTime, endTime, startSpeedRatio, endSpeedRatio);
				float endProgress = ProgressAt(timeline, displayTime, now, pieceEnd, startTime, endTime, startSpeedRatio, endSpeedRatio);

				if (TryClipToScreen(startProgress, endProgress, out float enter, out float exit))
				{
					float span = Math.Abs(Lerp(startProgress, endProgress, exit) - Lerp(startProgress, endProgress, enter));
					int steps = Math.Max(1, (int)Math.Ceiling(span * splitCount));
					total += steps;
					pieces?.Add(new NoteBodyPiece
					{
						StartTime = Lerp(pieceStart, pieceEnd, enter),
						EndTime = Lerp(pieceStart, pieceEnd, exit),
						Steps = steps,
						ConnectsToPrevious = previousReachedEnd && enter <= 0f
					});
					previousReachedEnd = exit >= 1f;
				}
				else
				{
					previousReachedEnd = false;
				}

				pieceStart = pieceEnd;
			}

			return total;
		}

		/// <summary>
		/// Earliest time any point of the body can be on screen (a conservative bound), never later than the
		/// end note's time. Used to decide when a body needs checking at all.
		/// </summary>
		public static float FirstVisibleTime(NoteScrollTimeline timeline, float displayTime, float startTime,
			float endTime, float startSpeedRatio, float endSpeedRatio)
		{
			if (startSpeedRatio == 0f || endSpeedRatio == 0f || Math.Sign(startSpeedRatio) != Math.Sign(endSpeedRatio))
			{
				return float.NegativeInfinity;
			}

			// The body's scroll positions span [minPosition, maxPosition]; the extremes sit at its ends or at
			// hi-speed changes inside it.
			double minPosition = double.PositiveInfinity;
			double maxPosition = double.NegativeInfinity;
			for (float time = startTime; ; time = timeline.NextChangeAfter(time))
			{
				float clamped = Math.Min(time, endTime);
				double position = timeline.PositionAt(clamped);
				minPosition = Math.Min(minPosition, position);
				maxPosition = Math.Max(maxPosition, position);
				if (time >= endTime)
				{
					break;
				}
			}

			double low = double.PositiveInfinity;
			double high = double.NegativeInfinity;
			foreach (float speedRatio in new[] { startSpeedRatio, endSpeedRatio })
			{
				double reach = displayTime / (double)speedRatio;
				low = Math.Min(low, Math.Min(minPosition, minPosition - reach));
				high = Math.Max(high, Math.Max(maxPosition, maxPosition - reach));
			}

			return timeline.FirstTimeInPositionRange(low, high, endTime);
		}

		// Part of a straight piece (progress going linearly from start to end) that is on screen, as fractions
		// of the piece. False when none of it is.
		private static bool TryClipToScreen(float startProgress, float endProgress, out float enter, out float exit)
		{
			float delta = endProgress - startProgress;
			if (Math.Abs(delta) < 1e-6f)
			{
				enter = 0f;
				exit = 1f;
				return startProgress >= 0f && startProgress <= 1f;
			}

			float atSpawnLine = -startProgress / delta;
			float atJudgeLine = (1f - startProgress) / delta;
			enter = Math.Max(Math.Min(atSpawnLine, atJudgeLine), 0f);
			exit = Math.Min(Math.Max(atSpawnLine, atJudgeLine), 1f);
			return enter < exit;
		}

		private static float Lerp(float from, float to, float rate)
		{
			return from + (to - from) * rate;
		}

		private static float Clamp01(float value)
		{
			return value < 0f ? 0f : value > 1f ? 1f : value;
		}
	}
}
