using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>The round difficulty badge (colour by difficulty, play level, and an "APD" tag for APPEND).</summary>
	public sealed class CustomMusicScoreDifficultyBadge : MonoBehaviour
	{
		[SerializeField]
		private Image circle;

		[SerializeField]
		private TextMeshProUGUI levelText;

		[SerializeField]
		private TextMeshProUGUI appendText;

		private float _baseLevelFontSize;

		public void Set(string difficultyType, int playLevel)
		{
			bool isAppend = string.Equals(difficultyType, "append", StringComparison.OrdinalIgnoreCase);
			if (circle != null)
			{
				circle.color = GetDifficultyColor(difficultyType);
			}
			if (appendText != null)
			{
				appendText.gameObject.SetActive(isAppend);
			}
			if (levelText != null)
			{
				levelText.text = playLevel > 0 ? playLevel.ToString(CultureInfo.InvariantCulture) : "-";
				if (_baseLevelFontSize <= 0f)
				{
					_baseLevelFontSize = levelText.fontSize;
				}
				RectTransform rect = levelText.rectTransform;
				float height = ((RectTransform)transform).rect.height;
				rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, isAppend ? -height * 0.09f : 0f);
				levelText.fontSize = isAppend ? _baseLevelFontSize * 0.82f : _baseLevelFontSize;
			}
		}

		internal void SetReferences(Image circle, TextMeshProUGUI levelText, TextMeshProUGUI appendText)
		{
			this.circle = circle;
			this.levelText = levelText;
			this.appendText = appendText;
		}

		/// <summary>The difficulty colours used by the official chart lists.</summary>
		public static Color32 GetDifficultyColor(string difficultyType)
		{
			switch ((difficultyType ?? string.Empty).ToLowerInvariant())
			{
				case "easy":
					return new Color32(46, 214, 106, 255);
				case "normal":
					return new Color32(51, 190, 240, 255);
				case "hard":
					return new Color32(255, 196, 0, 255);
				case "expert":
					return new Color32(238, 67, 102, 255);
				case "append":
					return new Color32(214, 128, 240, 255);
				default:
					return new Color32(190, 51, 238, 255);
			}
		}
	}
}
