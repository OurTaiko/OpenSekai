using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcbDecoder;
using OpenSekai.Audio;
using UnityEngine;
using Random = System.Random;

namespace Sekai
{
	/// <summary>
	/// A loaded ACB cue sheet played through the BASS engine. Waveforms are decoded with the managed HCA decoder into
	/// <see cref="SoundBuffer"/>s, either eagerly (short SE banks) or on a worker thread (music).
	/// Replaces CriAtomExAcb.
	/// </summary>
	public sealed class CueSheet : IDisposable
	{
		private static readonly Random RandomSource = new Random();

		private readonly AcbFile acb;
		private readonly Dictionary<int, AcbEntry> entriesByWaveId = new Dictionary<int, AcbEntry>();
		private readonly Dictionary<int, SoundBuffer> buffers = new Dictionary<int, SoundBuffer>();
		private readonly Dictionary<string, Task> pendingDecodes = new Dictionary<string, Task>(StringComparer.Ordinal);
		private readonly object sync = new object();
		private Task decodeAllTask;
		private bool disposed;

		private CueSheet(string name, AcbFile acb)
		{
			Name = name;
			this.acb = acb;
			foreach (var entry in acb.Entries)
			{
				entriesByWaveId[entry.WaveId] = entry;
			}
		}

		public string Name { get; }

		public bool IsAvailable => !disposed;

		/// <summary>Parses ACB bytes; returns null (and logs) when they are not a usable cue sheet.</summary>
		public static CueSheet Load(string name, byte[] acbBytes)
		{
			if (acbBytes == null || acbBytes.Length == 0)
			{
				return null;
			}

			try
			{
				return new CueSheet(name, AcbFile.Load(acbBytes));
			}
			catch (Exception exception)
			{
				Debug.LogWarningFormat("[Audio] ACB {0} could not be parsed: {1}", name, exception.Message);
				return null;
			}
		}

		public bool Exists(string cueName)
		{
			return !string.IsNullOrEmpty(cueName) && acb.CueWaveforms.ContainsKey(cueName);
		}

		public SoundCueInfo[] GetCueInfoList()
		{
			var infos = new List<SoundCueInfo>(acb.CueWaveforms.Count);
			foreach (var cueName in acb.CueWaveforms.Keys)
			{
				infos.Add(new SoundCueInfo { name = cueName, length = GetCueLengthMs(cueName) });
			}

			return infos.ToArray();
		}

		public bool TryGetFirstCueName(out string cueName)
		{
			foreach (var name in acb.CueWaveforms.Keys)
			{
				if (!string.IsNullOrEmpty(name))
				{
					cueName = name;
					return true;
				}
			}

			cueName = null;
			return false;
		}

		public long GetCueLengthMs(string cueName)
		{
			if (acb.CueLengths.TryGetValue(cueName, out var lengthMs))
			{
				return lengthMs;
			}

			// Fall back to decoded waveform lengths when the CueTable has no Length column.
			long longest = 0;
			lock (sync)
			{
				if (acb.CueWaveforms.TryGetValue(cueName, out var waveforms))
				{
					foreach (var waveform in waveforms)
					{
						if (buffers.TryGetValue(waveform.WaveId, out var buffer) && buffer.Length > 0)
						{
							longest = Math.Max(longest, (long)(buffer.Length * 1000));
						}
					}
				}
			}

			return longest;
		}

		/// <summary>Decodes every waveform now (short SE banks).</summary>
		public void DecodeAll()
		{
			foreach (var cueName in acb.CueWaveforms.Keys)
			{
				DecodeCue(cueName);
			}
		}

		/// <summary>Decodes every waveform on a worker thread. Repeated calls share one task.</summary>
		public Task DecodeAllAsync()
		{
			lock (sync)
			{
				return decodeAllTask ??= Task.Run(DecodeAll);
			}
		}

		/// <summary>True when every waveform of the cue is decoded.</summary>
		public bool IsDecoded(string cueName)
		{
			if (!acb.CueWaveforms.TryGetValue(cueName, out var waveforms))
			{
				return true;
			}

			lock (sync)
			{
				foreach (var waveform in waveforms)
				{
					if (!buffers.ContainsKey(waveform.WaveId) && entriesByWaveId.ContainsKey(waveform.WaveId))
					{
						return false;
					}
				}
			}

			return true;
		}

