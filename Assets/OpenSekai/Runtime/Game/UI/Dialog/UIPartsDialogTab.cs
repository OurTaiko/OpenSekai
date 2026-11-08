using System;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	[RequireComponent(typeof(CustomToggle))]
	public class UIPartsDialogTab : MonoBehaviour
	{
		[SerializeField]
		private CustomImage backgroundImage;

		[SerializeField]
		private CustomTextMesh text;

		[SerializeField]
		private GameObject badge;

		[SerializeField]
		private GameObject lineObj;

		private static readonly Color SelectedTextColor = new Color32(0x45, 0x45, 0x66, 0xFF);

		private bool isOn;

		private Color backgroundColor = Color.white;

		private bool hasBackgroundColor;

		private CustomToggle toggle;

		private Action onValueChanged;

		public bool IsOn
		{
			get
			{
				return toggle != null ? toggle.isOn : isOn;
			}
			set
			{
				isOn = value;
				if (toggle != null)
				{
					toggle.isOn = value;
				}
				if (value)
				{
					On();
				}
				else
				{
					Off();
				}
			}
		}

		public bool IsShowBadge
		{
			get
			{
				return badge != null && badge.activeSelf;
			}
			set
			{
				if (badge != null)
				{
					badge.SetActive(value);
				}
			}
		}

		protected void Awake()
		{
			toggle = GetComponent<CustomToggle>();
			CacheBackgroundColor();
		}

		private void CacheBackgroundColor()
		{
			if (!hasBackgroundColor && backgroundImage != null)
			{
				backgroundColor = backgroundImage.color;
				backgroundColor.a = 1f;
				hasBackgroundColor = true;
			}
		}

		protected void OnEnable()
		{
			if (toggle == null)
			{
				toggle = GetComponent<CustomToggle>();
			}
			if (toggle != null)
			{
				toggle.onValueChanged.RemoveListener(Change);
				toggle.onValueChanged.AddListener(Change);
				Change(toggle.isOn);
			}
		}

		protected void OnDisable()
		{
			if (toggle != null)
			{
				toggle.onValueChanged.RemoveListener(Change);
			}
		}

		private void On()
		{
			isOn = true;
			CacheBackgroundColor();
			// As in the original option dialog: only the selected tab is filled (in the dialog's own colour) and reads dark.
			if (backgroundImage != null)
			{
				backgroundImage.color = backgroundColor;
			}
			if (text != null)
			{
				text.color = SelectedTextColor;
			}
		}

		private void Off()
		{
			isOn = false;
			CacheBackgroundColor();
			// Unselected tabs are just white labels on the tab band.
			if (backgroundImage != null)
			{
				Color clear = backgroundColor;
				clear.a = 0f;
				backgroundImage.color = clear;
			}
			if (text != null)
			{
				text.color = Color.white;
			}
		}

		public void Setup(Action onValueChanged)
		{
			this.onValueChanged = onValueChanged;
			if (toggle == null)
			{
				toggle = GetComponent<CustomToggle>();
			}
			if (toggle != null)
			{
				toggle.onValueChanged.RemoveListener(Change);
				toggle.onValueChanged.AddListener(Change);
			}
		}

		private void Change(bool isOn)
		{
			if (isOn)
			{
				On();
			}
			else
			{
				Off();
			}
			onValueChanged?.Invoke();
		}

		public void ShowLine()
		{
			if (lineObj != null)
			{
				lineObj.SetActive(true);
			}
		}

		public void HideLine()
		{
			if (lineObj != null)
			{
				lineObj.SetActive(false);
			}
		}

		public UIPartsDialogTab()
		{
		}
	}
}
