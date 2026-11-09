using DG.Tweening;
using UnityEngine;

namespace Sekai.CoopLive
{
	/// <summary>
	/// The team score bar of the 協力ライブ live UI (the original's <c>MultiScoreView</c>): the team's total score, its
	/// rank, and a gauge split into one segment per player in their slot color. The gauge and rank use the team's
	/// thresholds (one player's 1,000,000 times the number of players), as on the result screen
	/// (<see cref="LiveResultData.GaugeOf"/>). The gauge segments are stacked: segment <c>i</c> starts at the left
	/// and reaches to the end of player <c>i</c>'s share, and earlier segments are drawn on top.
	/// </summary>
	public sealed class MultiScoreView : MonoBehaviour
	{
		[SerializeField]
		private SpriteRenderer[] scoreGaugeSpriteRenderers;

		[SerializeField]
		private SpriteRenderer rankSpriteRenderer;

		[SerializeField]
		private Sprite[] rankSprites;

		[SerializeField]
		private SpriteRenderer rankTextSpriteRenderer;

		// S, A, B, C.
		[SerializeField]
		private Transform[] gaugeRankSprites;

		[SerializeField]
		private NumberView number;

		[SerializeField]
		private NumberView outlineNumber;

		[SerializeField]
		private Transform addScoreRoot;

		[SerializeField]
		private NumberView addScoreNumber;

		[SerializeField]
		private NumberView addScoreOutlineNumber;

		private static readonly float[] RankRates =
		{
			Core.Live.ScoreGaugeCalculator.RankRateS,
			Core.Live.ScoreGaugeCalculator.RankRateA,
			Core.Live.ScoreGaugeCalculator.RankRateB,
			Core.Live.ScoreGaugeCalculator.RankRateC,
		};

		private Core.Live.ScoreGaugeCalculator _calculator;
		private float _gaugeWidth;
		private float _gaugeHeight;
		private int _players = 1;
		private int _lastTotal = -1;
		private int _lastRank = -1;
		private Sequence _addScoreSequence;
		private bool _inverseAddScale;
		private float[] _gaugeStartX;

		/// <summary>Colors the segments for <paramref name="slotColors"/> (slot order) and resets the score.</summary>
		public void Setup(Color[] slotColors)
		{
			_players = Mathf.Max(1, slotColors.Length);
			_calculator = Core.Live.ScoreGaugeCalculator.Create(LiveResultData.ScoreBase * _players);
			SpriteRenderer first = scoreGaugeSpriteRenderers.Length > 0 ? scoreGaugeSpriteRenderers[0] : null;
			if (first != null && _gaugeWidth <= 0f)
			{
				_gaugeWidth = first.size.x;
				_gaugeHeight = first.size.y;
			}
			_gaugeStartX ??= System.Array.ConvertAll(scoreGaugeSpriteRenderers, g => g != null ? g.transform.localPosition.x : 0f);
			for (int i = 0; i < scoreGaugeSpriteRenderers.Length; i++)
			{
				SpriteRenderer gauge = scoreGaugeSpriteRenderers[i];
				if (gauge == null)
				{
					continue;
				}
				gauge.gameObject.SetActive(i < slotColors.Length);
				if (i < slotColors.Length)
				{
					gauge.color = slotColors[i];
				}
				SetGaugeWidth(i, 0f);
			}
			if (first != null)
			{
				for (int i = 0; i < gaugeRankSprites.Length && i < RankRates.Length; i++)
				{
					Transform line = gaugeRankSprites[i];
					if (line == null)
					{
						continue;
					}
					// The gauges sit in their own container; place the line in its parent's space.
					Vector3 start = line.parent.InverseTransformPoint(first.transform.position);
					Vector3 position = line.localPosition;
					position.x = start.x + RankRates[i] * _gaugeWidth * first.transform.lossyScale.x / line.parent.lossyScale.x;
					line.localPosition = position;
				}
			}
			number?.Setup("#ffffff", "#CCCCEE");
			outlineNumber?.Setup("#444466", "#444466");
			addScoreNumber?.Setup("#ffffff", "#ffffff00");
			addScoreOutlineNumber?.Setup("#444466", "#ffffff00");
			if (addScoreRoot != null)
			{
				_inverseAddScale = _inverseAddScale || addScoreRoot.localScale.y < 0f;
				addScoreRoot.localScale = Vector3.zero;
			}
			CreateAddScoreAnimation();
			_lastTotal = -1;
			_lastRank = -1;
			UpdateScores(new int[slotColors.Length]);
		}

