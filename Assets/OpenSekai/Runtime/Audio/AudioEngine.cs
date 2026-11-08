// Ported from OurTaikoPlay (https://github.com/OurTaiko/OurTaikoPlay, GPLv3), Assets/OurTaiko/Runtime/Audio/AudioEngine.cs,
// with the WASAPI output path taken from MajdataPlay (https://github.com/LingFeng-bbben/MajdataPlay, GPLv3),
// Assets/Scripts/IO/AudioManager.cs. See NOTICE.md.
//
// Changes from the originals:
//  - Every backend renders through one global BASSmix mixer with a decode sub-mixer per AudioGroup, so group
//    volumes, pause and voice handling are identical across backends (OurTaiko played BassSimple streams directly).
//  - WASAPI pulls the mixer natively (MajdataPlay's WasapiProc_Bass) instead of through a managed callback, and the
//    mixer renders on one thread (MajdataPlay measured underruns with 8 threads during scene loads).
//  - The output latency is measured once per device so the song clock can use the heard position.
//  - Device option changes are applied on the next start instead of hot-swapping.
using System;
using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
using ManagedBass.Asio;
using ManagedBass.Wasapi;
#endif

namespace OpenSekai.Audio
{
	/// <summary>
	/// The application's single BASS output. When no device opens, BASS's "No Sound" device keeps streams and
	/// positions running silently.
	/// </summary>
	[DefaultExecutionOrder(-31000)]
	public sealed class AudioEngine : MonoBehaviour
	{
		/// <summary>BASSmix's BASS_ATTRIB_MIXER_THREADS, which ManagedBass does not name.</summary>
		private const ChannelAttribute MixerThreadsAttribute = (ChannelAttribute)86017;

		private const int GlobalMixerThreads = 1;

		private static readonly AudioGroup[] Groups = { AudioGroup.Bgm, AudioGroup.Se, AudioGroup.Voice };

		private readonly int[] groupMixers = new int[Groups.Length];
		private AudioOptions appliedOptions;
		private bool initialized;
		private bool nativeInitialized;
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
		private bool wasapiInitialized;
		private bool asioInitialized;
#endif

		public static AudioEngine Instance { get; private set; }

		public AudioBackend Backend { get; private set; } = AudioBackend.BassSimple;

		public string Diagnostics { get; private set; }

		/// <summary>Why the requested output could not open; null while it plays through a device.</summary>
		public string Failure { get; private set; }

		public bool Silent { get; private set; }

		/// <summary>False only when the BASS library itself cannot initialize; playback calls then do nothing.</summary>
		public bool Available { get; private set; }

		/// <summary>The global mixer. A playing stream on BassSimple, a decode stream pulled by WASAPI/ASIO.</summary>
		public int Mixer { get; private set; }

		public int OutputRate { get; private set; }

		/// <summary>Seconds between audio leaving the mixer and being heard. Used to align the song clock.</summary>
		public double OutputLatency { get; private set; }

		/// <summary>Increments whenever the output is torn down; voices and buffers from an older generation are dead.</summary>
		public int Generation { get; private set; }

		public bool HasPendingDeviceChanges => appliedOptions != null && !appliedOptions.SameDeviceSettings(AudioSettingsStore.Current);

		public static event Action BeforeOutputFreed;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instance = null;
			BeforeOutputFreed = null;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Bootstrap()
		{
			EnsureInstance();
		}

		public static AudioEngine EnsureInstance()
		{
			if (Instance != null)
			{
				return Instance;
			}

			var existing = FindAnyObjectByType<AudioEngine>();
			if (existing != null)
			{
				existing.Initialize();
				return existing;
			}

			return new GameObject(nameof(AudioEngine)).AddComponent<AudioEngine>();
		}

		private void Awake()
		{
			Initialize();
		}

		private void Initialize()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
			if (initialized)
			{
				return;
			}

			initialized = true;
			DontDestroyOnLoad(gameObject);
			appliedOptions = AudioSettingsStore.Current.Clone();
			InitializeOutput(appliedOptions);
			AudioSettingsStore.Changed += OnSettingsChanged;
		}

		private void Update()
		{
			GameTimeline.UpdateFrame();
		}

		public int GetGroupMixer(AudioGroup group)
		{
			return groupMixers[(int)group];
		}

