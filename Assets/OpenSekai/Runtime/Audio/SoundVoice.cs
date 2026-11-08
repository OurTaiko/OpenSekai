using System;
using AOT;
using ManagedBass;
using ManagedBass.Mix;

namespace OpenSekai.Audio
{
	/// <summary>
	/// One playing instance of a <see cref="SoundBuffer"/>: a BASS decode stream attached (paused) to its group's
	/// sub-mixer. Voices are cheap; create one per playback instead of rewinding a shared channel, so the same sound
	/// can overlap itself (OurTaiko/MajdataPlay keep a single voice per sample).
	/// </summary>
	public sealed class SoundVoice : IDisposable
	{
		private static readonly SyncProcedure LoopSync = OnLoopEnd;

		private readonly SoundBuffer buffer;
		private readonly int generation;
		private int handle;
		private float volume = 1f;

		private SoundVoice(SoundBuffer buffer, AudioGroup group, int handle, int generation)
		{
			this.buffer = buffer;
			Group = group;
			this.handle = handle;
			this.generation = generation;
			buffer.Attach(this);
		}

		public AudioGroup Group { get; }

		public SoundBuffer Buffer => buffer;

		/// <summary>False once stopped, disposed, finished (one-shots), or the output was rebuilt.</summary>
		public bool IsAlive
		{
			get
			{
				var engine = AudioEngine.Instance;
				return handle != 0 && engine != null && engine.Generation == generation && Bass.ChannelIsActive(handle) != PlaybackState.Stopped;
			}
		}

		public bool IsPaused => handle != 0 && BassMix.ChannelHasFlag(handle, BassFlags.MixerChanPause);

		public bool IsPlaying => IsAlive && !IsPaused;

		/// <summary>Rendered position in seconds (what the mixer has consumed; subtract output latency for the heard position).</summary>
		public double Position => handle == 0 ? 0 : Bass.ChannelBytes2Seconds(handle, BassMix.ChannelGetPosition(handle));

		public double Length => buffer.Length;

		/// <summary>
		/// Creates a paused voice. <paramref name="autoFree"/> releases the stream when it ends (fire-and-forget SE).
		/// Returns null when the engine is unavailable or the data cannot be decoded.
		/// </summary>
		public static SoundVoice Create(SoundBuffer buffer, AudioGroup group, bool loop = false, bool autoFree = false)
		{
			var engine = AudioEngine.Instance;
			if (buffer == null || buffer.IsDisposed || engine == null || !engine.Available)
			{
				return null;
			}

			var handle = buffer.OpenDecoder(BassFlags.Decode | BassFlags.Float | (loop && !buffer.HasLoopRegion ? BassFlags.Loop : 0));
			if (handle == 0)
			{
				UnityEngine.Debug.LogWarning("[Audio] Could not open audio data: " + Bass.LastError);
				return null;
			}

			if (loop && buffer.HasLoopRegion)
			{
				var bytesPerSample = Bass.ChannelSeconds2Bytes(handle, 1.0) / Math.Max(1, buffer.SampleRate);
				var loopStart = buffer.LoopStartSample * bytesPerSample;
				var loopEnd = buffer.LoopEndSample * bytesPerSample;
				Bass.ChannelSetSync(handle, SyncFlags.Position | SyncFlags.Mixtime, loopEnd, LoopSync, new IntPtr(loopStart));
			}

			var flags = BassFlags.MixerChanPause | (autoFree ? BassFlags.AutoFree | BassFlags.MixerChanNoRampin : 0);
			if (!BassMix.MixerAddChannel(engine.GetGroupMixer(group), handle, flags))
			{
				UnityEngine.Debug.LogWarning("[Audio] Could not attach voice: " + Bass.LastError);
				Bass.StreamFree(handle);
				return null;
			}

			return new SoundVoice(buffer, group, handle, engine.Generation);
		}

		[MonoPInvokeCallback(typeof(SyncProcedure))]
		private static void OnLoopEnd(int sync, int channel, int data, IntPtr user)
		{
			// Mixtime sync on the source: the jump happens sample-accurately inside the mixer.
			Bass.ChannelSetPosition(channel, user.ToInt64());
		}

		public void SetVolume(float value)
		{
			volume = Math.Max(0f, value);
			if (handle != 0)
			{
				Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, volume);
			}
		}

		/// <summary>Seeks to <paramref name="seconds"/> and starts (or resumes) playback.</summary>
		public void Play(double seconds = 0, float? withVolume = null)
		{
			if (handle == 0)
			{
				return;
			}

			if (withVolume.HasValue)
			{
				SetVolume(withVolume.Value);
			}

			Seek(seconds);
			Resume();
		}

		public void Seek(double seconds)
		{
			if (handle == 0)
			{
				return;
			}

			var position = Bass.ChannelSeconds2Bytes(handle, Math.Max(0, seconds));
			BassMix.ChannelSetPosition(handle, position);
		}

		public void Pause()
		{
			if (handle != 0)
			{
				BassMix.ChannelAddFlag(handle, BassFlags.MixerChanPause);
			}
		}

		public void Resume()
		{
			if (handle != 0)
			{
				BassMix.ChannelRemoveFlag(handle, BassFlags.MixerChanPause);
			}
		}

		public void Stop()
		{
			Dispose();
		}

		public void Dispose()
		{
			if (handle == 0)
			{
				return;
			}

			var engine = AudioEngine.Instance;
			if (engine != null && engine.Generation == generation)
			{
				BassMix.MixerRemoveChannel(handle);
				Bass.StreamFree(handle);
			}

			handle = 0;
			buffer.Detach(this);
		}
	}
}
