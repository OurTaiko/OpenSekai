using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenSekai.Audio;
using Sekai.Live;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sekai
{
	/// <summary>
	/// Game-facing sound API. Plays ACB cue sheets (decoded by the managed HCA decoder) and external audio files
	/// through the BASS engine (<see cref="AudioEngine"/>). The public surface matches the former CRI implementation.
	/// </summary>
	public class SoundManager
	{
		public enum BundleWorkingStatus
		{
			None = 0,
			Loading = 1,
			Loaded = 2
		}

		public class AcbDataMemoryContainer
		{
		}

		private sealed class IngameTrack
		{
			public uint RequestId;
			public string CueName;
			public CueSheet Sheet;
			public ExternalAudioEntry External;
			public Task DecodeTask;
			public List<SoundVoice> Voices;
			public double StartSeconds;
			public bool Loop;
			public bool StartRequested;
			public bool Paused;
			public readonly SongClock Clock = new SongClock();
		}

		private sealed class SePlayback
		{
			public List<SoundVoice> Voices;
			public bool Ingame;
		}

		private const string MenuCommonBundleName = "sound/menu/menu_common";
		private const string MenuCommonCueSheetName = "MenuCommon";
		private const string MenuCommonAcbFileName = "MenuCommon.acb";
		private const string MenuCommonBuiltInCueSheetName = "MenuCommon_Built_in";
		private const string MenuCommonBuiltInAcbFileName = "MenuCommon_Built_in.acb";
		private const string SoundBundleBuildDataAssetName = "SoundBundleBuildData";
		private const string LiveDefaultSoundBundleName = "live/sound/live_se/default";
		private const string LiveDefaultCommonAcbFileName = "se_live_common.acb.bytes";
		private const string LiveTapSeBundleNamePrefix = "live/tap_se";
		private const string LiveTapSeCustom01BundleName = "live/tap_se/custom01";
		private const string LiveMusicBundleNameBase = "music/long/{0}";
		private const int SePlaybackPruneThreshold = 64;
		private static readonly string[] LiveTapCueNames =
		{
			"se_live_long",
			"se_live_long_critical",
			"se_live_perfect",
			"se_live_tap",
			"se_live_great",
			"se_live_good",
			"se_live_critical",
			"se_live_connect",
			"se_live_connect_critical",
			"se_live_flick",
			"se_live_flick_critical",
			"se_live_trace",
			"se_live_trace_critical"
		};

		private static readonly HashSet<string> LiveTapCueNameSet = new HashSet<string>(LiveTapCueNames, StringComparer.OrdinalIgnoreCase);

		private static readonly SoundManager instance = new SoundManager();
		private readonly HashSet<string> loadedBundles = new HashSet<string>();
		private readonly List<string> loadedBundleSearchOrder = new List<string>();
		private readonly Dictionary<string, CueSheet> acbByBundleName = new Dictionary<string, CueSheet>();
		private readonly Dictionary<string, ExternalAudioEntry> externalAudioByKey = new Dictionary<string, ExternalAudioEntry>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, string> acbFileNameByBundleName = new Dictionary<string, string>
		{
			{ MenuCommonBundleName, MenuCommonAcbFileName },
			{ MenuCommonCueSheetName, MenuCommonAcbFileName },
			{ MenuCommonBuiltInCueSheetName, MenuCommonBuiltInAcbFileName }
		};
		private readonly Dictionary<string, string> convertSeTable = new Dictionary<string, string>();
		private readonly HashSet<string> warnedMissingBundles = new HashSet<string>();
		private readonly HashSet<string> warnedMissingCues = new HashSet<string>();
		private readonly Dictionary<uint, SePlayback> sePlaybacks = new Dictionary<uint, SePlayback>();
		private readonly SoundVolumes volumes = new SoundVolumes();
		private IngameTrack ingameTrack;
		private uint currentIngamePlaybackRequestId;
		private uint nextSePlaybackId;
		private bool initialized;
		private bool defaultSoundBundlesRequested;
		private bool appPaused;

		public static SoundManager Instance => instance;

		public void Initialize()
		{
			if (initialized)
			{
				return;
			}

			var engine = AudioEngine.EnsureInstance();
			engine.ApplyVolumes(volumes);
			AudioEngine.BeforeOutputFreed -= OnOutputFreed;
			AudioEngine.BeforeOutputFreed += OnOutputFreed;
			EnsureDefaultSoundBundlesLoaded();
			initialized = true;
		}

		private void OnOutputFreed()
		{
			StopAll();
		}

		public void SetupVolume(float master, float bgm, float se, float voice)
		{
			volumes.master = Mathf.Clamp01(master);
			volumes.bgm = Mathf.Clamp01(bgm);
			volumes.se = Mathf.Clamp01(se);
			volumes.voice = Mathf.Clamp01(voice);
			AudioEngine.Instance?.ApplyVolumes(volumes);
		}

		public bool IsLoadedSoundBundle(string bundleName)
		{
			return !string.IsNullOrEmpty(bundleName)
				&& (loadedBundles.Contains(bundleName) || acbByBundleName.ContainsKey(bundleName) || externalAudioByKey.ContainsKey(bundleName));
		}

		public bool IsExternalAudioRegistered(string key)
		{
			return TryResolveExternalAudio(key, out _);
		}

		public void LoadSoundBundle(string bundleName, bool isResident)
		{
			if (string.IsNullOrEmpty(bundleName) || loadedBundles.Contains(bundleName) || acbByBundleName.ContainsKey(bundleName) || externalAudioByKey.ContainsKey(bundleName))
			{
				return;
			}

			if (TryRegisterLoadedBundleAlias(bundleName))
			{
				return;
			}

			if (!defaultSoundBundlesRequested && !IsDefaultSoundBundle(bundleName))
			{
				EnsureDefaultSoundBundlesLoaded();
				if (acbByBundleName.ContainsKey(bundleName))
				{
					return;
				}
			}

			List<string> acbFileNames = GetAcbFileNameCandidates(bundleName);
			if (TryLoadSoundBundleFromBuildData(bundleName, acbFileNames))
			{
				return;
			}

			foreach (string acbFileName in acbFileNames)
			{
				CueSheet acb = null;
				if (ShouldPreferAssetBundleAcb(bundleName))
				{
					byte[] acbBytes = TryLoadAcbBytesFromAssetBundle(bundleName, acbFileName);
					acb = LoadCueSheet(acbFileName, acbBytes);
					if (acb == null)
					{
						acb = TryLoadAcbFromStreamingAssets(acbFileName);
					}
				}
				else
				{
					acb = TryLoadAcbFromStreamingAssets(acbFileName);
					if (acb == null)
					{
						byte[] acbBytes = TryLoadAcbBytesFromAssetBundle(bundleName, acbFileName);
						acb = LoadCueSheet(acbFileName, acbBytes);
					}
				}
				if (acb == null || !acb.IsAvailable)
				{
					continue;
				}

				acbByBundleName[bundleName] = acb;
				loadedBundles.Add(bundleName);
				AddLoadedBundleSearchOrder(bundleName);
				return;
			}

			LogMissingBundle(bundleName, string.Join(", ", acbFileNames.ToArray()));
		}

		public void Load(string cueSheetName, string acbFileName)
		{
			if (string.IsNullOrEmpty(cueSheetName) || string.IsNullOrEmpty(acbFileName))
			{
				return;
			}

			acbFileNameByBundleName[cueSheetName] = acbFileName;
			LoadSoundBundle(cueSheetName, true);
		}

		/// <summary>Registers in-memory audio (any format BASS decodes, e.g. WAV) under <paramref name="key"/>.</summary>
		public bool RegisterExternalAudioData(string key, byte[] wavData, int channels, int sampleRate, long lengthMs, long sourceVersion)
		{
			if (string.IsNullOrEmpty(key) || wavData == null || wavData.Length <= 44)
			{
				Debug.LogWarningFormat(
					"External audio could not be registered. key:{0} bytes:{1} channels:{2} sampleRate:{3}",
					key,
					wavData?.Length ?? 0,
					channels,
					sampleRate);
				return false;
			}

			return RegisterExternalAudio(key, SoundBuffer.FromEncoded(wavData), lengthMs, sourceVersion);
		}

		/// <summary>
		/// Registers an audio file (wav/mp3/ogg/opus, aac where supported) under <paramref name="key"/>. BASS decodes the
		/// encoded file on the fly; it is not converted to PCM up front.
		/// </summary>
		public bool RegisterExternalAudioFile(string key, string path, long sourceVersion)
		{
			if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				Debug.LogWarningFormat("External audio file could not be registered. key:{0} path:{1}", key, path);
				return false;
			}

			if (TryResolveExternalAudio(key, out ExternalAudioEntry existing) && existing.SourceVersion == sourceVersion && existing.SourcePath == path)
			{
				return true;
			}

			SoundBuffer buffer;
			try
			{
				buffer = SoundBuffer.FromFile(path);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return false;
			}

			Initialize();
			double lengthSeconds = buffer.ProbeLength();
			if (lengthSeconds <= 0)
			{
				Debug.LogWarningFormat("External audio file could not be decoded. key:{0} path:{1}", key, path);
				buffer.Dispose();
				return false;
			}

			return RegisterExternalAudio(key, buffer, (long)(lengthSeconds * 1000), sourceVersion, path);
		}

		private bool RegisterExternalAudio(string key, SoundBuffer buffer, long lengthMs, long sourceVersion, string sourcePath = null)
		{
			ReleaseExternalAudioData();
			ExternalAudioEntry entry = new ExternalAudioEntry
			{
				Key = key,
				Buffer = buffer,
				SourcePath = sourcePath,
				LengthMs = Math.Max(0L, lengthMs),
				SourceVersion = sourceVersion
			};
			RegisterExternalAudioEntry(key, entry);

			string liveMusicBundleName = GetLiveMusicBundleNameCandidate(key);
			if (!string.IsNullOrEmpty(liveMusicBundleName))
			{
				RegisterExternalAudioEntry(liveMusicBundleName, entry);
			}
			return IsExternalAudioRegistered(key);
		}

		public bool ExistsCueName(string cueName)
		{
			return TryResolveExternalAudio(cueName, out _) || ResolveCueName(cueName, out _) != null;
		}

		public SoundCueInfo[] GetCueInfos(string bundleName)
		{
			if (string.IsNullOrEmpty(bundleName))
			{
				return Array.Empty<SoundCueInfo>();
			}

			if (externalAudioByKey.TryGetValue(bundleName, out ExternalAudioEntry externalAudio))
			{
				return new[]
				{
					new SoundCueInfo
					{
						length = externalAudio.LengthMs
					}
				};
			}

			LoadSoundBundle(bundleName, true);
			if (!acbByBundleName.TryGetValue(bundleName, out CueSheet acb) || acb == null)
			{
				return Array.Empty<SoundCueInfo>();
			}

			return GetCueInfosWithLength(acb);
		}

		public bool IsAudioSyncedUnityTimerEnabled => ingameTrack != null && ingameTrack.Voices != null && ingameTrack.Voices.Count > 0;

		/// <summary>
		/// Prepares the in-game music paused at <paramref name="startTime"/> seconds. ACB music is decoded on a worker
		/// thread; poll <see cref="IsIngamePlaybackReady"/> before <see cref="ResumePreparedPlaybackIngame"/>.
		/// </summary>
		public uint PrepareIngameBGM(string cueName, float startTime, Action callback = null, bool loop = false)
		{
			uint requestId = AllocateIngamePlaybackRequestId();
			StopIngame();
			Initialize();

			IngameTrack track = new IngameTrack
			{
				RequestId = requestId,
				CueName = cueName,
				StartSeconds = Mathf.Max(0f, startTime),
				Loop = loop
			};

			if (TryResolveExternalAudio(cueName, out ExternalAudioEntry externalAudio))
			{
				track.External = externalAudio;
				ingameTrack = track;
				CreateIngameVoices(track);
				callback?.Invoke();
				return requestId;
			}

			if (IsCustomExternalAudioKey(cueName))
			{
				LogMissingCue(cueName);
				callback?.Invoke();
				return requestId;
			}

			if (TryResolveIngameCue(cueName, out CueSheet acb, out string resolvedCueName))
			{
				track.Sheet = acb;
				track.CueName = resolvedCueName;
				track.DecodeTask = acb.DecodeCueAsync(resolvedCueName);
				ingameTrack = track;
				callback?.Invoke();
				return requestId;
			}

			if (!string.IsNullOrEmpty(cueName))
			{
				LogMissingCue(cueName);
			}
			callback?.Invoke();
			return requestId;
		}

		public bool IsIngamePlaybackReady(uint requestId)
		{
			IngameTrack track = ingameTrack;
			if (requestId == 0 || track == null || track.RequestId != requestId)
			{
				return true;
			}

			if (track.Voices != null)
			{
				return true;
			}

			if (track.DecodeTask != null && !track.DecodeTask.IsCompleted)
			{
				return false;
			}

			if (track.DecodeTask != null && track.DecodeTask.IsFaulted)
			{
				Debug.LogException(track.DecodeTask.Exception?.GetBaseException());
			}

			CreateIngameVoices(track);
			return true;
		}

		private void CreateIngameVoices(IngameTrack track)
		{
			if (track.Voices != null)
			{
				return;
			}

			if (track.External != null)
			{
				track.Voices = new List<SoundVoice>();
				SoundVoice voice = SoundVoice.Create(track.External.Buffer, AudioGroup.Bgm, track.Loop);
				if (voice != null)
				{
					track.Voices.Add(voice);
				}
			}
			else if (track.Sheet != null)
			{
				track.Voices = track.Sheet.CreateVoices(track.CueName, AudioGroup.Bgm, 1f, track.Loop, false);
			}
			else
			{
				track.Voices = new List<SoundVoice>();
			}

			foreach (SoundVoice voice in track.Voices)
			{
				voice.Seek(track.StartSeconds);
			}

			track.Clock.Seek(track.StartSeconds);
			if (track.StartRequested)
			{
				StartIngameVoices(track, track.StartSeconds);
			}
		}

		public void ResumePreparedPlaybackIngame()
		{
			IngameTrack track = ingameTrack;
			if (track != null)
			{
				track.StartRequested = true;
				if (track.Voices != null)
				{
					StartIngameVoices(track, track.StartSeconds);
				}
			}
			ResumeIngameSe();
		}

		private void StartIngameVoices(IngameTrack track, double positionSeconds)
		{
			track.Paused = false;
			foreach (SoundVoice voice in track.Voices)
			{
				voice.Play(positionSeconds);
			}

			// The clock follows what is heard: audio started now reaches the listener after the output latency.
			double latency = AudioEngine.Instance != null ? AudioEngine.Instance.OutputLatency : 0;
			track.Clock.Start(positionSeconds - latency);
		}

		public void ResumeIngame(long musicTimeMs)
		{
			IngameTrack track = ingameTrack;
			if (track != null)
			{
				double position = Math.Max(0L, musicTimeMs) / 1000.0;
				track.StartRequested = true;
				track.StartSeconds = position;
				if (track.Voices != null)
				{
					StartIngameVoices(track, position);
				}
			}
			ResumeIngameSe();
		}

		public void PauseIngame(long musicTimeMs)
		{
			IngameTrack track = ingameTrack;
			if (track != null)
			{
				double position = Math.Max(0L, musicTimeMs) / 1000.0;
				track.Paused = true;
				track.StartSeconds = position;
				if (track.Voices != null)
				{
					foreach (SoundVoice voice in track.Voices)
					{
						voice.Pause();
						voice.Seek(position);
					}
				}
				track.Clock.Seek(position);
			}
			SetSePaused(ingame: true, paused: true);
		}

		public void SetAudioSyncedUnityTimer(uint requestId)
		{
			IngameTrack track = ingameTrack;
			if (track == null || (requestId != 0 && requestId != track.RequestId))
			{
				return;
			}

			SoundVoice primary = GetPrimaryVoice(track);
			if (primary != null && primary.IsPlaying)
			{
				double latency = AudioEngine.Instance != null ? AudioEngine.Instance.OutputLatency : 0;
				track.Clock.Start(primary.Position - latency);
			}
		}

		/// <summary>Song position in milliseconds as heard by the player, sampled once per frame; 0 when nothing plays.</summary>
		public long GetAudioSyncedUnityTimer()
		{
			return TryGetAudioSyncedUnityTimer(out long playbackTime) ? playbackTime : 0L;
		}

		public bool TryGetAudioSyncedUnityTimer(out long playbackTime)
		{
			playbackTime = 0L;
			IngameTrack track = ingameTrack;
			if (track == null || track.Voices == null || track.Voices.Count == 0)
			{
				return false;
			}

			UpdateIngameClock(track);
			playbackTime = Math.Max(0L, (long)Math.Round(track.Clock.Time * 1000.0));
			return true;
		}

		private static void UpdateIngameClock(IngameTrack track)
		{
			if (!track.Clock.Running)
			{
				return;
			}

			double? heard = null;
			SoundVoice primary = GetPrimaryVoice(track);
			AudioEngine engine = AudioEngine.Instance;
			if (primary != null && primary.IsPlaying && engine != null)
			{
				double rendered = primary.Position;
				// Until the output latency has elapsed nothing rendered has been heard yet.
				if (rendered > engine.OutputLatency + 0.02)
				{
					heard = rendered - engine.OutputLatency;
				}
			}

			track.Clock.Update(Time.frameCount, heard);
		}

		private static SoundVoice GetPrimaryVoice(IngameTrack track)
		{
			if (track?.Voices == null)
			{
				return null;
			}

			foreach (SoundVoice voice in track.Voices)
			{
				if (voice != null)
				{
					return voice;
				}
			}
			return null;
		}

		/// <summary>Last measured clock-vs-audio error in milliseconds (diagnostics).</summary>
		public double IngameClockErrorMs => ingameTrack != null ? ingameTrack.Clock.LastError * 1000.0 : 0.0;

		public void StopIngame()
		{
			IngameTrack track = ingameTrack;
			ingameTrack = null;
			if (track?.Voices == null)
			{
				return;
			}

			foreach (SoundVoice voice in track.Voices)
			{
				voice.Dispose();
			}
			track.Voices = null;
		}

		public void StopAll()
		{
			StopIngame();
			foreach (SePlayback playback in sePlaybacks.Values)
			{
				DisposeVoices(playback.Voices);
			}
			sePlaybacks.Clear();
		}

		public uint PlaySE(string cueName)
		{
			return PlaySE(cueName, 1f, 0f, 1);
		}

		public uint PlaySE(string cueName, float vol)
		{
			return PlaySE(cueName, vol, 0f, 1);
		}

		public uint PlaySE(string cueName, float volume = 1f, float startTime = 0f, int playerIndex = 1)
		{
			return PlayCue(cueName, AudioGroup.Se, Mathf.Clamp01(volume), Mathf.Max(0f, startTime), ingame: false);
		}

		public void PlaySEOneShot(string cueName, int priority = 0)
		{
			PlayCue(cueName, AudioGroup.Se, 1f, 0f, ingame: false);
		}

		public uint PlayIngameSE(string cueName)
		{
			return PlayCue(cueName, AudioGroup.Se, 1f, 0f, ingame: true);
		}

		public void PlayIngameSEOneShot(string cueName)
		{
			PlayCue(cueName, AudioGroup.Se, 1f, 0f, ingame: true);
		}

		public void PlayIngameVoiceOneShot(string cueName)
		{
			PlayCue(cueName, AudioGroup.Voice, 1f, 0f, ingame: true);
		}

		public void StopSE(uint playbackId)
		{
			if (playbackId == 0 || !sePlaybacks.TryGetValue(playbackId, out SePlayback playback))
			{
				return;
			}

			DisposeVoices(playback.Voices);
			sePlaybacks.Remove(playbackId);
		}

		public void ResumeIngameSe()
		{
			SetSePaused(ingame: true, paused: false);
		}

		private uint PlayCue(string cueName, AudioGroup group, float volume, float startTime, bool ingame)
		{
			string resolvedCueName = ResolveCueName(cueName, out CueSheet acb);
			if (string.IsNullOrEmpty(resolvedCueName) || acb == null)
			{
				LogMissingCue(cueName);
				return 0;
			}

			Initialize();
			List<SoundVoice> voices;
			try
			{
				voices = acb.CreateVoices(resolvedCueName, group, volume, false, true);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return 0;
			}

			if (voices.Count == 0)
			{
				return 0;
			}

			bool startPaused = appPaused && !ingame;
			foreach (SoundVoice voice in voices)
			{
				voice.Seek(startTime);
				if (!startPaused)
				{
					voice.Resume();
				}
			}

			if (sePlaybacks.Count >= SePlaybackPruneThreshold)
			{
				PruneFinishedSe();
			}

			nextSePlaybackId++;
			if (nextSePlaybackId == 0)
			{
				nextSePlaybackId = 1;
			}
			sePlaybacks[nextSePlaybackId] = new SePlayback { Voices = voices, Ingame = ingame };
			return nextSePlaybackId;
		}

		private void SetSePaused(bool ingame, bool paused)
		{
			foreach (SePlayback playback in sePlaybacks.Values)
			{
				if (playback.Ingame != ingame)
				{
					continue;
				}

				foreach (SoundVoice voice in playback.Voices)
				{
					if (!voice.IsAlive)
					{
						continue;
					}

					if (paused)
					{
						voice.Pause();
					}
					else
					{
						voice.Resume();
					}
				}
			}
		}

		private void PruneFinishedSe()
		{
			List<uint> finished = null;
			foreach (KeyValuePair<uint, SePlayback> pair in sePlaybacks)
			{
				bool alive = false;
				foreach (SoundVoice voice in pair.Value.Voices)
				{
					if (voice.IsAlive)
					{
						alive = true;
						break;
					}
				}

				if (!alive)
				{
					(finished ??= new List<uint>()).Add(pair.Key);
				}
			}

			if (finished == null)
			{
				return;
			}

			foreach (uint id in finished)
			{
				DisposeVoices(sePlaybacks[id].Voices);
				sePlaybacks.Remove(id);
			}
		}

		private static void DisposeVoices(List<SoundVoice> voices)
		{
			if (voices == null)
			{
				return;
			}

			foreach (SoundVoice voice in voices)
			{
				voice.Dispose();
			}
		}

		private uint AllocateIngamePlaybackRequestId()
		{
			currentIngamePlaybackRequestId++;
			if (currentIngamePlaybackRequestId == 0)
			{
				currentIngamePlaybackRequestId = 1;
			}
			return currentIngamePlaybackRequestId;
		}

		private static CueSheet LoadCueSheet(string name, byte[] acbBytes)
		{
			CueSheet sheet = CueSheet.Load(name, acbBytes);
			// Decode on a worker right away: SE banks are ready within milliseconds, music by the time it is prepared.
			sheet?.DecodeAllAsync();
			return sheet;
		}

		private string ResolveCueName(string cueName, out CueSheet acb)
		{
			acb = null;
			if (string.IsNullOrEmpty(cueName))
			{
				return null;
			}

			EnsureDefaultSoundBundlesLoaded();

			if (IsLiveTapCueName(cueName) && TryResolveCurrentLiveTapSeCue(cueName, out acb, out string currentTapSeCueName))
			{
				return currentTapSeCueName;
			}

			// Original SoundManager.Initialize registers MenuCommon_Built_in before
			// dynamic menu bundles, so common button SE should resolve there first.
			string builtInCueName = ResolveCueNameFromBundle(MenuCommonBuiltInCueSheetName, cueName, out acb);
			if (builtInCueName != null)
			{
				return builtInCueName;
			}

			foreach (string bundleName in loadedBundleSearchOrder)
			{
				if (string.Equals(bundleName, MenuCommonBuiltInCueSheetName, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				string resolvedCueName = ResolveCueNameFromBundle(bundleName, cueName, out acb);
				if (resolvedCueName != null)
				{
					return resolvedCueName;
				}
			}

			if (convertSeTable.TryGetValue(cueName, out string convertedCueName))
			{
				builtInCueName = ResolveCueNameFromBundle(MenuCommonBuiltInCueSheetName, convertedCueName, out acb);
				if (builtInCueName != null)
				{
					return builtInCueName;
				}

				foreach (string bundleName in loadedBundleSearchOrder)
				{
					if (string.Equals(bundleName, MenuCommonBuiltInCueSheetName, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					string resolvedCueName = ResolveCueNameFromBundle(bundleName, convertedCueName, out acb);
					if (resolvedCueName != null)
					{
						return resolvedCueName;
					}
				}
			}

			return null;
		}

		private static bool IsLiveTapCueName(string cueName)
		{
			return !string.IsNullOrEmpty(cueName) && LiveTapCueNameSet.Contains(cueName);
		}

		private bool TryResolveCurrentLiveTapSeCue(string cueName, out CueSheet acb, out string resolvedCueName)
		{
			string bundleName = LiveTapSeBundleNamePrefix + "/" + LiveConfig.NoteSeName;
			resolvedCueName = ResolveCueNameFromBundle(bundleName, cueName, out acb);
			return resolvedCueName != null;
		}

		private string ResolveCueNameFromBundle(string bundleName, string cueName, out CueSheet acb)
		{
			acb = null;
			if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(cueName))
			{
				return null;
			}
			if (!acbByBundleName.TryGetValue(bundleName, out CueSheet loadedAcb))
			{
				return null;
			}
			if (loadedAcb != null && loadedAcb.Exists(cueName))
			{
				acb = loadedAcb;
				return cueName;
			}
			return null;
		}

		private bool TryResolveIngameCue(string cueName, out CueSheet acb, out string resolvedCueName)
		{
			acb = null;
			resolvedCueName = null;
			if (string.IsNullOrEmpty(cueName))
			{
				return false;
			}

			string liveMusicBundleName = GetLiveMusicBundleNameCandidate(cueName);
			if (TryResolveCueFromSpecificBundle(liveMusicBundleName, cueName, out acb, out resolvedCueName))
			{
				return true;
			}

			if (TryResolveCueFromSpecificBundle(cueName, cueName, out acb, out resolvedCueName))
			{
				return true;
			}

			foreach (string bundleName in loadedBundleSearchOrder)
			{
				if (TryResolveCueFromSpecificBundle(bundleName, cueName, out acb, out resolvedCueName))
				{
					return true;
				}
			}

			if (!string.IsNullOrEmpty(liveMusicBundleName))
			{
				LoadSoundBundle(liveMusicBundleName, true);
				if (TryResolveCueFromSpecificBundle(liveMusicBundleName, cueName, out acb, out resolvedCueName))
				{
					return true;
				}
			}

			LoadSoundBundle(cueName, true);
			if (TryResolveCueFromSpecificBundle(cueName, cueName, out acb, out resolvedCueName))
			{
				return true;
			}

			if (acbByBundleName.TryGetValue(cueName, out CueSheet directAcb) && TryGetFirstCueName(directAcb, out resolvedCueName))
			{
				acb = directAcb;
				return true;
			}

			return false;
		}

		private static string GetLiveMusicBundleNameCandidate(string cueName)
		{
			if (string.IsNullOrEmpty(cueName))
			{
				return string.Empty;
			}

			string normalized = cueName.Replace('\\', '/');
			if (normalized.StartsWith("music/long/", StringComparison.OrdinalIgnoreCase))
			{
				return normalized;
			}

			if (normalized.Contains("/"))
			{
				return string.Empty;
			}

			return string.Format(LiveMusicBundleNameBase, normalized);
		}

		private bool TryRegisterLoadedBundleAlias(string bundleName)
		{
			if (string.IsNullOrEmpty(bundleName))
			{
				return false;
			}

			string normalized = bundleName.Replace('\\', '/');
			if (!normalized.StartsWith("music/long/", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			string cueName = normalized.Substring("music/long/".Length);
			if (string.IsNullOrEmpty(cueName) || !acbByBundleName.TryGetValue(cueName, out CueSheet acb) || acb == null)
			{
				return false;
			}

			acbByBundleName[bundleName] = acb;
			loadedBundles.Add(bundleName);
			AddLoadedBundleSearchOrder(bundleName);
			return true;
		}

		private void RegisterExternalAudioEntry(string key, ExternalAudioEntry entry)
		{
			externalAudioByKey[key] = entry;
			loadedBundles.Add(key);
			AddLoadedBundleSearchOrder(key);
		}

		private void ReleaseExternalAudioData()
		{
			StopIngame();
			foreach (string key in new List<string>(externalAudioByKey.Keys))
			{
				loadedBundles.Remove(key);
				loadedBundleSearchOrder.Remove(key);
			}
			HashSet<SoundBuffer> released = new HashSet<SoundBuffer>();
			foreach (ExternalAudioEntry entry in externalAudioByKey.Values)
			{
				if (entry.Buffer != null && released.Add(entry.Buffer))
				{
					entry.Buffer.Dispose();
				}
			}
			externalAudioByKey.Clear();
		}

		private bool TryResolveExternalAudio(string key, out ExternalAudioEntry entry)
		{
			if (!string.IsNullOrEmpty(key) && externalAudioByKey.TryGetValue(key, out entry))
			{
				return true;
			}

			string liveMusicBundleName = GetLiveMusicBundleNameCandidate(key);
			if (!string.IsNullOrEmpty(liveMusicBundleName) && externalAudioByKey.TryGetValue(liveMusicBundleName, out entry))
			{
				return true;
			}

			entry = null;
			return false;
		}

		private static bool IsCustomExternalAudioKey(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return false;
			}

			string normalized = key.Replace('\\', '/');
			const string liveMusicPrefix = "music/long/";
			if (normalized.StartsWith(liveMusicPrefix, StringComparison.OrdinalIgnoreCase))
			{
				normalized = normalized.Substring(liveMusicPrefix.Length);
			}
			return normalized.StartsWith("custom_", StringComparison.OrdinalIgnoreCase);
		}

		private bool TryResolveCueFromSpecificBundle(string bundleName, string cueName, out CueSheet acb, out string resolvedCueName)
		{
			foreach (string candidateCueName in GetCueNameCandidates(cueName))
			{
				resolvedCueName = ResolveCueNameFromBundle(bundleName, candidateCueName, out acb);
				if (!string.IsNullOrEmpty(resolvedCueName) && acb != null)
				{
					return true;
				}
			}
			resolvedCueName = null;
			acb = null;
			return false;
		}

		private static IEnumerable<string> GetCueNameCandidates(string cueName)
		{
			if (string.IsNullOrEmpty(cueName))
			{
				yield break;
			}
			yield return cueName;
			string normalized = cueName.Replace('\\', '/');
			int slashIndex = normalized.LastIndexOf('/');
			if (slashIndex >= 0 && slashIndex + 1 < normalized.Length)
			{
				normalized = normalized.Substring(slashIndex + 1);
				yield return normalized;
			}
			if (normalized.EndsWith(".acb", StringComparison.OrdinalIgnoreCase))
			{
				yield return normalized.Substring(0, normalized.Length - ".acb".Length);
			}
		}

		private static bool TryGetFirstCueName(CueSheet acb, out string cueName)
		{
			cueName = null;
			return acb != null && acb.IsAvailable && acb.TryGetFirstCueName(out cueName);
		}

		private static SoundCueInfo[] GetCueInfosWithLength(CueSheet acb)
		{
			if (acb == null || !acb.IsAvailable)
			{
				return Array.Empty<SoundCueInfo>();
			}
			return acb.GetCueInfoList();
		}

		private string GetAcbFileName(string bundleName)
		{
			if (acbFileNameByBundleName.TryGetValue(bundleName, out string acbFileName))
			{
				return acbFileName;
			}

			string lastSegment = bundleName.Replace('\\', '/');
			int slashIndex = lastSegment.LastIndexOf('/');
			if (slashIndex >= 0)
			{
				lastSegment = lastSegment.Substring(slashIndex + 1);
			}
			return lastSegment + ".acb";
		}

		private void EnsureDefaultSoundBundlesLoaded()
		{
			if (defaultSoundBundlesRequested)
			{
				return;
			}

			defaultSoundBundlesRequested = true;
			// IDA shows the original app loads MenuCommon_Built_in on SoundManager.Initialize.
			// MenuCommon is kept as a project-local supplement for score-maker cues copied from AssetRipper.
			LoadSoundBundle(MenuCommonBuiltInCueSheetName, true);
			LoadSoundBundle(MenuCommonBundleName, true);
		}

		private static bool IsDefaultSoundBundle(string bundleName)
		{
			return string.Equals(bundleName, MenuCommonBuiltInCueSheetName, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(bundleName, MenuCommonBundleName, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(bundleName, MenuCommonCueSheetName, StringComparison.OrdinalIgnoreCase);
		}

		private static bool ShouldPreferAssetBundleAcb(string bundleName)
		{
			// Project-local menu_common contains score-maker cues and is built through
			// the README AssetBundle flow; the loose StreamingAssets ACB is only a fallback.
			return string.Equals(bundleName, MenuCommonBundleName, StringComparison.OrdinalIgnoreCase);
		}

		private List<string> GetAcbFileNameCandidates(string bundleName)
		{
			List<string> candidates = new List<string>();
			AddAcbFileNameCandidateWithVariants(candidates, GetAcbFileName(bundleName));
			if (string.Equals(bundleName, LiveDefaultSoundBundleName, StringComparison.OrdinalIgnoreCase))
			{
				AddAcbFileNameCandidateWithVariants(candidates, LiveDefaultCommonAcbFileName);
			}
			if (string.Equals(bundleName, MenuCommonBuiltInCueSheetName, StringComparison.OrdinalIgnoreCase))
			{
				AddAcbFileNameCandidateWithVariants(candidates, MenuCommonBuiltInAcbFileName);
			}
			if (string.Equals(bundleName, MenuCommonBundleName, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(bundleName, MenuCommonCueSheetName, StringComparison.OrdinalIgnoreCase))
			{
				AddAcbFileNameCandidateWithVariants(candidates, MenuCommonAcbFileName);
			}
			return candidates;
		}

		private static void AddAcbFileNameCandidateWithVariants(List<string> candidates, string acbFileName)
		{
			if (string.IsNullOrEmpty(acbFileName))
			{
				return;
			}

			if (acbFileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
			{
				string withoutBytes = acbFileName.Substring(0, acbFileName.Length - ".bytes".Length);
				AddAcbFileNameCandidate(candidates, withoutBytes);
				AddAcbFileNameCandidate(candidates, acbFileName);
				return;
			}

			AddAcbFileNameCandidate(candidates, acbFileName);
			AddAcbFileNameCandidate(candidates, acbFileName + ".bytes");
		}

		private static void AddAcbFileNameCandidate(List<string> candidates, string acbFileName)
		{
			if (string.IsNullOrEmpty(acbFileName) || candidates.Contains(acbFileName))
			{
				return;
			}
			candidates.Add(acbFileName);
		}

		public SoundBundleBuildData GetSoundBundleBuildData(string bundleName)
		{
			if (string.IsNullOrEmpty(bundleName))
			{
				return null;
			}

#if UNITY_EDITOR
			SoundBundleBuildData editorBuildData = TryLoadSoundBundleBuildDataFromEditorAssets(bundleName);
			if (editorBuildData != null)
			{
				return editorBuildData;
			}
#endif

			try
			{
				if (!AssetBundleManager.Instance.HasBundle(bundleName))
				{
					return null;
				}

				return AssetBundleUtility.LoadAsset<SoundBundleBuildData>(bundleName, SoundBundleBuildDataAssetName, false);
			}
			catch
			{
				return null;
			}
		}

		private bool TryLoadSoundBundleFromBuildData(string bundleName, List<string> diagnosticCandidates)
		{
			SoundBundleBuildData buildData = GetSoundBundleBuildData(bundleName);
			SoundBundleData[] acbFiles = buildData?.AcbFiles;
			if (acbFiles == null || acbFiles.Length == 0)
			{
				return false;
			}

			bool loadedAny = false;
			for (int i = 0; i < acbFiles.Length; i++)
			{
				SoundBundleData soundBundleData = acbFiles[i];
				if (soundBundleData == null)
				{
					continue;
				}

				List<string> acbFileCandidates = new List<string>();
				AddAcbFileNameCandidateWithVariants(acbFileCandidates, soundBundleData.AssetBundleFileName);
				for (int j = 0; j < acbFileCandidates.Count; j++)
				{
					AddAcbFileNameCandidate(diagnosticCandidates, acbFileCandidates[j]);
					byte[] acbBytes = TryLoadAcbBytesFromAssetBundle(bundleName, acbFileCandidates[j]);
					CueSheet acb = LoadCueSheet(acbFileCandidates[j], acbBytes);
					if (acb == null)
					{
						acb = TryLoadAcbFromStreamingAssets(acbFileCandidates[j]);
					}
					if (acb == null || !acb.IsAvailable)
					{
						continue;
					}

					string cueSheetName = GetCueSheetName(soundBundleData);
					if (string.IsNullOrEmpty(cueSheetName))
					{
						cueSheetName = bundleName;
					}

					acbByBundleName[cueSheetName] = acb;
					AddLoadedBundleSearchOrder(cueSheetName);
					if (!acbByBundleName.ContainsKey(bundleName))
					{
						acbByBundleName[bundleName] = acb;
					}
					loadedAny = true;
					break;
				}
			}

			if (!loadedAny)
			{
				return false;
			}

			loadedBundles.Add(bundleName);
			return true;
		}

		private static string GetCueSheetName(SoundBundleData soundBundleData)
		{
			if (!string.IsNullOrEmpty(soundBundleData.CueSheetName))
			{
				return soundBundleData.CueSheetName;
			}

			string fileName = soundBundleData.AssetBundleFileName;
			if (string.IsNullOrEmpty(fileName))
			{
				return string.Empty;
			}

			if (fileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
			{
				fileName = fileName.Substring(0, fileName.Length - ".bytes".Length);
			}
			if (fileName.EndsWith(".acb", StringComparison.OrdinalIgnoreCase))
			{
				fileName = fileName.Substring(0, fileName.Length - ".acb".Length);
			}
			return Path.GetFileName(fileName);
		}

		private void AddLoadedBundleSearchOrder(string bundleName)
		{
			if (!string.IsNullOrEmpty(bundleName) && !loadedBundleSearchOrder.Contains(bundleName))
			{
				loadedBundleSearchOrder.Add(bundleName);
			}
		}

		private static byte[] TryLoadStreamingAssetBytes(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return null;
			}

			if (IsDirectFilePath(path))
			{
				return File.Exists(path) ? File.ReadAllBytes(path) : null;
			}

			using (UnityWebRequest request = UnityWebRequest.Get(path))
			{
				UnityWebRequestAsyncOperation operation = request.SendWebRequest();
				while (!operation.isDone)
				{
					Thread.Sleep(1);
				}

				if (request.result != UnityWebRequest.Result.Success)
				{
					return null;
				}

				return request.downloadHandler?.data;
			}
		}

		private static bool IsDirectFilePath(string path)
		{
			return !string.IsNullOrEmpty(path)
				&& path.IndexOf("://", StringComparison.Ordinal) < 0
				&& !path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase);
		}

		private static byte[] TryLoadAcbBytesFromAssetBundle(string bundleName, string acbFileName)
		{
			if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(acbFileName))
			{
				return null;
			}

			try
			{
#if UNITY_EDITOR
				byte[] editorBytes = TryLoadAcbBytesFromEditorAssets(bundleName, acbFileName);
				if (editorBytes != null && editorBytes.Length > 0)
				{
					return editorBytes;
				}
#endif

				if (!AssetBundleManager.Instance.HasBundle(bundleName))
				{
					return null;
				}
				TextAsset acbAsset = AssetBundleUtility.LoadAsset<TextAsset>(bundleName, acbFileName, false);
				return acbAsset != null ? acbAsset.bytes : null;
			}
			catch
			{
				return null;
			}
		}

#if UNITY_EDITOR
		private static SoundBundleBuildData TryLoadSoundBundleBuildDataFromEditorAssets(string bundleName)
		{
			foreach (string basePath in GetEditorAssetBundleResourceBasePaths(bundleName))
			{
				string path = basePath + "/" + SoundBundleBuildDataAssetName + ".asset";
				SoundBundleBuildData buildData = AssetDatabase.LoadAssetAtPath<SoundBundleBuildData>(path);
				if (buildData != null)
				{
					return buildData;
				}
			}
			return null;
		}

		private static byte[] TryLoadAcbBytesFromEditorAssets(string bundleName, string acbFileName)
		{
			foreach (string basePath in GetEditorAssetBundleResourceBasePaths(bundleName))
			{
				foreach (string fileName in GetEditorAcbFileNameCandidates(acbFileName))
				{
					string path = basePath + "/" + fileName;
					TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
					if (textAsset != null && textAsset.bytes != null && textAsset.bytes.Length > 0)
					{
						return textAsset.bytes;
					}

					string fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
					if (File.Exists(fullPath))
					{
						return File.ReadAllBytes(fullPath);
					}
				}
			}
			return null;
		}

		private static IEnumerable<string> GetEditorAssetBundleResourceBasePaths(string bundleName)
		{
			string normalized = bundleName.Replace('\\', '/').Trim('/');
			yield return "Assets/Sekai/assetbundle/resources/startapp/" + normalized;
			yield return "Assets/Sekai/assetbundle/resources/tutorial/" + normalized;
			yield return "Assets/Sekai/assetbundle/resources/" + normalized;
		}

		private static IEnumerable<string> GetEditorAcbFileNameCandidates(string acbFileName)
		{
			if (string.IsNullOrEmpty(acbFileName))
			{
				yield break;
			}

			yield return acbFileName;
			if (acbFileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
			{
				yield return acbFileName.Substring(0, acbFileName.Length - ".bytes".Length);
			}
			else
			{
				yield return acbFileName + ".bytes";
			}
		}
#endif

		private static CueSheet TryLoadAcbFromStreamingAssets(string acbFileName)
		{
			if (string.IsNullOrEmpty(acbFileName))
			{
				return null;
			}

			foreach (string candidate in GetStreamingAssetPathCandidates(acbFileName))
			{
				if (!ShouldTryStreamingAssetPath(candidate))
				{
					continue;
				}

				try
				{
					byte[] acbBytes = TryLoadStreamingAssetBytes(candidate);
					CueSheet acb = LoadCueSheet(acbFileName, acbBytes);
					if (acb != null && acb.IsAvailable)
					{
						return acb;
					}
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}

			return null;
		}

		private static IEnumerable<string> GetStreamingAssetPathCandidates(string fileName)
		{
			string[] candidates =
			{
				Path.Combine(Application.streamingAssetsPath, fileName),
				Path.Combine(Application.streamingAssetsPath, "data", fileName),
				Path.Combine(Application.streamingAssetsPath, "sound/menu/menu_common", fileName)
			};

			foreach (string candidate in candidates)
			{
				if (!string.IsNullOrEmpty(candidate))
				{
					yield return candidate;
				}
			}
		}

		private static bool ShouldTryStreamingAssetPath(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return false;
			}

			if (Application.platform == RuntimePlatform.Android)
			{
				return true;
			}

			return File.Exists(path);
		}

		private void LogMissingBundle(string bundleName, string acbFileName)
		{
			string key = bundleName + "|" + acbFileName;
			if (warnedMissingBundles.Add(key))
			{
				Debug.LogWarningFormat("Sound bundle could not be loaded. bundle:{0} acb:{1}", bundleName, acbFileName);
			}
		}

		private void LogMissingCue(string cueName)
		{
			if (!string.IsNullOrEmpty(cueName) && warnedMissingCues.Add(cueName))
			{
				Debug.LogWarningFormat("{0} は登録されていません。", cueName);
			}
		}

		public void Pause()
		{
			appPaused = true;
			IngameTrack track = ingameTrack;
			if (track?.Voices != null)
			{
				foreach (SoundVoice voice in track.Voices)
				{
					voice.Pause();
				}
				track.Clock.Pause();
			}
			SetSePaused(ingame: false, paused: true);
		}

		public void Resume()
		{
			appPaused = false;
			IngameTrack track = ingameTrack;
			if (track?.Voices != null && track.StartRequested && !track.Paused)
			{
				SoundVoice primary = GetPrimaryVoice(track);
				double position = primary != null ? primary.Position : track.Clock.Time;
				StartIngameVoices(track, position);
			}
			SetSePaused(ingame: false, paused: false);
		}
	}

	internal sealed class ExternalAudioEntry
	{
		public string Key;
		public SoundBuffer Buffer;
		public string SourcePath;
		public long LengthMs;
		public long SourceVersion;
	}
}