		/// <summary>Applies volumes to the group sub-mixers.</summary>
		public void ApplyVolumes(SoundVolumes volumes)
		{
			if (!Available || volumes == null)
			{
				return;
			}

			foreach (var group in Groups)
			{
				var mixer = groupMixers[(int)group];
				if (mixer != 0)
				{
					Bass.ChannelSetAttribute(mixer, ChannelAttribute.Volume, volumes.Gain(group));
				}
			}
		}

		private void OnSettingsChanged(AudioOptions options)
		{
			ApplyVolumes(options.volume);
		}

		private void InitializeOutput(AudioOptions options)
		{
			Backend = AudioBackend.BassSimple;
			Failure = null;
			Silent = false;
			Available = false;
			var backend = options.Backend;
			var platform = SoundSettings.Platform;
			try
			{
#if UNITY_ANDROID && !UNITY_EDITOR
				Bass.Configure(Configuration.AndroidAAudio, options.androidAAudio);
#endif
				var updatePeriod = Math.Clamp(options.updatePeriodMs, 5, 100);
				Bass.Configure(Configuration.UpdatePeriod, updatePeriod);
				Bass.Configure(Configuration.PlaybackBufferLength, Math.Clamp(options.playbackBufferMs, updatePeriod + 1, 5000));
				Bass.Configure(Configuration.DevicePeriod, options.Period(platform));
				Bass.Configure(Configuration.DeviceBufferLength, options.Buffer(platform));
				Bass.Configure(Configuration.DevNonStop, true);
			}
			catch (Exception error)
			{
				// DllNotFoundException and friends: there is no BASS at all.
				Failure = error.Message;
				Diagnostics = "No audio output: " + Failure;
				Debug.LogError("[Audio] " + Diagnostics);
				return;
			}

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
			if (backend == AudioBackend.Automatic || backend == AudioBackend.BassWASAPI || backend == AudioBackend.BassASIO)
			{
				try
				{
					if (backend == AudioBackend.BassASIO)
					{
						InitAsio(options);
					}
					else
					{
						InitWasapi(options);
					}
				}
				catch (Exception error)
				{
					FreeNative();
					Failure = error.Message;
				}
			}
#else
			if (backend == AudioBackend.BassWASAPI || backend == AudioBackend.BassASIO)
			{
				Failure = backend + " requires Windows";
			}
#endif

			if (Mixer == 0)
			{
				try
				{
					InitBass(options, -1);
				}
				catch (Exception error)
				{
					FreeNative();
					Failure = Failure == null ? error.Message : Failure + "; " + error.Message;
					try
					{
						InitBass(options, Bass.NoSoundDevice);
						Silent = true;
					}
					catch (Exception silent)
					{
						FreeNative();
						Failure += "; " + silent.Message;
					}
				}
			}

			if (!Available)
			{
				Diagnostics = "No audio output: " + Failure;
				Debug.LogError("[Audio] " + Diagnostics);
				return;
			}

			CreateGroupMixers();
			ApplyVolumes(options.volume);
			Diagnostics = (Silent ? "Silent (no device)" : Backend.ToString())
			              + $"; {OutputRate} Hz; latency {OutputLatency * 1000:0.0} ms"
			              + $"; device period {options.Period(platform)} ms; buffer {options.Buffer(platform)} ms requested";
			if (Failure != null)
			{
				Diagnostics += "; fallback: " + Failure;
			}

			if (Silent)
			{
				Debug.LogWarning("[Audio] " + Diagnostics);
			}
			else
			{
				Debug.Log("[Audio] " + Diagnostics);
			}
		}

		private static void Check(bool success, string operation)
		{
			if (!success)
			{
				throw new InvalidOperationException(operation + ": " + Bass.LastError);
			}
		}

