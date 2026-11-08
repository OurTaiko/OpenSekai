// Based on OurTaikoPlay's SongClock (https://github.com/OurTaiko/OurTaikoPlay, GPLv3), which ports
// MajdataPlay's GamePlayManager timing. See NOTICE.md.
//
// Differences from the original: instead of correcting the drift only on BassSimple and only for the first
// two seconds, this clock keeps slewing towards the heard audio position on every backend, and jumps only
// when the error is large (seek, device hiccup).
using System;

namespace OpenSekai.Audio
{
	/// <summary>
	/// Song position derived from <see cref="GameTimeline.FrameTime"/> and continuously corrected against the
	/// audio position that is actually heard. The value is stable within a frame and monotonic while playing
	/// small corrections.
	/// </summary>
	public sealed class SongClock
	{
		/// <summary>Errors above this snap the clock instead of slewing (seconds).</summary>
		public const double SnapThreshold = 0.1;

		/// <summary>Fraction of the measured error removed per frame.</summary>
		public const double SlewStrength = 0.1;

		private double origin;
		private int lastFrame = -1;

		public bool Running { get; private set; }

		/// <summary>Current song position in seconds.</summary>
		public double Time { get; private set; }

		/// <summary>Last measured difference between heard audio and the clock (seconds), for diagnostics.</summary>
		public double LastError { get; private set; }

		/// <summary>Starts (or restarts) the clock at <paramref name="position"/> seconds.</summary>
		public void Start(double position)
		{
			Time = position;
			origin = GameTimeline.FrameTime - position;
			Running = true;
			LastError = 0;
			lastFrame = -1;
		}

		/// <summary>Freezes the clock at its current position.</summary>
		public void Pause()
		{
			if (!Running)
			{
				return;
			}

			Time = GameTimeline.FrameTime - origin;
			Running = false;
		}

		/// <summary>Freezes the clock at <paramref name="position"/>.</summary>
		public void Seek(double position)
		{
			if (double.IsNaN(position) || double.IsInfinity(position))
			{
				throw new ArgumentOutOfRangeException(nameof(position));
			}

			Time = position;
			Running = false;
		}

		/// <summary>
		/// Advances the clock for this frame. <paramref name="heardPosition"/> is the audio position the listener
		/// hears now (rendered position minus output latency), or null while it is not meaningful.
		/// Calling more than once per frame is harmless.
		/// </summary>
		public void Update(int frame, double? heardPosition)
		{
			if (!Running || frame == lastFrame)
			{
				return;
			}

			lastFrame = frame;
			Time = GameTimeline.FrameTime - origin;
			if (!heardPosition.HasValue)
			{
				return;
			}

			var error = heardPosition.Value - Time;
			LastError = error;
			if (Math.Abs(error) >= SnapThreshold)
			{
				origin -= error;
				Time += error;
				return;
			}

			// Applied from the next frame so the published value never jumps backwards within a frame.
			origin -= error * SlewStrength;
		}
	}
}
