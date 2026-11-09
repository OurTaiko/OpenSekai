using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenSekai.Platform;
using Sekai.CustomMusicScoreManager;
using Sekai.MusicScoreMaker.Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Sekai.Community
{
	public enum CommunityInstallState
	{
		NotInstalled,
		Installed,
		/// <summary>Installed, but the site has a newer revision.</summary>
		Outdated,
	}

	/// <summary>
	/// Community charts downloaded into the local custom chart folder. A download goes through
	/// /charts/&lt;id&gt;/download (which counts it and redirects to the CDN), is imported with
	/// <see cref="CustomMusicScoreManagerService.ImportZip"/>, and is remembered in
	/// <c>CustomMusicScores/CommunityInstalls.json</c> so the browser can show 已下载 and replace it with a newer revision.
	/// </summary>
	public static class CommunityLibrary
	{
		public const string IndexFileName = "CommunityInstalls.json";

		private sealed class Install
		{
			/// <summary>The chart's id. Records written before ids were random hold the old number here.</summary>
			public string ChartId = string.Empty;
			public int Revision;
			/// <summary>Folder name under CustomMusicScores.</summary>
			public string Directory = string.Empty;
			public DateTime InstalledAt;
		}

		private static Dictionary<string, Install> _index;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => _index = null;

		private static string IndexPath => Path.Combine(CustomMusicScoreStorage.RootDirectory, IndexFileName);

		private static string DownloadDirectory => Path.Combine(GamePaths.Root, "CommunityDownloads");

		public static CommunityInstallState GetState(CommunityChart chart)
		{
			if (chart == null || !TryGetInstall(chart, out Install install))
			{
				return CommunityInstallState.NotInstalled;
			}
			return install.Revision < chart.Revision ? CommunityInstallState.Outdated : CommunityInstallState.Installed;
		}

		/// <summary>The installed local entry of a community chart, or null.</summary>
		public static CustomMusicScoreEntry GetEntry(CommunityChart chart)
		{
			return TryGetInstall(chart, out Install install)
				? CustomMusicScoreStorage.LoadEntry(Path.Combine(CustomMusicScoreStorage.RootDirectory, install.Directory))
				: null;
		}

		/// <summary>Downloads and imports a chart, replacing an older installed revision. Returns the local entry.</summary>
		public static async UniTask<CustomMusicScoreEntry> DownloadAsync(CommunityChart chart, IProgress<float> progress, CancellationToken ct)
		{
			Directory.CreateDirectory(DownloadDirectory);
			string zipPath = Path.Combine(DownloadDirectory, chart.Id + "-r" + chart.Revision + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zip");
			try
			{
				using (UnityWebRequest request = new UnityWebRequest(CommunityClient.DownloadUrl(chart.Id), UnityWebRequest.kHttpVerbGET))
				{
					request.downloadHandler = new DownloadHandlerFile(zipPath) { removeFileOnAbort = true };
					request.timeout = 300;
					UnityWebRequestAsyncOperation operation = request.SendWebRequest();
					while (!operation.isDone)
					{
						progress?.Report(request.downloadProgress);
						await UniTask.Yield(PlayerLoopTiming.Update, ct);
					}
					if (request.result != UnityWebRequest.Result.Success)
					{
						throw new CommunityException(
							request.responseCode == 404 ? "NOT_FOUND" : "DOWNLOAD_FAILED",
							request.responseCode == 404 ? "这份谱面已经不在了。" : "下载失败：" + request.error,
							request.responseCode);
					}
				}
				progress?.Report(1f);

				CustomMusicScoreEntry previous = GetEntry(chart);
				CustomMusicScoreEntry entry = CustomMusicScoreManagerService.ImportZip(zipPath);
				if (entry == null)
				{
					throw new CommunityException("IMPORT_FAILED", "谱面包无法导入。");
				}
				if (previous != null && !string.Equals(previous.RootDirectory, entry.RootDirectory, StringComparison.Ordinal))
				{
					try
					{
						CustomMusicScoreManagerService.DeleteEntry(previous);
					}
					catch (Exception exception)
					{
						Debug.LogWarning("[Community] Could not remove the older revision: " + exception.Message);
					}
				}

				Index()[chart.Id] = new Install
				{
					ChartId = chart.Id,
					Revision = chart.Revision,
					Directory = Path.GetFileName(entry.RootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
					InstalledAt = DateTime.UtcNow,
				};
				SaveIndex();
				return entry;
			}
			finally
			{
				try
				{
					if (File.Exists(zipPath))
					{
						File.Delete(zipPath);
					}
				}
				catch (IOException)
				{
				}
			}
		}

		/// <summary>
		/// Called when a live of a local custom chart finishes (not auto, not a test play). If the chart came from
		/// the community site, the play is reported there in the background.
		/// </summary>
		public static void OnLocalChartPlayed(string chartDirectory)
		{
			if (string.IsNullOrEmpty(chartDirectory))
			{
				return;
			}
			string name = Path.GetFileName(chartDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			Install install = Index().Values.FirstOrDefault(i => string.Equals(i.Directory, name, StringComparison.Ordinal));
			if (install == null)
			{
				return;
			}
			ReportAsync(install.ChartId).Forget();
		}

		private static async UniTaskVoid ReportAsync(string chartId)
		{
			try
			{
				await CommunityClient.ReportPlayAsync(chartId);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Community] Could not report the play of chart " + chartId + ": " + exception.Message);
			}
		}

		private static bool TryGetInstall(CommunityChart chart, out Install install)
		{
			string chartId = chart.Id;
			if (!Index().TryGetValue(chartId, out install))
			{
				// Downloaded when chart ids were numbers: the record holds the number. Re-key it.
				string legacyKey = chart.NumberId.ToString(System.Globalization.CultureInfo.InvariantCulture);
				if (chart.NumberId <= 0 || !Index().TryGetValue(legacyKey, out install))
				{
					return false;
				}
				Index().Remove(legacyKey);
				install.ChartId = chartId;
				Index()[chartId] = install;
				SaveIndex();
			}
			// The player may have deleted it in the manager.
			if (!Directory.Exists(Path.Combine(CustomMusicScoreStorage.RootDirectory, install.Directory)))
			{
				Index().Remove(chartId);
				SaveIndex();
				install = null;
				return false;
			}
			return true;
		}

		private static Dictionary<string, Install> Index()
		{
			if (_index != null)
			{
				return _index;
			}
			_index = new Dictionary<string, Install>(StringComparer.Ordinal);
			try
			{
				if (File.Exists(IndexPath))
				{
					List<Install> list = JsonConvert.DeserializeObject<List<Install>>(File.ReadAllText(IndexPath));
					foreach (Install install in list ?? new List<Install>())
					{
						if (install != null && !string.IsNullOrEmpty(install.ChartId) && !string.IsNullOrEmpty(install.Directory))
						{
							_index[install.ChartId] = install;
						}
					}
				}
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Community] Ignoring unreadable " + IndexFileName + ": " + exception.Message);
			}
			return _index;
		}

		private static void SaveIndex()
		{
			try
			{
				Directory.CreateDirectory(CustomMusicScoreStorage.RootDirectory);
				string temporary = IndexPath + ".tmp";
				File.WriteAllText(temporary, JsonConvert.SerializeObject(Index().Values.OrderBy(i => i.InstalledAt).ToList(), Formatting.Indented));
				if (File.Exists(IndexPath))
				{
					File.Delete(IndexPath);
				}
				File.Move(temporary, IndexPath);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Community] Could not save " + IndexFileName + ": " + exception.Message);
			}
		}
	}
}
