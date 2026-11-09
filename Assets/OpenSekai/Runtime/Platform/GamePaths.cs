// Adapted from OurTaikoPlay (Runtime/Core/GamePaths.cs) and MajdataPlay (MajEnv.RootPath), both GPLv3. See NOTICE.md.
using System;
using System.IO;
using UnityEngine;

namespace OpenSekai.Platform
{
	/// <summary>
	/// Where settings, custom charts, editor saves and logs live.
	/// <list type="bullet">
	/// <item>Desktop players keep them with the game, in <c>UserData</c> beside the executable on Windows and
	/// Linux, or at the root of the <c>.app</c> bundle on macOS, so a copied game folder carries its data along.
	/// When that folder is read-only (Program Files, a translocated macOS app) they fall back to
	/// <see cref="Application.persistentDataPath"/>.</item>
	/// <item>A desktop player that already saved into persistentDataPath (builds before this class existed) keeps
	/// using it, so nothing is left behind.</item>
	/// <item>iOS uses Documents (persistentDataPath), which the Info.plist shares in the Files app.
	/// Android and the Editor use persistentDataPath.</item>
	/// </list>
	/// </summary>
	public static class GamePaths
	{
		public const string PortableDirectoryName = "UserData";

		// Files and folders an older desktop build wrote straight into persistentDataPath.
		private static readonly string[] LegacyEntries =
		{
			"ApplicationLocalSettings.json",
			"LiveSettingData.json",
			"audio_settings.json",
			"CustomMusicScores",
			"music_score_maker",
			"inappcache",
		};

		private static string _root;

		/// <summary>The data root. Read it first on the main thread: it asks Unity for its paths.</summary>
		public static string Root => _root ??= Resolve();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => _root = null;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void ResolveOnMainThread() => _ = Root;

		private static string Resolve()
		{
			string persistent = Application.persistentDataPath;
#if UNITY_STANDALONE && !UNITY_EDITOR
			if (HasLegacyData(persistent))
			{
				return persistent;
			}

			// dataPath is <name>_Data beside the executable, or <name>.app/Contents on macOS (so this is <name>.app/UserData).
			string portable = Path.GetFullPath(Path.Combine(Application.dataPath, "..", PortableDirectoryName));
			if (IsWritable(portable))
			{
				return portable;
			}

			Debug.LogWarning("[GamePaths] The game folder is read-only; saving to " + persistent);
#endif
			return persistent;
		}

		private static bool HasLegacyData(string directory)
		{
			foreach (string entry in LegacyEntries)
			{
				string path = Path.Combine(directory, entry);
				if (File.Exists(path) || Directory.Exists(path))
				{
					return true;
				}
			}

			// Chart editor saves (MusicScoreSaveDataManagerDialog) sit at the top level.
			return Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.musicScore").GetEnumerator().MoveNext();
		}

		private static bool IsWritable(string directory)
		{
			string probe = Path.Combine(directory, ".write-" + Guid.NewGuid().ToString("N"));
			try
			{
				Directory.CreateDirectory(directory);
				File.WriteAllBytes(probe, Array.Empty<byte>());
				File.Delete(probe);
				return true;
			}
			catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
			                              error is System.Security.SecurityException)
			{
				return false;
			}
		}
	}
}
