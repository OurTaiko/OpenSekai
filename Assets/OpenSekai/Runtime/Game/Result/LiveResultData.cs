using Sekai.Core.Live;
using Sekai.MusicScoreMaker.Common;
using UnityEngine;

namespace Sekai
{
	/// <summary>
	/// What the Result scene shows about this player's own live: the chart, the score and judgements, and the
	/// high score. <see cref="SoloLiveController"/> fills <see cref="Last"/> as the live ends; it outlives the scene
	/// change, and a co-op room's 上一局结果 reuses it when it is of the same round.
	/// </summary>
	public sealed class LiveResultData
	{
		/// <summary>Total score a custom chart is scored against (see <see cref="ScoreLogic"/>); rank thresholds are rates of it.</summary>
		public const int ScoreBase = 1000000;

		public static LiveResultData Last { get; set; }

		public LiveScore Score;

		public string Title;
		public string Difficulty;
		public int PlayLevel;
		public string JacketPath;

		/// <summary>The best score before this live, or -1 when there was none.</summary>
		public int PreviousBest = -1;

		public bool IsAuto;

		/// <summary>The live ended at life 0 or was retired by a failure.</summary>
		public bool Failed;

		/// <summary>Where 次へ goes after a solo live: the chart manager or the community charts.</summary>
		public MenuScreenType ReturnScreenType;

		public bool IsCoop;

		/// <summary>The co-op round this live was, so the room's 上一局结果 of another round does not show it.</summary>
		public int CoopRound;

		public int HighScore => IsAuto ? Mathf.Max(PreviousBest, 0) : Mathf.Max(PreviousBest, Score.totalScore);

		public bool IsNewRecord => !IsAuto && !Failed && Score.totalScore > PreviousBest && PreviousBest >= 0;

		public static LiveResultData Create(FreeLiveBootData bootData, LiveScore score, int previousBest, bool failed)
		{
			LiveResultData data = new LiveResultData
			{
				Score = score,
				PreviousBest = previousBest,
				IsAuto = bootData?.IsAuto == true,
				Failed = failed,
				ReturnScreenType = bootData?.ReturnScreenType ?? MenuScreenType.MusicScoreMakerTop,
			};
			if (bootData == null)
			{
				return data;
			}
			data.Title = FirstNonEmpty(bootData.MusicData?.Music?.title, bootData.CustomMusicScoreTitle);
			data.Difficulty = FirstNonEmpty(bootData.MusicData?.Difficulty?.musicDifficulty, bootData.MusicData?.DifficultyString);
			data.PlayLevel = bootData.MusicData?.PlayLevel ?? 0;
			if (!string.IsNullOrEmpty(bootData.CustomMusicScorePath))
			{
				try
				{
					data.JacketPath = CustomMusicScoreStorage.LoadEntry(bootData.CustomMusicScorePath)?.JacketPath;
				}
				catch (System.Exception exception)
				{
					Debug.LogWarning($"Result: no jacket for {bootData.CustomMusicScorePath}: {exception.Message}");
				}
			}
			return data;
		}

		/// <summary>The best recorded score of a custom chart, or -1.</summary>
		public static int LoadBest(FreeLiveBootData bootData)
		{
			if (bootData == null || string.IsNullOrEmpty(bootData.CustomMusicScoreId))
			{
				return -1;
			}
			return CustomMusicScorePlayHistoryStorage.TryLoadBestResult(bootData.CustomMusicScoreId, out CustomMusicScorePlayHistoryRecord best) && best != null
				? best.totalScore
				: -1;
		}

		/// <summary>The rank of <paramref name="score"/> out of <paramref name="players"/> players' worth of score.</summary>
		public static ScoreRank RankOf(long score, int players = 1)
		{
			double total = (double)ScoreBase * Mathf.Max(1, players);
			return score >= total * ScoreGaugeCalculator.RankRateS ? ScoreRank.S
				: score >= total * ScoreGaugeCalculator.RankRateA ? ScoreRank.A
				: score >= total * ScoreGaugeCalculator.RankRateB ? ScoreRank.B
				: score >= total * ScoreGaugeCalculator.RankRateC ? ScoreRank.C
				: ScoreRank.D;
		}

		/// <summary>Where <paramref name="score"/> falls on the result gauge (0..1; C, B, A, S sit at their rank rates).</summary>
		public static float GaugeOf(long score, int players = 1)
		{
			int total = ScoreBase * Mathf.Max(1, players);
			return ScoreGaugeCalculator.Create(total).CalculateGaugeProgress((int)System.Math.Min(score, int.MaxValue));
		}

		private static string FirstNonEmpty(string a, string b) => !string.IsNullOrEmpty(a) ? a : b ?? string.Empty;
	}
}
