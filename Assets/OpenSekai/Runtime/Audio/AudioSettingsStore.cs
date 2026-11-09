using System;
using System.IO;
using UnityEngine;

namespace OpenSekai.Audio
{
	/// <summary>
	/// Persists <see cref="AudioOptions"/> as JSON in <see cref="OpenSekai.Platform.GamePaths.Root"/> (persistentDataPath, or beside the game on desktop).
	/// Replaces OurTaikoPlay's SettingManager for the audio subset.
	/// Device options are applied when the engine starts; volumes apply immediately.
	/// </summary>
	public static class AudioSettingsStore
	{
		private const string FileName = "audio_settings.json";
		private static AudioOptions current;

		public static event Action<AudioOptions> Changed;

		private static string FilePath => Path.Combine(OpenSekai.Platform.GamePaths.Root, FileName);

		public static AudioOptions Current => current ??= Load();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			current = null;
			Changed = null;
		}

		/// <summary>Replaces the stored options, saves them and notifies listeners.</summary>
		public static void Set(AudioOptions options)
		{
			if (options == null)
			{
				throw new ArgumentNullException(nameof(options));
			}

			current = options.Clone();
			Save(current);
			Changed?.Invoke(current);
		}

		private static AudioOptions Load()
		{
			try
			{
				if (File.Exists(FilePath))
				{
					var options = JsonUtility.FromJson<AudioOptions>(File.ReadAllText(FilePath));
					if (options != null)
					{
						options.volume ??= new SoundVolumes();
						return options;
					}
				}
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Audio] Could not read audio settings, using defaults: " + exception.Message);
			}

			return new AudioOptions();
		}

		private static void Save(AudioOptions options)
		{
			try
			{
				File.WriteAllText(FilePath, JsonUtility.ToJson(options, true));
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Audio] Could not save audio settings: " + exception.Message);
			}
		}
	}
}