		/// <summary>Decodes the cue's waveforms on a worker thread. Repeated calls share one task.</summary>
		public Task DecodeCueAsync(string cueName)
		{
			if (IsDecoded(cueName))
			{
				return Task.CompletedTask;
			}

			lock (sync)
			{
				if (decodeAllTask != null)
				{
					return decodeAllTask;
				}

				if (!pendingDecodes.TryGetValue(cueName, out var task))
				{
					task = Task.Run(() => DecodeCue(cueName));
					pendingDecodes[cueName] = task;
				}

				return task;
			}
		}

		private void DecodeCue(string cueName)
		{
			if (!acb.CueWaveforms.TryGetValue(cueName, out var waveforms))
			{
				return;
			}

			foreach (var waveform in waveforms)
			{
				lock (sync)
				{
					if (disposed || buffers.ContainsKey(waveform.WaveId))
					{
						continue;
					}
				}

				if (!entriesByWaveId.TryGetValue(waveform.WaveId, out var entry))
				{
					continue;
				}

				SoundBuffer buffer;
				try
				{
					var audio = acb.Decode(entry);
					buffer = SoundBuffer.FromPcm(audio.Samples, audio.SampleRate, audio.Channels, audio.LoopStartSample, audio.LoopEndSample);
				}
				catch (Exception exception)
				{
					Debug.LogWarningFormat("[Audio] {0}/{1} wave {2} could not be decoded: {3}", Name, cueName, waveform.WaveId, exception.Message);
					continue;
				}

				lock (sync)
				{
					if (disposed || buffers.ContainsKey(waveform.WaveId))
					{
						buffer.Dispose();
					}
					else
					{
						buffers[waveform.WaveId] = buffer;
					}
				}
			}
		}

		/// <summary>
		/// Creates paused voices for one start of the cue: every waveform, except that random/shuffle sequences
		/// contribute one chosen track. Decodes synchronously if needed. Voices loop when their HCA has a loop region
		/// (as CRI does) or when <paramref name="loop"/> is set.
		/// </summary>
		public List<SoundVoice> CreateVoices(string cueName, AudioGroup group, float volume, bool loop, bool autoFree)
		{
			var voices = new List<SoundVoice>();
			if (disposed || !acb.CueWaveforms.TryGetValue(cueName, out var waveforms))
			{
				return voices;
			}

			if (!IsDecoded(cueName))
			{
				DecodeCue(cueName);
			}

			Dictionary<int, int> choices = null;
			foreach (var waveform in waveforms)
			{
				if (waveform.RandomGroup >= 0)
				{
					choices ??= PickRandomChoices(waveforms);
					if (choices[waveform.RandomGroup] != waveform.RandomChoice)
					{
						continue;
					}
				}

				SoundBuffer buffer;
				lock (sync)
				{
					buffers.TryGetValue(waveform.WaveId, out buffer);
				}

				if (buffer == null)
				{
					continue;
				}

				var voice = SoundVoice.Create(buffer, group, loop || buffer.HasLoopRegion, autoFree && !buffer.HasLoopRegion && !loop);
				if (voice == null)
				{
					continue;
				}

				voice.SetVolume(volume * waveform.VolumeScale);
				voices.Add(voice);
			}

			return voices;
		}

		private static Dictionary<int, int> PickRandomChoices(IReadOnlyList<AcbCueWaveform> waveforms)
		{
			var options = new Dictionary<int, List<int>>();
			foreach (var waveform in waveforms)
			{
				if (waveform.RandomGroup < 0)
				{
					continue;
				}

				if (!options.TryGetValue(waveform.RandomGroup, out var list))
				{
					list = new List<int>();
					options[waveform.RandomGroup] = list;
				}

				if (!list.Contains(waveform.RandomChoice))
				{
					list.Add(waveform.RandomChoice);
				}
			}

			var choices = new Dictionary<int, int>();
			lock (RandomSource)
			{
				foreach (var pair in options)
				{
					choices[pair.Key] = pair.Value[RandomSource.Next(pair.Value.Count)];
				}
			}

			return choices;
		}

		public void Dispose()
		{
			List<SoundBuffer> owned;
			lock (sync)
			{
				if (disposed)
				{
					return;
				}

				disposed = true;
				owned = new List<SoundBuffer>(buffers.Values);
				buffers.Clear();
			}

			foreach (var buffer in owned)
			{
				buffer.Dispose();
			}
		}
	}
}
