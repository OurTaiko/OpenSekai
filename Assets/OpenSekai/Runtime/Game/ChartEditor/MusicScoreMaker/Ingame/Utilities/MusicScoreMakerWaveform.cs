using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Audio;
using UnityEngine;

namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
	/// <summary>
	/// The chart's audio as peaks for the editor's waveform (<see cref="Views.MusicScoreWaveformView"/>): decoded once
	/// with BASS, the same decoder the editor plays with, on a worker thread. Peaks are scaled so the loudest is 1.
	/// </summary>
	public static class MusicScoreMakerWaveform
	{
		private const int TargetPeaksPerSecond = 200;

		private static int _loadId;

		public static float[] Peaks { get; private set; }

		/// <summary>Peaks per second of audio.</summary>
		public static double PeakRate { get; private set; }

		/// <summary>Audio time of the chart's tick 0 (the chart's fillerSec).</summary>
		public static float FillerSec { get; set; }

		public static bool HasData => Peaks != null && Peaks.Length > 0 && PeakRate > 0;

		/// <summary>Reads and decodes <paramref name="audioPath"/>; true when the peaks are ready.</summary>
		public static async UniTask<bool> LoadAsync(string audioPath, CancellationToken ct)
		{
			int loadId = ++_loadId;
			Clear();
			if (string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath))
			{
				return false;
			}

			float[] peaks = null;
			double rate = 0;
			try
			{
				(peaks, rate) = await UniTask.RunOnThreadPool(() =>
				{
					using (SoundBuffer buffer = SoundBuffer.FromFile(audioPath))
					{
						float[] result = buffer.ComputePeaks(TargetPeaksPerSecond, out double peakRate);
						return (result, peakRate);
					}
				}, cancellationToken: ct);
			}
			catch (OperationCanceledException)
			{
				return false;
			}
			catch (Exception exception) when (exception is IOException || exception is DllNotFoundException || exception is EntryPointNotFoundException || exception is ArgumentException)
			{
				Debug.LogWarning($"MusicScoreMaker: waveform of {audioPath}: {exception.Message}");
				return false;
			}

			if (loadId != _loadId || peaks == null || peaks.Length == 0)
			{
				return false;
			}

			Normalize(peaks);
			Peaks = peaks;
			PeakRate = rate;
			return true;
		}

		public static void Clear()
		{
			Peaks = null;
			PeakRate = 0;
		}

		/// <summary>The loudest peak between two audio times, 0 outside the audio.</summary>
		public static float GetPeak(double fromSec, double toSec)
		{
			float[] peaks = Peaks;
			if (peaks == null || PeakRate <= 0)
			{
				return 0f;
			}
			if (toSec < fromSec)
			{
				(fromSec, toSec) = (toSec, fromSec);
			}
			long first = (long)Math.Floor(fromSec * PeakRate);
			long last = (long)Math.Floor(toSec * PeakRate);
			if (last < 0 || first >= peaks.Length)
			{
				return 0f;
			}
			first = Math.Max(0, first);
			last = Math.Min(peaks.Length - 1, last);
			float max = 0f;
			for (long i = first; i <= last; i++)
			{
				if (peaks[i] > max)
				{
					max = peaks[i];
				}
			}
			return max;
		}

		private static void Normalize(float[] peaks)
		{
			float max = 0f;
			foreach (float peak in peaks)
			{
				max = Mathf.Max(max, peak);
			}
			if (max <= 0f)
			{
				return;
			}
			for (int i = 0; i < peaks.Length; i++)
			{
				peaks[i] /= max;
			}
		}
	}
}
