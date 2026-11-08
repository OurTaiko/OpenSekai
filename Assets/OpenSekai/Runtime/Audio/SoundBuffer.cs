// Stream opening follows OurTaikoPlay's NativeAudioSample (https://github.com/OurTaiko/OurTaikoPlay, GPLv3), itself a port
// of MajdataPlay's BassHelper. See NOTICE.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ManagedBass;
using ManagedBass.Aac;
using ManagedBass.Opus;

namespace OpenSekai.Audio
{
	/// <summary>
	/// Audio data pinned in memory that any number of <see cref="SoundVoice"/>s decode from independently.
	/// Either an encoded file (wav/mp3/ogg/opus/aac, decoded by BASS on the fly) or PCM wrapped as a 16-bit WAV.
	/// </summary>
	public sealed class SoundBuffer : IDisposable
	{
		private static readonly HashSet<SoundBuffer> LiveBuffers = new HashSet<SoundBuffer>();

		private readonly HashSet<SoundVoice> voices = new HashSet<SoundVoice>();
		private GCHandle pin;
		private byte[] data;

		private SoundBuffer(byte[] bytes, int loopStartSample, int loopEndSample)
		{
			data = bytes ?? throw new ArgumentNullException(nameof(bytes));
			if (data.Length == 0)
			{
				throw new ArgumentException("Empty audio data", nameof(bytes));
			}

			pin = GCHandle.Alloc(data, GCHandleType.Pinned);
			LoopStartSample = loopStartSample;
			LoopEndSample = loopEndSample;
			lock (LiveBuffers)
			{
				LiveBuffers.Add(this);
			}
		}

		public bool IsDisposed { get; private set; }

		/// <summary>Length in seconds, or -1 until a voice (or <see cref="ProbeLength"/>) has opened the data.</summary>
		public double Length { get; private set; } = -1;

		/// <summary>Sample rate of the decoded data, known after the first open.</summary>
		public int SampleRate { get; private set; }

		public int LoopStartSample { get; }

		/// <summary>Exclusive end of the loop region in samples; 0 when the data has no loop region.</summary>
		public int LoopEndSample { get; }

		public bool HasLoopRegion => LoopEndSample > LoopStartSample;

		public int SizeInBytes => data?.Length ?? 0;

		internal IntPtr Address => pin.AddrOfPinnedObject();

		/// <summary>Wraps an encoded audio file. BASS decodes it on the fly; nothing is decoded up front.</summary>
		public static SoundBuffer FromEncoded(byte[] encoded)
		{
			return new SoundBuffer(encoded, 0, 0);
		}

		/// <summary>Reads an encoded audio file from disk.</summary>
		public static SoundBuffer FromFile(string path)
		{
			return FromEncoded(File.ReadAllBytes(path));
		}

		/// <summary>
		/// Wraps interleaved float PCM as an in-memory 16-bit WAV. Safe to call from a worker thread.
		/// Loop points are in sample frames; pass 0/0 for none.
		/// </summary>
		public static SoundBuffer FromPcm(float[] interleaved, int sampleRate, int channels, int loopStartSample = 0, int loopEndSample = 0)
		{
			if (interleaved == null)
			{
				throw new ArgumentNullException(nameof(interleaved));
			}

			if (sampleRate <= 0 || channels <= 0)
			{
				throw new ArgumentException("Invalid PCM format");
			}

			return new SoundBuffer(CreatePcm16Wav(interleaved, sampleRate, channels), loopStartSample, loopEndSample)
			{
				SampleRate = sampleRate,
				Length = interleaved.Length / (double)channels / sampleRate
			};
		}

		/// <summary>Opens a throwaway decoder to read the length and rate. Main thread, engine available.</summary>
		public double ProbeLength()
		{
			if (Length >= 0 || IsDisposed)
			{
				return Length;
			}

			var handle = OpenDecoder(BassFlags.Decode | BassFlags.Float);
			if (handle == 0)
			{
				return Length;
			}

			Bass.StreamFree(handle);
			return Length;
		}