		/// <summary>Every player's score so far, in slot order.</summary>
		public void UpdateScores(int[] scores)
		{
			long total = 0;
			foreach (int score in scores)
			{
				total += Mathf.Max(0, score);
			}
			int team = (int)System.Math.Min(total, int.MaxValue);
			if (team == _lastTotal)
			{
				return;
			}
			int added = _lastTotal >= 0 ? team - _lastTotal : 0;
			_lastTotal = team;

			float[] shares = _calculator.CalculateMultiGaugeProgress(scores);
			float reach = 0f;
			for (int i = 0; i < scoreGaugeSpriteRenderers.Length; i++)
			{
				SpriteRenderer gauge = scoreGaugeSpriteRenderers[i];
				if (gauge == null || !gauge.gameObject.activeSelf)
				{
					continue;
				}
				reach += i < shares.Length && !float.IsNaN(shares[i]) ? shares[i] : 0f;
				SetGaugeWidth(i, Mathf.Clamp01(reach) * _gaugeWidth);
			}

			number?.UpdateNumber(team);
			outlineNumber?.UpdateNumber(team);

			ScoreRank rank = LiveResultData.RankOf(team, _players);
			if (rankSpriteRenderer != null && _lastRank != (int)rank)
			{
				int index = (int)rank;
				rankSpriteRenderer.sprite = rankSprites != null && index >= 0 && index < rankSprites.Length ? rankSprites[index] : null;
				if (rankTextSpriteRenderer != null)
				{
					rankTextSpriteRenderer.color = ColorUtility.GetScoreRankColor(rank);
				}
				_lastRank = index;
			}

			if (added > 0 && addScoreRoot != null)
			{
				addScoreRoot.localScale = _inverseAddScale ? new Vector3(1f, -1f, 1f) : Vector3.one;
				addScoreNumber?.UpdateNumber(added);
				addScoreOutlineNumber?.UpdateNumber(added);
				_addScoreSequence?.Restart();
			}
		}

		// Every segment starts at the gauge's left end: a sprite pivoted elsewhere than its left edge (the placeholder's
		// is centred) is moved by the part of its width left of the pivot.
		private void SetGaugeWidth(int index, float width)
		{
			SpriteRenderer gauge = scoreGaugeSpriteRenderers[index];
			gauge.size = new Vector2(width, _gaugeHeight);
			Sprite sprite = gauge.sprite;
			float pivot = sprite != null && sprite.rect.width > 0f ? sprite.pivot.x / sprite.rect.width : 0f;
			Vector3 position = gauge.transform.localPosition;
			position.x = _gaugeStartX[index] + pivot * width;
			gauge.transform.localPosition = position;
		}

		private void CreateAddScoreAnimation()
		{
			if (addScoreRoot == null)
			{
				return;
			}
			_addScoreSequence?.Kill();
			Vector3 start = addScoreRoot.localPosition;
			Vector3 from = new Vector3(-0.5f, start.y, start.z);
			Vector3 to = new Vector3(0f, start.y, start.z);
			_addScoreSequence = DOTween.Sequence();
			_addScoreSequence.Pause();
			_addScoreSequence.AppendCallback(() =>
			{
				addScoreRoot.localPosition = from;
				SetAddAlpha(0f);
			});
			_addScoreSequence.Append(addScoreRoot.DOLocalMove(to, 0.2f).SetEase(Ease.OutQuad));
			_addScoreSequence.Join(DOTween.To(() => 0f, SetAddAlpha, 1f, 0.2f).SetEase(Ease.OutQuad));
			_addScoreSequence.AppendInterval(0.5f);
			_addScoreSequence.AppendCallback(() => addScoreRoot.localScale = Vector3.zero);
			_addScoreSequence.SetAutoKill(false);
		}

		private void SetAddAlpha(float alpha)
		{
			addScoreNumber?.UpdateAlpha(alpha);
			addScoreOutlineNumber?.UpdateAlpha(alpha);
		}

		private void OnDestroy()
		{
			_addScoreSequence?.Kill();
		}
	}
}
