using System;
using Sekai.MusicScoreMaker.Common;

namespace Sekai.CustomMusicScoreManager
{
	public sealed class CustomMusicScoreManagerItem
	{
		public CustomMusicScoreEntry Entry { get; }

		public DateTime LastWriteTime { get; }

		public bool HasManifest { get; }

		public bool HasScore { get; }

		public bool HasAudio { get; }

		public bool HasJacket { get; }

		public string StatusText
		{
			get
			{
				if (!HasManifest)
				{
					return L10n.Get("ChartManager", "item.missingManifest");
				}
				if (!HasScore)
				{
					return L10n.Get("ChartManager", "item.missingScore");
				}
				if (!HasAudio)
				{
					return L10n.Get("ChartManager", "item.missingAudio");
				}
				if (!HasJacket)
				{
					return L10n.Get("ChartManager", "item.missingJacket");
				}
				return L10n.Get("ChartManager", "item.ready");
			}
		}

		public bool IsReadyForEdit => HasManifest;

		public CustomMusicScoreManagerItem(
			CustomMusicScoreEntry entry,
			DateTime lastWriteTime,
			bool hasManifest,
			bool hasScore,
			bool hasAudio,
			bool hasJacket)
		{
			Entry = entry;
			LastWriteTime = lastWriteTime;
			HasManifest = hasManifest;
			HasScore = hasScore;
			HasAudio = hasAudio;
			HasJacket = hasJacket;
		}
	}
}
