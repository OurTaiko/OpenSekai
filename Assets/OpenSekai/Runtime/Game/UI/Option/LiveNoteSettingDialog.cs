using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// The official detail dialog opened by オプション > プレビューで詳細設定 (<see cref="DialogType.LiveNoteSettingDialog"/>),
	/// built from <see cref="OptionCatalog.NoteDetail"/>: ノーツの速さ and タイミング調整 in steps down to 0.01, and
	/// ノーツの開始位置. It edits a draft; OK copies the three values into the option dialog's settings, キャンセル drops them.
	/// </summary>
	public sealed class LiveNoteSettingDialog : Common2ButtonDialog, IOptionRowHost
	{
		// No tab band here: gap above the content (64) + footer (144).
		private const float DialogChromeHeight = 208f;

		[SerializeField]
		private OptionRowLibrary library;

		[SerializeField]
		private LayoutElement contentRoot;

		[SerializeField]
		private RectTransform body;

		private OptionSettings target;
		private OptionSettings draft;
		private Action onDecide;
		private readonly Dictionary<string, OptionRow> rows = new Dictionary<string, OptionRow>();
		private readonly List<OptionRowView> views = new List<OptionRowView>();

		public OptionSettings Settings => draft;

		public override void Initialize(DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true)
		{
			Initialize(null, null, null, Decide, null, dialogSize, allowCloseExternal);
			OptionDialog.FitContentHeight(this, contentRoot, DialogChromeHeight);
		}

		public void Setup(OptionSettings settings, Action decide)
		{
			target = settings;
			onDecide = decide;
			var live = new LiveSettingData
			{
				NoteSpeed = settings.Live.NoteSpeed,
				TimingAdjustData = settings.Live.TimingAdjustData,
				_noteShowRate = settings.Live._noteShowRate,
			};
			draft = new OptionSettings(live, settings.Local);

			OptionPage page = OptionCatalog.NoteDetail();
			rows.Clear();
			foreach (OptionSection section in page.Sections)
			{
				foreach (OptionBlock block in section.Blocks)
				{
					foreach (OptionRow row in block.Rows)
					{
						rows[row.Id] = row;
					}
				}
			}
			views.AddRange(OptionPageView.Build(page, body, library, this));
			foreach (OptionRowView view in views)
			{
				view.Refresh(draft);
			}
		}

		public void OnRowChanged(OptionRow row)
		{
			foreach (OptionRowView view in views)
			{
				view.Refresh(draft);
			}
		}

		public void RunAction(string action)
		{
		}

		public OptionRow FindRow(string id)
		{
			return id != null && rows.TryGetValue(id, out OptionRow row) ? row : null;
		}

		private void Decide()
		{
			if (target == null || draft == null)
			{
				return;
			}
			target.Live.NoteSpeed = draft.Live.NoteSpeed;
			target.Live.TimingAdjustData = draft.Live.TimingAdjustData;
			target.Live._noteShowRate = draft.Live._noteShowRate;
			onDecide?.Invoke();
		}
	}
}
