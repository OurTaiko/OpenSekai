// Ported from OurTaikoPlay (https://github.com/OurTaiko/OurTaikoPlay, GPLv3),
// Assets/OurTaiko/Runtime/Audio/AudioOptions.cs and SoundSettings.cs. See NOTICE.md.
using System;
using UnityEngine;

namespace OpenSekai.Audio
{
	/// <summary>
	/// How decoded and mixed BASS audio reaches the device. BassSimple is BASS's own cross-platform output
	/// (through the system mixer); BassWASAPI and BassASIO are Windows-only.
	/// </summary>
	public enum AudioBackend
	{
		Automatic,
		BassSimple,
		BassWASAPI,
		BassASIO
	}

	/// <summary>Mix groups; each one is a sub-mixer with its own volume.</summary>
	public enum AudioGroup
	{
		Bgm,
		Se,
		Voice
	}

	public enum SoundPlatform
	{
		Desktop,
		Windows,
		Android,
		IOS
	}

	[Serializable]
	public sealed class SoundVolumes
	{
		public float master = 1f;
		public float bgm = 1f;
		public float se = 1f;
		public float voice = 1f;

		private static float Safe(float value)
		{
			return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
		}

		public float Gain(AudioGroup group)
		{
			var groupVolume = group switch
			{
				AudioGroup.Bgm => bgm,
				AudioGroup.Voice => voice,
				_ => se
			};
			return Safe(master) * Safe(groupVolume);
		}
	}

	[Serializable]
	public sealed class AudioOptions
	{
		public AudioBackend backend = AudioBackend.Automatic;
		public int sampleRate = 48000;
		public SoundVolumes volume = new SoundVolumes();
		public int updatePeriodMs = 10;
		public int playbackBufferMs = 500;

		/// <summary>0 = platform default: 8 ms on mobile, 16 ms on desktop (MajdataPlay defaults).</summary>
		public int devicePeriodMs;

		/// <summary>0 = platform default: 16 ms on Android, 32 ms on iOS, 64 ms on desktop.</summary>
		public int deviceBufferMs;

		public bool androidAAudio = true;
		public bool wasapiExclusive = true;
		public bool wasapiRaw = true;
		public bool wasapiAsync = true;

		/// <summary>MajdataPlay default: 20 ms buffer, 5 ms period in exclusive mode.</summary>
		public float wasapiBufferSeconds = 0.02f;

		public float wasapiPeriodSeconds = 0.005f;
		public int asioDevice;
		public int asioBufferSamples;

		public AudioBackend Backend => Enum.IsDefined(typeof(AudioBackend), backend) ? backend : AudioBackend.Automatic;

		public int Rate => sampleRate >= 8000 && sampleRate <= 192000 ? sampleRate : 48000;

		private static bool Mobile(SoundPlatform platform)
		{
			return platform == SoundPlatform.Android || platform == SoundPlatform.IOS;
		}

		public int Period(SoundPlatform platform)
		{
			return Math.Clamp(devicePeriodMs == 0 ? (Mobile(platform) ? 8 : 16) : devicePeriodMs, 1, 100);
		}

		public int Buffer(SoundPlatform platform)
		{
			var buffer = deviceBufferMs == 0
				? (platform == SoundPlatform.Android ? 16 : Mobile(platform) ? 32 : 64)
				: deviceBufferMs;
			return Math.Clamp(buffer, Period(platform) * 2, 1000);
		}

		public bool SameDeviceSettings(AudioOptions other)
		{
			return other != null
			       && backend == other.backend
			       && sampleRate == other.sampleRate
			       && updatePeriodMs == other.updatePeriodMs
			       && playbackBufferMs == other.playbackBufferMs
			       && devicePeriodMs == other.devicePeriodMs
			       && deviceBufferMs == other.deviceBufferMs
			       && androidAAudio == other.androidAAudio
			       && wasapiExclusive == other.wasapiExclusive
			       && wasapiRaw == other.wasapiRaw
			       && wasapiAsync == other.wasapiAsync
			       && wasapiBufferSeconds.Equals(other.wasapiBufferSeconds)
			       && wasapiPeriodSeconds.Equals(other.wasapiPeriodSeconds)
			       && asioDevice == other.asioDevice
			       && asioBufferSamples == other.asioBufferSamples;
		}

		public AudioOptions Clone()
		{
			var clone = (AudioOptions)MemberwiseClone();
			clone.volume = new SoundVolumes
			{
				master = volume?.master ?? 1f,
				bgm = volume?.bgm ?? 1f,
				se = volume?.se ?? 1f,
				voice = volume?.voice ?? 1f
			};
			return clone;
		}
	}

	public static class SoundSettings
	{
		public static SoundPlatform Platform
		{
			get
			{
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
				return SoundPlatform.Windows;
#elif UNITY_ANDROID && !UNITY_EDITOR
				return SoundPlatform.Android;
#elif UNITY_IOS && !UNITY_EDITOR
				return SoundPlatform.IOS;
#else
				return SoundPlatform.Desktop;
#endif
			}
		}
	}
}
