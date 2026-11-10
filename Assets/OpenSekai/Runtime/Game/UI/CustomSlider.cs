using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sekai.UI
{
	[AddComponentMenu("Custom UI/Custom Slider")]
	public class CustomSlider : Slider
	{
		[SerializeField]
		private bool m_isUseExtension;

		[SerializeField]
		private bool m_isUseSliderSE;

		[SerializeField]
		private bool m_isUsePositionOneValueMax;

		private const string SliderUpCue = "SE_UI_GAUGE_SLIDER_UP";
		private const string SliderDownCue = "SE_UI_GAUGE_SLIDER_DOWN";
		private const float SliderSeIntervalSec = 0.08f;

		private float currentValue;
		private float lastSliderSeTime = float.NegativeInfinity;

		private Action<float> _onDragCallback;

		public bool IsSEPlaying
		{
			[CompilerGenerated]
			get
			{
				return _isSEPlaying;
			}
			[CompilerGenerated]
			set
			{
				_isSEPlaying = value;
			}
		}

		private bool _isSEPlaying;

		protected override void Awake()
		{
			base.Awake();
			currentValue = value;
			if (m_isUseExtension)
			{
				SetOnValueChangeEvent();
			}
		}

		public void SetOnDrag(Action<float> onDrag)
		{
			_onDragCallback = onDrag;
		}

		public void SetSliderPositionOneValueMax()
		{
			if (m_isUsePositionOneValueMax && maxValue > minValue && value >= maxValue)
			{
				SetValueWithoutNotify(maxValue);
			}
		}

		public void SetOnValueChangeEvent()
		{
			onValueChanged.RemoveListener(OnValueChange);
			onValueChanged.AddListener(OnValueChange);
		}

		public void RemoveAllAndAddListener(Action<float> action)
		{
			onValueChanged.RemoveAllListeners();
			if (action != null)
			{
				onValueChanged.AddListener(action.Invoke);
			}
		}

		public void RemoveAllListeners()
		{
			onValueChanged.RemoveAllListeners();
		}

		public override void OnPointerDown(PointerEventData eventData)
		{
			currentValue = value;
			float before = value;
			base.OnPointerDown(eventData);
			PlaySliderSe(before);
			_onDragCallback?.Invoke(value);
		}

		public override void OnDrag(PointerEventData eventData)
		{
			float before = value;
			base.OnDrag(eventData);
			PlaySliderSe(before);
			_onDragCallback?.Invoke(value);
		}

		/// <summary>The gauge sound of a slider the player moves (only with m_isUseSliderSE), at most every
		/// <see cref="SliderSeIntervalSec"/>.</summary>
		private void PlaySliderSe(float before)
		{
			if (!m_isUseSliderSE || Mathf.Approximately(before, value) || Time.unscaledTime - lastSliderSeTime < SliderSeIntervalSec)
			{
				return;
			}

			lastSliderSeTime = Time.unscaledTime;
			IsSEPlaying = true;
			SoundManager.Instance?.PlaySEOneShot(value > before ? SliderUpCue : SliderDownCue, 0);
		}

		private void OnValueChange(float value)
		{
			currentValue = value;
			if (m_isUseSliderSE)
			{
				IsSEPlaying = true;
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			currentValue = value;
			if (m_isUsePositionOneValueMax)
			{
				SetSliderPositionOneValueMax();
			}
		}

		public CustomSlider()
		{
		}
	}
}
