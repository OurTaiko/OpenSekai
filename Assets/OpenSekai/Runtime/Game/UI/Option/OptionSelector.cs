using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// The official "&lt; value &gt;" selector: ノーツスキン (one preview page per skin) and ノーツのSE (a label per sound).
	/// The arrows stop at the ends.
	/// </summary>
	public sealed class OptionSelector : MonoBehaviour
	{
		[SerializeField]
		private Button previousButton;

		[SerializeField]
		private Button nextButton;

		[SerializeField]
		private TMP_Text label;

		[SerializeField]
		private string[] labels;

		[SerializeField]
		private GameObject[] pages;

		private int value;
		private bool bound;

		public event Action<int> OnValueChanged;

		public int Value => value;

		private int Count => Mathf.Max(labels != null ? labels.Length : 0, pages != null ? pages.Length : 0);

		private void Awake()
		{
			Bind();
		}

		public void SetLabels(string[] newLabels)
		{
			labels = newLabels;
		}

		public void SetValueWithoutNotify(int newValue)
		{
			Bind();
			value = Mathf.Clamp(newValue, 0, Mathf.Max(0, Count - 1));
			Refresh();
		}

		private void Bind()
		{
			if (bound)
			{
				return;
			}

			bound = true;
			previousButton?.onClick.AddListener(() => Change(value - 1));
			nextButton?.onClick.AddListener(() => Change(value + 1));
		}

		private void Change(int newValue)
		{
			newValue = Mathf.Clamp(newValue, 0, Mathf.Max(0, Count - 1));
			if (newValue == value)
			{
				return;
			}

			value = newValue;
			Refresh();
			OnValueChanged?.Invoke(value);
		}

		private void Refresh()
		{
			if (label != null && labels != null && value < labels.Length)
			{
				label.text = labels[value];
			}
			if (pages != null)
			{
				for (int i = 0; i < pages.Length; i++)
				{
					pages[i]?.SetActive(i == value);
				}
			}
			if (previousButton != null)
			{
				previousButton.interactable = value > 0;
			}
			if (nextButton != null)
			{
				nextButton.interactable = value < Count - 1;
			}
		}
	}
}
