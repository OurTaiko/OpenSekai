using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// The official detail dialog opened by オプション > プレビューで詳細設定 (<see cref="DialogType.LiveNoteSettingDialog"/>):
	/// ノーツの速さ and タイミング調整 in steps down to 0.01, and ノーツの開始位置. キャンセル discards, OK hands the
	/// three values back to the option dialog.
	/// </summary>
	public sealed class LiveNoteSettingDialog : Common2ButtonDialog
	{
		// No tab band here: gap above the content (64) + footer (144).
		private const float DialogChromeHeight = 208f;

		[SerializeField]
		private LayoutElement contentRoot;

		[SerializeField]
		private OptionStepper noteSpeed;

		[SerializeField]
		private OptionStepper startPosition;

		[SerializeField]
		private OptionStepper timingAdjust;

		private Action<float, float, float> onDecide;

		public override void Initialize(DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true)
		{
			Initialize(null, null, null, Decide, null, dialogSize, allowCloseExternal);
			OptionDialog.FitContentHeight(this, contentRoot, DialogChromeHeight);
		}

		public void Setup(float speed, float start, float timing, Action<float, float, float> decide)
		{
			onDecide = decide;
			noteSpeed?.SetValueWithoutNotify(speed);
			startPosition?.SetValueWithoutNotify(start);
			timingAdjust?.SetValueWithoutNotify(timing);
		}

		private void Decide()
		{
			onDecide?.Invoke(
				noteSpeed != null ? noteSpeed.Value : 0f,
				startPosition != null ? startPosition.Value : 0f,
				timingAdjust != null ? timingAdjust.Value : 0f);
		}
	}
}
