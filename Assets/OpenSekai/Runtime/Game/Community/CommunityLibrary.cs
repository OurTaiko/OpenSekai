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
	/// Community charts downloaded into the local custom chart folder. A download fetches the chart's parts
	/// from the CDN, is imported with <see cref="CustomMusicScoreManagerService.ImportFolder"/>, is counted with
	/// POST /api/v1/charts/&lt;id&gt;/downloads, and is remembered in
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

		/// <summary>
		/// Downloads a chart and imports it, replacing an older installed revision. The site keeps no ZIP: this
		/// fetches each part from the CDN, checks its size and SHA-256, writes manifest.json beside them and
		/// imports the folder. Returns the local entry.
		/// </summary>
		public static async UniTask<CustomMusicScoreEntry> DownloadAsync(CommunityChart chart, IProgress<float> progress, CancellationToken ct)
		{
			// List rows carry no package; the single-chart read does.
			CommunityChart full = chart.Package != null ? chart : await CommunityClient.GetAsync(chart.Id, ct);
			CommunityPackage package = full?.Package;
			if (package?.Manifest == null || package.Files == null || package.Files.Count == 0)
			{
				throw new CommunityException("PACKAGE_PENDING", "这份谱面正在整理，请稍后再试。");
			}

			string work = Path.Combine(DownloadDirectory, chart.Id + "-r" + full.Revision + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
			string folder = Path.Combine(work, "chart");
			try
			{
				Directory.CreateDirectory(folder);
				File.WriteAllText(Path.Combine(folder, "manifest.json"), package.Manifest.ToString(Formatting.Indented));

				long total = Math.Max(1L, package.Files.Sum(f => Math.Max(0L, f.Size)));
				long done = 0;
				foreach (CommunityPackageFile file in package.Files)
				{
					string name = file.Name ?? string.Empty;
					if (name.Length == 0 || name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
						|| string.Equals(name, "manifest.json", StringComparison.OrdinalIgnoreCase))
					{
						throw new CommunityException("PACKAGE_INVALID", "谱面包里有不正确的文件名。");
					}
					string path = Path.Combine(folder, name);
					long before = done;
					await DownloadFileAsync(file.Url, path, received => progress?.Report((float)(before + received) / total), ct);
					await VerifyAsync(file, path, ct);
					done += Math.Max(0L, file.Size);
				}
				progress?.Report(1f);

				CustomMusicScoreEntry previous = GetEntry(chart);
				CustomMusicScoreEntry entry = CustomMusicScoreManagerService.ImportFolder(folder);
				if (entry == null)
				{
					throw new CommunityException("IMPORT_FAILED", "谱面无法导入。");
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
					Revision = full.Revision,
					Directory = Path.GetFileName(entry.RootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
					InstalledAt = DateTime.UtcNow,
				};
				SaveIndex();
				chart.Revision = full.Revision;
				ReportDownloadAsync(chart.Id).Forget();
				return entry;
			}
			finally
			{
				try
				{
					if (Directory.Exists(work))
					{
						Directory.Delete(work, true);
					}
				}
				catch (IOException)
				{
				}
			}
		}

		private static async UniTask DownloadFileAsync(string url, string path, Action<long> received, CancellationToken ct)
		{
			using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
			request.downloadHandler = new DownloadHandlerFile(path) { removeFileOnAbort = true };
			request.timeout = 300;
			UnityWebRequestAsyncOperation operation = request.SendWebRequest();
			try
			{
				while (!operation.isDone)
				{
					received((long)request.downloadedBytes);
					await UniTask.Yield(PlayerLoopTiming.Update, ct);
				}
			}
			catch (OperationCanceledException)
			{
				request.Abort();
				throw;
			}
			if (request.result != UnityWebRequest.Result.Success)
			{
				throw new CommunityException(
					request.responseCode == 404 ? "NOT_FOUND" : "DOWNLOAD_FAILED",
					request.responseCode == 404 ? "这份谱面的文件已经不在了。" : "下载失败：" + request.error,
					request.responseCode);
			}
		}

		private static async UniTask VerifyAsync(CommunityPackageFile file, string path, CancellationToken ct)
		{
			long length = new FileInfo(path).Length;
			if (file.Size > 0 && length != file.Size)
			{
				throw new CommunityException("DOWNLOAD_INCOMPLETE", file.Name + " 下载不完整，请重试。");
			}
			if (string.IsNullOrEmpty(file.Sha256))
			{
				return;
			}
			string hash = await UniTask.RunOnThreadPool(() =>
			{
				using FileStream stream = File.OpenRead(path);
				using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
				return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
			}, cancellationToken: ct);
			if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
			{
				throw new CommunityException("DOWNLOAD_INCOMPLETE", file.Name + " 下载不完整，请重试。");
			}
		}

		private static async UniTaskVoid ReportDownloadAsync(string chartId)
		{
			try
			{
				await CommunityClient.ReportDownloadAsync(chartId);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Community] Could not count the download of chart " + chartId + ": " + exception.Message);
			}
		}

		/// <summary>
		/// The site chart a local chart folder is linked to: one downloaded from the site, or one the player uploaded
		/// from the manager. Null when it has none.
		/// </summary>
		public static string FindChartId(string chartDirectory)
		{
			string name = FolderName(chartDirectory);
			if (string.IsNullOrEmpty(name))
			{
				return null;
			}
			return Index().Values
				.Where(i => string.Equals(i.Directory, name, StringComparison.Ordinal))
				.OrderByDescending(i => i.InstalledAt)
				.Select(i => i.ChartId)
				.FirstOrDefault();
		}

		/// <summary>
		/// Links a local chart folder to the site chart it was just uploaded as, so the online browser shows it as
		/// downloaded, plays of it are reported, and the next upload replaces that chart instead of adding another.
		/// </summary>
		public static void RememberUpload(string chartId, int revision, string chartDirectory)
		{
			string name = FolderName(chartDirectory);
			if (string.IsNullOrEmpty(chartId) || string.IsNullOrEmpty(name))
			{
				return;
			}
			foreach (string stale in Index().Where(p => p.Key != chartId && string.Equals(p.Value.Directory, name, StringComparison.Ordinal)).Select(p => p.Key).ToList())
			{
				Index().Remove(stale);
			}
			Index()[chartId] = new Install { ChartId = chartId, Revision = revision, Directory = name, InstalledAt = DateTime.UtcNow };
			SaveIndex();
		}

		/// <summary>Forgets a link whose site chart is gone.</summary>
		public static void Forget(string chartId)
		{
			if (!string.IsNullOrEmpty(chartId) && Index().Remove(chartId))
			{
				SaveIndex();
			}
		}

		private static string FolderName(string chartDirectory)
		{
			return string.IsNullOrEmpty(chartDirectory)
				? null
				: Path.GetFileName(chartDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
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
