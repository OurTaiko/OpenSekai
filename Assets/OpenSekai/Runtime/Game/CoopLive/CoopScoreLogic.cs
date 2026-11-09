using System;
using Sekai.Core.Live;
using Sekai.Live;

namespace Sekai.CoopLive
{
	/// <summary>The solo score logic, except that notes judged during FEVER score <see cref="FeverFactor"/> times.</summary>
	public sealed class CoopScoreLogic : ScoreLogic
	{
		public const float FeverFactor = 1.5f;

		private readonly Func<bool> _feverActive;

		public CoopScoreLogic(LiveBundleBuildData data, Func<bool> feverActive) : base(data)
		{
			_feverActive = feverActive;
		}

		public override int CalculateAddScore(NoteBase note, float factor = 1f)
		{
			return base.CalculateAddScore(note, _feverActive() ? factor * FeverFactor : factor);
		}
	}
}