		/// <summary>Opens a decode stream over the pinned data; returns 0 on failure.</summary>
		internal int OpenDecoder(BassFlags flags)
		{
			if (IsDisposed)
			{
				return 0;
			}

			var size = data.LongLength;
			var handle = Bass.CreateStream(Address, 0, size, flags);
			if (handle == 0 && Bass.LastError == Errors.FileFormat)
			{
				handle = BassOpus.CreateStream(Address, 0, size, flags);
			}
#if UNITY_ANDROID || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX
			if (handle == 0 && Bass.LastError == Errors.FileFormat)
			{
				handle = BassAac.CreateStream(Address, 0, size, flags);
			}
#endif
			if (handle == 0)
			{
				return 0;
			}

			if (Length < 0)
			{
				Length = Bass.ChannelBytes2Seconds(handle, Bass.ChannelGetLength(handle));
				SampleRate = Bass.ChannelGetInfo(handle).Frequency;
			}

			return handle;
		}

		internal void Attach(SoundVoice voice)
		{
			voices.Add(voice);
		}

		internal void Detach(SoundVoice voice)
		{
			voices.Remove(voice);
		}

		/// <summary>Stops every voice using this buffer and releases the pinned memory.</summary>
		public void Dispose()
		{
			if (IsDisposed)
			{
				return;
			}

			foreach (var voice in new List<SoundVoice>(voices))
			{
				voice.Dispose();
			}

			IsDisposed = true;
			if (pin.IsAllocated)
			{
				pin.Free();
			}

			data = null;
			lock (LiveBuffers)
			{
				LiveBuffers.Remove(this);
			}
		}

		internal static void ReleaseAll()
		{
			List<SoundBuffer> buffers;
			lock (LiveBuffers)
			{
				buffers = new List<SoundBuffer>(LiveBuffers);
			}

			foreach (var buffer in buffers)
			{
				buffer.Dispose();
			}
		}

		private static byte[] CreatePcm16Wav(float[] samples, int sampleRate, int channels)
		{
			const int headerSize = 44;
			var dataSize = samples.Length * 2;
			var bytes = new byte[headerSize + dataSize];
			WriteAscii(bytes, 0, "RIFF");
			WriteInt32(bytes, 4, headerSize - 8 + dataSize);
			WriteAscii(bytes, 8, "WAVE");
			WriteAscii(bytes, 12, "fmt ");
			WriteInt32(bytes, 16, 16);
			WriteInt16(bytes, 20, 1);
			WriteInt16(bytes, 22, (short)channels);
			WriteInt32(bytes, 24, sampleRate);
			WriteInt32(bytes, 28, sampleRate * channels * 2);
			WriteInt16(bytes, 32, (short)(channels * 2));
			WriteInt16(bytes, 34, 16);
			WriteAscii(bytes, 36, "data");
			WriteInt32(bytes, 40, dataSize);
			var offset = headerSize;
			for (var i = 0; i < samples.Length; i++)
			{
				var value = samples[i];
				if (float.IsNaN(value))
				{
					value = 0f;
				}

				var pcm = (short)Math.Clamp((int)Math.Round(value * 32767f), short.MinValue, short.MaxValue);
				bytes[offset++] = (byte)pcm;
				bytes[offset++] = (byte)(pcm >> 8);
			}

			return bytes;
		}

		private static void WriteAscii(byte[] bytes, int offset, string text)
		{
			for (var i = 0; i < text.Length; i++)
			{
				bytes[offset + i] = (byte)text[i];
			}
		}

		private static void WriteInt32(byte[] bytes, int offset, int value)
		{
			bytes[offset] = (byte)value;
			bytes[offset + 1] = (byte)(value >> 8);
			bytes[offset + 2] = (byte)(value >> 16);
			bytes[offset + 3] = (byte)(value >> 24);
		}

		private static void WriteInt16(byte[] bytes, int offset, short value)
		{
			bytes[offset] = (byte)value;
			bytes[offset + 1] = (byte)(value >> 8);
		}
	}
}
