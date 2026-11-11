using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sekai.MusicScoreMaker.Common;
using Sekai.MusicScoreMaker.Ingame.Models;

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>
	/// Starts a live of a local custom chart: checks its files, registers its audio and opens the Core scene.
	/// Used by the chart manager and the community chart browser; <paramref name="returnScreenType"/> says
	/// which one the live goes back to.
	/// </summary>
	public static class CustomMusicScorePlayLauncher
	{
		private const string Table = "ChartManager";

		/// <summary>Returns null once the live is starting, otherwise why it cannot start.</summary>
		public static async UniTask<string> PlayAsync(
			CustomMusicScoreEntry entry,
			bool isAuto,
			MenuScreenType returnScreenType,
			Action<string> setStatus,
			CancellationToken ct,
			Sekai.Core.PlayMode playMode = Sekai.Core.PlayMode.SoloLive)
		{
			if (entry == null)
			{
				return L10n.Get(Table, "status.loadFailed");
			}
			if (!File.Exists(entry.ScorePath))
			{
				return L10n.Get(Table, "launch.scoreMissing");
			}
			if (!File.Exists(entry.AudioPath))
			{
				return L10n.Get(Table, "launch.audioMissing");
			}

			MusicScoreMakerData scoreData = entry.LoadScore();
			if (scoreData == null)
			{
				return L10n.Get(Table, "launch.scoreLoadFailed");
			}
			if (scoreData.NoteList == null || !scoreData.NoteList.Exists(note => note != null))
			{
				return L10n.Get(Table, "launch.noNotes");
			}

			setStatus?.Invoke(L10n.Get(Table, "launch.loadingAudio"));
			if (!await entry.RegisterAudioAsync(ct))
			{
				return L10n.Get(Table, "launch.audioLoadFailed");
			}

			FreeLiveBootData bootData = ScreenLayerCustomMusicScoreManager.CreateDirectPlayBootData(entry, scoreData, isAuto, returnScreenType);
			if (bootData == null)
			{
				return L10n.Get(Table, "launch.bootDataFailed");
			}

			Sekai.Core.CoreSceneArgs.Prepare(playMode, bootData);
			LiveTransitioner.SafeForceFinish(null);
			ScreenManager.Instance?.PushUIScreen(MenuScreenType.LiveLoading, false);
			return null;
		}
	}
}
