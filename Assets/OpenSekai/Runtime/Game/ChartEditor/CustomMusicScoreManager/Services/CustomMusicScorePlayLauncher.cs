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
				return "无法加载谱面。";
			}
			if (!File.Exists(entry.ScorePath))
			{
				return "找不到谱面文件。";
			}
			if (!File.Exists(entry.AudioPath))
			{
				return "找不到音频文件。";
			}

			MusicScoreMakerData scoreData = entry.LoadScore();
			if (scoreData == null)
			{
				return "无法加载谱面文件。";
			}
			if (scoreData.NoteList == null || !scoreData.NoteList.Exists(note => note != null))
			{
				return "没有可游玩的音符。";
			}

			setStatus?.Invoke("正在加载音频...");
			if (!await entry.RegisterAudioAsync(ct))
			{
				return "无法加载音频文件。";
			}

			FreeLiveBootData bootData = ScreenLayerCustomMusicScoreManager.CreateDirectPlayBootData(entry, scoreData, isAuto, returnScreenType);
			if (bootData == null)
			{
				return "无法创建游玩启动数据。";
			}

			Sekai.Core.CoreSceneArgs.Prepare(playMode, bootData);
			LiveTransitioner.SafeForceFinish(null);
			ScreenManager.Instance?.PushUIScreen(MenuScreenType.LiveLoading, false);
			return null;
		}
	}
}
