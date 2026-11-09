using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Sekai
{
	public class ApplicationLocalSettings
	{
		private const string StorageFileName = "ApplicationLocalSettings.json";

		private static ApplicationLocalSettings cachedStorage;

		public class VolumeSettings
		{
			public float Bgm = 1f;
			public float Se = 1f;
			public float Voice = 1f;
		}

		/// <summary>
		/// The official option dialog's システム tab. OpenSekai stores these but nothing reads them yet
		/// (placeholders; see AGENTS.md).
		/// </summary>
		public class SystemOptionSettings
		{
			/// <summary>デフォルト楽曲: 0 = セカイver., 1 = バーチャル・シンガーver.</summary>
			public int DefaultVocalType;
			/// <summary>画面固定.</summary>
			public bool LockScreenOrientation = true;
			/// <summary>ライブ前設定確認.</summary>
			public bool ConfirmSettingsBeforeLive;
			/// <summary>オンライン表示.</summary>
			public bool ShowOnlineStatus = true;
			/// <summary>ライブボーナス全回復通知.</summary>
			public bool NotifyLiveBonusFullRecovery = true;
		}

		public VolumeSettings SystemVolume = new VolumeSettings();
		public VolumeSettings LiveVolume;
		public bool? FullscreenEnabled;
		public SystemOptionSettings SystemOptions = new SystemOptionSettings();

		public static ApplicationLocalSettings LoadFromStorage()
		{
			if (cachedStorage != null)
			{
				return cachedStorage;
			}

			ApplicationLocalSettings settings = null;
			string path = StoragePath;
			if (File.Exists(path))
			{
				try
				{
					settings = JsonConvert.DeserializeObject<ApplicationLocalSettings>(File.ReadAllText(path));
				}
				catch (Exception exception)
				{
					Debug.LogWarningFormat("ApplicationLocalSettings could not be loaded. path:{0} error:{1}", path, exception.Message);
				}
			}

			cachedStorage = settings ?? new ApplicationLocalSettings();
			cachedStorage.EnsureDefaults();
			return cachedStorage;
		}

		public static void SaveToStorage(ApplicationLocalSettings settings)
		{
			cachedStorage = settings ?? new ApplicationLocalSettings();
			cachedStorage.EnsureDefaults();
			string path = StoragePath;
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, JsonConvert.SerializeObject(cachedStorage, Formatting.Indented));
			}
			catch (Exception exception)
			{
				Debug.LogWarningFormat("ApplicationLocalSettings could not be saved. path:{0} error:{1}", path, exception.Message);
			}
		}

		public VolumeSettings SetupLiveVolume()
		{
			LiveVolume = new VolumeSettings();
			return LiveVolume;
		}

		private static string StoragePath => Path.Combine(OpenSekai.Platform.GamePaths.Root, StorageFileName);

		private void EnsureDefaults()
		{
			SystemVolume ??= new VolumeSettings();
			LiveVolume ??= new VolumeSettings();
			SystemOptions ??= new SystemOptionSettings();
		}
	}
}
