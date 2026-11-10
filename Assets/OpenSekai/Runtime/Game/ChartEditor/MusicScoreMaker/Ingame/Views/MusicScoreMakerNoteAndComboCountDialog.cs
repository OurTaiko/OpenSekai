using System.IO;
using Sekai.MusicScoreMaker.Common;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using Sekai.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.MusicScoreMaker.Ingame.Views
{
	/// <summary>
	/// ノーツ数確認: how many notes of each kind the chart has, its total combo, the difficulty that combo suggests for the
	/// song's length and the combo range of each difficulty (<see cref="MusicScoreDifficultyEstimator"/>).
	/// </summary>
	public class MusicScoreMakerNoteAndComboCountDialog : Common1ButtonDialog
	{
		[SerializeField]
		private RawImage _jacketImage;

		[SerializeField]
		private CustomTextMesh _tapCountText;

		[SerializeField]
		private CustomTextMesh _longCountText;

		[SerializeField]
		private CustomTextMesh _flickCountText;

		[SerializeField]
		private CustomTextMesh _traceCountText;

		[SerializeField]
		private CustomTextMesh _middleNoteCountText;

		[SerializeField]
		private CustomTextMesh _totalComboCountText;

		[SerializeField]
		private UIPartsMusicDifficultyLabel _estimatedDifficultyLabel;

		/// <summary>EASY to MASTER.</summary>
		[SerializeField]
		private UIPartsMusicDifficultyLabel[] _difficultyLabels;

		/// <summary>The combo range of each of <see cref="_difficultyLabels"/>.</summary>
		[SerializeField]
		private CustomTextMesh[] _difficultyThresholdTexts;

		[SerializeField]
		private GameObject _changeRateRoot;

		[SerializeField]
		private CustomTextMesh _changeRateText;

		[SerializeField]
		private CustomTextMesh _changeRateThresholdText;

		private Texture2D _jacketTexture;

		public void Setup(NoteAndComboCountInfo countInfo, string jacketPath, MusicDifficulty estimatedDifficulty, int durationSec)
		{
			SetText(_tapCountText, countInfo.TapCount);
			SetText(_longCountText, countInfo.LongCount);
			SetText(_flickCountText, countInfo.FlickCount);
			SetText(_traceCountText, countInfo.TraceCount);
			SetText(_middleNoteCountText, countInfo.MiddleNoteCount);
			SetText(_totalComboCountText, countInfo.TotalComboCount);
			if (_estimatedDifficultyLabel != null)
			{
				_estimatedDifficultyLabel.Setup(estimatedDifficulty);
				_estimatedDifficultyLabel.Show();
			}
			SetupDifficultyThresholdDisplay(durationSec);
			LoadJacket(jacketPath);
		}

		public void SetChangeRateSectionVisible(bool visible)
		{
			if (_changeRateRoot != null)
			{
				_changeRateRoot.SetActive(visible);
			}
		}

		public void SetupChangeRate(float? changeRate, float threshold)
		{
			if (_changeRateText != null)
			{
				if (changeRate.HasValue)
				{
					float displayedRate = MusicScoreMakerUtility.RoundToDisplayedPercent(changeRate.Value);
					float displayedThreshold = MusicScoreMakerUtility.RoundToDisplayedPercent(threshold);
					_changeRateText.text = displayedRate.ToString("0.0") + "%";
					_changeRateText.color = displayedRate < displayedThreshold ? Color.red : TextColor;
				}
				else
				{
					_changeRateText.text = "-";
					_changeRateText.color = TextColor;
				}
			}
			if (_changeRateThresholdText != null)
			{
				_changeRateThresholdText.text = string.Format("≥ {0:0.0}%", threshold * 100f);
			}
		}

		private static readonly Color TextColor = new Color32(0x55, 0x55, 0x77, 0xFF);

		/// <summary>
		/// EASY to EXPERT go up to the estimator's maximum for the song's length; MASTER is everything above EXPERT's.
		/// </summary>
		private void SetupDifficultyThresholdDisplay(int durationSec)
		{
			if (_difficultyLabels == null || _difficultyThresholdTexts == null)
			{
				return;
			}
			int[] maxValues = MusicScoreDifficultyEstimator.GetLevelDesignMaxValuesFromDuration(durationSec);
			for (int i = 0; i < _difficultyLabels.Length; i++)
			{
				MusicDifficulty difficulty = (MusicDifficulty)(i + 1);
				if (_difficultyLabels[i] != null)
				{
					_difficultyLabels[i].Setup(difficulty);
					_difficultyLabels[i].Show();
				}
				if (i >= _difficultyThresholdTexts.Length || _difficultyThresholdTexts[i] == null)
				{
					continue;
				}
				string range;
				if (i < maxValues.Length)
				{
					range = "～" + maxValues[i];
				}
				else
				{
					range = maxValues.Length > 0 ? (maxValues[maxValues.Length - 1] + 1) + "～" : string.Empty;
				}
				_difficultyThresholdTexts[i].text = range;
			}
		}

		private void LoadJacket(string jacketPath)
		{
			ReleaseJacket();
			if (_jacketImage == null)
			{
				return;
			}
			if (!string.IsNullOrEmpty(jacketPath) && File.Exists(jacketPath))
			{
				try
				{
					Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
					if (texture.LoadImage(File.ReadAllBytes(jacketPath)))
					{
						_jacketTexture = texture;
					}
					else
					{
						Destroy(texture);
					}
				}
				catch (IOException exception)
				{
					Debug.LogWarning($"MusicScoreMaker: jacket {jacketPath}: {exception.Message}");
				}
			}
			_jacketImage.texture = _jacketTexture;
			_jacketImage.enabled = _jacketTexture != null;
		}

		private void ReleaseJacket()
		{
			if (_jacketImage != null)
			{
				_jacketImage.texture = null;
			}
			if (_jacketTexture != null)
			{
				Destroy(_jacketTexture);
				_jacketTexture = null;
			}
		}

		private void OnDestroy()
		{
			ReleaseJacket();
		}

		private static void SetText(CustomTextMesh text, int value)
		{
			if (text != null)
			{
				text.text = value.ToString();
			}
		}
	}
}
