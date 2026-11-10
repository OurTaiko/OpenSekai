using UnityEngine;

namespace Sekai
{
	/// <summary>A <see cref="OptionRowKind.Volume"/> row of the 音量 tab: label, − , the slider with its value, +.</summary>
	public sealed class OptionVolumeRowView : OptionRowView
	{
		private const string GaugeUpCue = "SE_UI_GAUGE_SLIDER_UP";
		private const string GaugeDownCue = "SE_UI_GAUGE_SLIDER_DOWN";
		/// <summary>A slider drag changes the value every frame; the sound restarts at most this often.</summary>
		private const float PreviewIntervalSec = 0.1f;

		private static uint previewPlaybackId;
		private static float lastPreviewTime = float.NegativeInfinity;

		[SerializeField]
		private OptionLabelView label;

		[SerializeField]
		private OptionVolumeSlider slider;

		private int shownValue;

		protected override void OnBind(OptionRowStyle style)
		{
			label.Setup(Row.Label, 135f, 46f);
			slider.OnValueChanged -= OnValueChanged;
			slider.OnValueChanged += OnValueChanged;
		}

		public override void Refresh(OptionSettings settings)
		{
			shownValue = Mathf.RoundToInt(Row.GetNumber(settings));
			slider.SetValueWithoutNotify(shownValue);
		}

		private void OnValueChanged(int value)
		{
			int previous = shownValue;
			Row.SetNumber(Host.Settings, value);
			Changed();
			PlayPreview(value, previous);
		}

		/// <summary>
		/// Plays the row's SE_VOLCHANGE_* cue at the new volume, as the original does; BGM rows play the gauge sound.
		/// The menus hear SE and voices at the system volume (applied by the commit just before), so a ライブ row scales its
		/// cue against that; it cannot go louder than the system volume.
		/// </summary>
		private void PlayPreview(int value, int previous)
		{
			SoundManager sound = SoundManager.Instance;
			if (sound == null || Time.unscaledTime - lastPreviewTime < PreviewIntervalSec)
			{
				return;
			}

			lastPreviewTime = Time.unscaledTime;
			sound.StopSE(previewPlaybackId);
			if (string.IsNullOrEmpty(Row.PreviewCue))
			{
				previewPlaybackId = sound.PlaySE(value >= previous ? GaugeUpCue : GaugeDownCue);
				return;
			}

			ApplicationLocalSettings.VolumeSettings system = Host.Settings.SystemVolume;
			float heardAt = Row.PreviewIsVoice ? system.Voice : system.Se;
			float volume = heardAt > 0f ? Mathf.Clamp01(value / 100f / heardAt) : 0f;
			previewPlaybackId = Row.PreviewIsVoice ? sound.PlayVoice(Row.PreviewCue, volume) : sound.PlaySE(Row.PreviewCue, volume);
		}
	}
}
