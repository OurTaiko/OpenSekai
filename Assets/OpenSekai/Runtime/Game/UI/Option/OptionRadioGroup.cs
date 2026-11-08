using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// The official option radio row ("◯ ON ◯ OFF", "◯ 標準 ◯ 控えめ", ...). Each option is a button whose
	/// mark (the teal dot) is shown while it is selected. Boolean settings use index 0 for ON, 1 for OFF.
	/// </summary>
	public sealed class OptionRadioGroup : MonoBehaviour
	{
		[SerializeField]
		private Button[] options;

		[SerializeField]
		private GameObject[] marks;

		private int value;
		private bool bound;

		public event Action<int> OnValueChanged;

		public int Value => value;

		private void Awake()
		{
			Bind();
		}

		public void SetValueWithoutNotify(int newValue)
		{
			Bind();
			value = Mathf.Clamp(newValue, 0, Mathf.Max(0, Count - 1));
			Refresh();
		}

		public void SetOnWithoutNotify(bool isOn)
		{
			SetValueWithoutNotify(isOn ? 0 : 1);
		}

		private int Count => options != null ? options.Length : 0;

		private void Bind()
		{
			if (bound || options == null)
			{
				return;
			}

			bound = true;
			for (int i = 0; i < options.Length; i++)
			{
				int index = i;
				options[i]?.onClick.AddListener(() => Select(index));
			}
		}

		private void Select(int index)
		{
			if (index == value)
			{
				return;
			}

			value = index;
			Refresh();
			OnValueChanged?.Invoke(value);
		}

		private void Refresh()
		{
			if (marks == null)
			{
				return;
			}

			for (int i = 0; i < marks.Length; i++)
			{
				marks[i]?.SetActive(i == value);
			}
		}
	}
}