		private void InitBass(AudioOptions options, int device)
		{
			Check(Bass.Init(device, options.Rate, DeviceInitFlags.Latency), "BASS device initialization");
			nativeInitialized = true;
			var info = Bass.Info;
			OutputRate = info.SampleRate > 0 ? info.SampleRate : options.Rate;
			OutputLatency = device == Bass.NoSoundDevice ? 0 : Math.Max(0, info.Latency) / 1000.0;

			// A playing, unbuffered mixer renders on each device update, like OurTaiko's unbuffered streams.
			Mixer = BassMix.CreateMixerStream(OutputRate, 2, BassFlags.Float | BassFlags.MixerNonStop);
			Check(Mixer != 0, "BASS mixer");
			Bass.ChannelSetAttribute(Mixer, ChannelAttribute.Buffer, 0);
			Bass.ChannelSetAttribute(Mixer, MixerThreadsAttribute, GlobalMixerThreads);
			Check(Bass.ChannelPlay(Mixer), "Start BASS mixer");
			Backend = AudioBackend.BassSimple;
			Available = true;
		}

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
		private void InitWasapi(AudioOptions options)
		{
			Check(Bass.Init(Bass.NoSoundDevice, options.Rate), "BASS decode device");
			nativeInitialized = true;
			var combinations = new[] { (true, true), (true, false), (false, true), (false, false) };
			var attempt = false;
			foreach (var (exclusive, raw) in combinations)
			{
				if ((exclusive, raw) == (options.wasapiExclusive, options.wasapiRaw))
				{
					attempt = true;
				}

				if (!attempt)
				{
					continue;
				}

				var flags = WasapiInitFlags.EventDriven;
				if (exclusive)
				{
					flags |= WasapiInitFlags.Exclusive | (options.wasapiAsync ? WasapiInitFlags.Async : 0);
				}

				if (raw)
				{
					flags |= WasapiInitFlags.Raw;
				}

				var buffer = exclusive ? Mathf.Clamp(options.wasapiBufferSeconds, 0.003f, 0.5f) : 0f;
				var period = options.wasapiPeriodSeconds > 0 ? Mathf.Clamp(options.wasapiPeriodSeconds, 0.001f, 0.1f) : 0f;
				if (TryInitWasapiOutput(flags, buffer, period))
				{
					wasapiInitialized = true;
					break;
				}
			}

			if (!wasapiInitialized)
			{
				throw new InvalidOperationException("WASAPI exclusive/shared initialization failed: " + Bass.LastError);
			}

			Check(BassWasapi.GetInfo(out var info), "WASAPI format");
			OutputRate = info.Frequency;
			var bytesPerSecond = (double)info.Frequency * info.Channels * sizeof(float);
			OutputLatency = bytesPerSecond > 0 ? info.BufferLength / bytesPerSecond : 0;
			Check(BassWasapi.Start(), "Start WASAPI");
			Backend = AudioBackend.BassWASAPI;
			Available = true;
		}

		/// <summary>
		/// MajdataPlay: initializes WASAPI in one mode and lets BASSWASAPI pull the mixer natively. A managed output
		/// callback would register the output thread with the managed runtime, whose garbage collections suspend it.
		/// The mode's format is probed first with a push-mode initialization that is freed again.
		/// </summary>
		private bool TryInitWasapiOutput(WasapiInitFlags flags, float buffer, float period)
		{
			var probeFlags = flags & ~WasapiInitFlags.EventDriven;
			if (!BassWasapi.InitEx(BassWasapi.DefaultDevice, 0, 0, probeFlags, buffer, period, BassWasapi.WasapiProc_Push))
			{
				return false;
			}

			var hasInfo = BassWasapi.GetInfo(out var info);
			BassWasapi.Free();
			if (!hasInfo)
			{
				return false;
			}

			var mixer = BassMix.CreateMixerStream(info.Frequency, info.Channels, BassFlags.MixerNonStop | BassFlags.Decode | BassFlags.Float);
			if (mixer == 0)
			{
				return false;
			}

			Bass.ChannelSetAttribute(mixer, ChannelAttribute.Buffer, 0);
			Bass.ChannelSetAttribute(mixer, MixerThreadsAttribute, GlobalMixerThreads);
			if (!BassWasapi.InitEx(BassWasapi.DefaultDevice, info.Frequency, info.Channels, flags, buffer, period, BassWasapi.WasapiProc_Bass, (IntPtr)mixer))
			{
				Bass.StreamFree(mixer);
				return false;
			}

			Mixer = mixer;
			return true;
		}

