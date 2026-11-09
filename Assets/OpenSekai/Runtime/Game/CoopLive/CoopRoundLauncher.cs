using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sekai.Community;
using Sekai.CoopLive.Proto;
using Sekai.CustomMusicScoreManager;
using Sekai.MusicScoreMaker.Common;
using UnityEngine;

namespace Sekai.CoopLive
{
	/// <summary>
	/// LOADING: downloads the chart the server gave this player (the community chart of the drawn song at the
	/// picked difficulty) and opens the live in co-op mode. The Core scene then reports `loaded` once the
	/// music is ready (<see cref="CoopLiveBridge"/>), and the live starts when everyone has.
	/// </summary>
	public static class CoopRoundLauncher
	{
		private static int _launchedRound;

		/// <summary>Progress for the room screen ("下载中 42%").</summary>
		public static event Action<string> StatusChanged;

		public static bool IsLaunching { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			_launchedRound = 0;
			IsLaunching = false;
			StatusChanged = null;
		}

		/// <summary>Called with every room snapshot; starts the live once this player is due to load.</summary>
		public static void OnRoom(Room room, CancellationToken ct)
		{
			if (room == null || room.Phase != Phase.Loading || _launchedRound == room.Round)
			{
				return;
			}
			Member me = CoopSession.Me;
			if (me == null || !me.Playing || me.Loaded || string.IsNullOrEmpty(me.ChartId))
			{
				return;
			}
			_launchedRound = room.Round;
			LaunchAsync(room.Round, me.ChartId, ct).Forget();
		}

		private static async UniTaskVoid LaunchAsync(int round, string chartId, CancellationToken ct)
		{
			IsLaunching = true;
			try
			{
				StatusChanged?.Invoke("正在获取谱面…");
				CommunityChart chart = await CommunityClient.GetAsync(chartId, ct);
				if (chart == null)
				{
					Fail("谱面已经被删除。");
					return;
				}

				CustomMusicScoreEntry entry = CommunityLibrary.GetState(chart) == CommunityInstallState.Installed
					? CommunityLibrary.GetEntry(chart)
					: null;
				if (entry == null)
				{
					var progress = new Progress<float>(p => StatusChanged?.Invoke($"下载谱面 {Mathf.RoundToInt(p * 100)}%"));
					entry = await CommunityLibrary.DownloadAsync(chart, progress, ct);
				}
				if (CoopSession.Room == null || CoopSession.Room.Round != round || CoopSession.Room.Phase != Phase.Loading)
				{
					return;
				}

				CoopSession.LastPlayedRound = round;
				string error = await CustomMusicScorePlayLauncher.PlayAsync(
					entry, false, MenuScreenType.CoopLive, s => StatusChanged?.Invoke(s), ct, Sekai.Core.PlayMode.MultiLive);
				if (error != null)
				{
					Fail(error);
				}
			}
			catch (OperationCanceledException)
			{
				Fail("已取消。");
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				Fail(exception is CommunityException ? exception.Message : "加载谱面失败。");
			}
			finally
			{
				IsLaunching = false;
			}
		}

		private static void Fail(string message)
		{
			CoopSession.Loaded(false, message);
			StatusChanged?.Invoke(message);
		}
	}
}
