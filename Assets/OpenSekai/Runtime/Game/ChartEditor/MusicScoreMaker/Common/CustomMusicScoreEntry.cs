using System;
using System.IO;
using System.Threading;
using CP;
using Cysharp.Threading.Tasks;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using UnityEngine;

namespace Sekai.MusicScoreMaker.Common
{
	public sealed class CustomMusicScoreEntry
	{
		public CustomMusicScoreEntry(string rootDirectory, CustomMusicScoreManifest manifest)
		{
			RootDirectory = rootDirectory;
			Manifest = manifest ?? new CustomMusicScoreManifest();
			Manifest.Normalize();
			MusicId = CreateStableMusicId(Manifest.id);
			AudioCueName = "custom_" + Manifest.id;
		}

		public string RootDirectory { get; }

		public CustomMusicScoreManifest Manifest { get; }

		public int MusicId { get; }

		public string AudioCueName { get; }

		public long AudioLengthMs { get; private set; }

		public string ManifestPath => Path.Combine(RootDirectory, CustomMusicScoreStorage.ManifestFileName);

		public string ScorePath => ResolveEntryPath(Manifest.scoreFileName);

		public string AudioPath => ResolveEntryPath(Manifest.audioFileName);

		public string JacketPath => ResolveEntryPath(Manifest.jacketFileName);

		public string VideoPath => ResolveEntryPath(Manifest.videoFileName);

		public int MusicDurationSec
		{
			get
			{
				if (Manifest.secForMusicScoreMaker > 0)
				{
					return Manifest.secForMusicScoreMaker;
				}
				if (AudioLengthMs > 0L)
				{
					float seconds = AudioLengthMs / 1000f - Manifest.fillerSec;
					return Mathf.Max(1, Mathf.CeilToInt(seconds));
				}
				return 120;
			}
		}

		public MusicScoreMakerData LoadScore()
		{
			if (!File.Exists(ScorePath))
			{
				return null;
			}

			MusicScoreMakerData data = DeepCopyHelper.FromJson<MusicScoreMakerData>(File.ReadAllText(ScorePath));
			if (data == null)
			{
				return null;
			}
			data.MigrateToCurrentVersion();
			data.InitializeIdCount();
			data.MusicId = MusicId;
			return data;
		}

		public void SaveScore(MusicScoreMakerData data)
		{
			if (data == null)
			{
				return;
			}

			Directory.CreateDirectory(RootDirectory);
			data.MusicId = MusicId;
			File.WriteAllText(ScorePath, DeepCopyHelper.ToJsonString(data));
		}

		public async UniTask<bool> RegisterAudioAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (!File.Exists(AudioPath))
			{
				LogUtility.LogWarning("Custom music audio not found. path:{0}", AudioPath);
				return false;
			}

			if (!IsSupportedAudioFile(AudioPath))
			{
				LogUtility.LogWarning("Unsupported custom music audio format. path:{0}", AudioPath);
				return false;
			}

			// BASS decodes the encoded file directly (no Unity AudioClip decode and PCM copy).
			await UniTask.Yield(token);
			try
			{
				long sourceTicks = File.GetLastWriteTimeUtc(AudioPath).Ticks;
				if (!SoundManager.Instance.RegisterExternalAudioFile(AudioCueName, AudioPath, sourceTicks))
				{
					return false;
				}

				SoundCueInfo[] cueInfos = SoundManager.Instance.GetCueInfos(AudioCueName);
				AudioLengthMs = cueInfos.Length > 0 ? cueInfos[0].length : 0L;
				return true;
			}
			catch (Exception exception)
			{
				LogUtility.LogWarning("Failed to register custom music audio. path:{0} error:{1}", AudioPath, exception.Message);
				return false;
			}
		}

		private static bool IsSupportedAudioFile(string path)
		{
			switch (Path.GetExtension(path)?.ToLowerInvariant())
			{
				case ".ogg":
				case ".mp3":
				case ".wav":
				case ".opus":
					return true;
				default:
					return false;
			}
		}

		private string ResolveEntryPath(string fileName)
		{
			string safeFileName = Path.GetFileName(string.IsNullOrEmpty(fileName) ? string.Empty : fileName);
			return string.IsNullOrEmpty(safeFileName) ? RootDirectory : Path.Combine(RootDirectory, safeFileName);
		}

		private static int CreateStableMusicId(string id)
		{
			unchecked
			{
				uint hash = 2166136261U;
				string source = string.IsNullOrEmpty(id) ? "custom" : id.ToLowerInvariant();
				for (int i = 0; i < source.Length; i++)
				{
					hash ^= source[i];
					hash *= 16777619U;
				}

				int value = (int)(hash & 0x7FFFFFFF);
				return value == 0 ? -1 : -value;
			}
		}
	}
}