		private void InitAsio(AudioOptions options)
		{
			Check(Bass.Init(Bass.NoSoundDevice, options.Rate), "BASS decode device");
			nativeInitialized = true;
			if (!BassAsio.Init(options.asioDevice, AsioInitFlags.Thread))
			{
				throw new InvalidOperationException("ASIO initialization: " + BassAsio.LastError);
			}

			asioInitialized = true;
			BassAsio.Rate = options.Rate;
			OutputRate = (int)BassAsio.Rate;
			Mixer = BassMix.CreateMixerStream(OutputRate, BassAsio.Info.Outputs, BassFlags.Float | BassFlags.Decode | BassFlags.MixerNonStop);
			Check(Mixer != 0, "ASIO mixer");
			Bass.ChannelSetAttribute(Mixer, ChannelAttribute.Buffer, 0);
			Bass.ChannelSetAttribute(Mixer, MixerThreadsAttribute, GlobalMixerThreads);
			BassAsio.ChannelEnableBass(false, 0, Mixer, true);
			BassAsio.ChannelSetFormat(false, 0, AsioSampleFormat.Float);
			BassAsio.ChannelJoin(false, 1, 0);
			BassAsio.ChannelSetFormat(false, 1, AsioSampleFormat.Float);
			if (!BassAsio.Start(Math.Max(0, options.asioBufferSamples)))
			{
				throw new InvalidOperationException("ASIO stereo output: " + BassAsio.LastError);
			}

			OutputLatency = OutputRate > 0 ? BassAsio.GetLatency(false) / (double)OutputRate : 0;
			Backend = AudioBackend.BassASIO;
			Available = true;
		}
#endif

		private void CreateGroupMixers()
		{
			var outputChannels = Bass.ChannelGetInfo(Mixer).Channels;
			var matrix = outputChannels != 2 ? CreateMixingMatrix(outputChannels) : null;
			foreach (var group in Groups)
			{
				var mixer = BassMix.CreateMixerStream(OutputRate, 2, BassFlags.Float | BassFlags.Decode | BassFlags.MixerNonStop);
				Check(mixer != 0, group + " mixer");
				Bass.ChannelSetAttribute(mixer, ChannelAttribute.Buffer, 0);
				Check(BassMix.MixerAddChannel(Mixer, mixer, matrix != null ? BassFlags.MixerChanMatrix : BassFlags.Default), "Attach " + group + " mixer");
				if (matrix != null)
				{
					Check(BassMix.ChannelSetMatrix(mixer, matrix), "Set " + group + " matrix");
				}

				groupMixers[(int)group] = mixer;
			}
		}

		/// <summary>Maps a stereo source onto <paramref name="channels"/> output channels (rows = outputs).</summary>
		public static float[,] CreateMixingMatrix(int channels)
		{
			var matrix = new float[channels, 2];
			if (channels == 1)
			{
				matrix[0, 0] = matrix[0, 1] = 0.5f;
				return matrix;
			}

			for (var row = 0; row < channels; row++)
			{
				matrix[row, row % 2] = 1;
			}

			if (channels == 3)
			{
				matrix[1, 0] = matrix[1, 1] = 0.5f;
				matrix[2, 0] = 0;
				matrix[2, 1] = 1;
			}

			return matrix;
		}

		private void OnApplicationPause(bool paused)
		{
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
			if (!Available)
			{
				return;
			}

			Bass.GlobalStreamVolume = paused ? 0 : 10000;
#endif
		}

		private void FreeNative()
		{
			Available = false;
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
			if (wasapiInitialized)
			{
				BassWasapi.Stop();
				BassWasapi.Free();
				wasapiInitialized = false;
			}

			if (asioInitialized)
			{
				BassAsio.Stop();
				BassAsio.Free();
				asioInitialized = false;
			}
#endif
			for (var i = 0; i < groupMixers.Length; i++)
			{
				if (groupMixers[i] != 0)
				{
					Bass.StreamFree(groupMixers[i]);
					groupMixers[i] = 0;
				}
			}

			if (Mixer != 0)
			{
				Bass.StreamFree(Mixer);
				Mixer = 0;
			}

			if (nativeInitialized)
			{
				Bass.Free();
				nativeInitialized = false;
			}
		}

		private void OnDestroy()
		{
			if (Instance != this)
			{
				return;
			}

			AudioSettingsStore.Changed -= OnSettingsChanged;
			try
			{
				BeforeOutputFreed?.Invoke();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}

			Generation++;
			SoundBuffer.ReleaseAll();
			FreeNative();
			Instance = null;
		}
	}
}
