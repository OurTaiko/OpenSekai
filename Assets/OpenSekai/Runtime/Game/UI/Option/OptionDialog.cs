using System.Collections;
using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// The official オプション dialog (<see cref="DialogType.OptionDialog"/>), rebuilt for OpenSekai and organised like
	/// OurTaikoPlay's settings overlay: <see cref="OptionCatalog"/> declares the pages and rows, <see cref="OptionSettings"/>
	/// loads, saves and applies them, <see cref="OptionPageView"/> builds each tab page from the row prefabs in
	/// <see cref="OptionRowLibrary"/>, and this controller connects them. Tabs: ライブ / 音量 / システム like the original,
	/// and Additionals for OpenSekai's own settings in place of コミュニケーション. Every change is saved and applied at
	/// once; the footer button only closes.
	/// </summary>
	public sealed class OptionDialog : Common1ButtonDialog, IOptionRowHost
	{
		// The original dialog is 1033 units tall (measured on a 4:3 screen) and leaves at least this much screen above
		// and below it on shorter screens.
		private const float FullDialogHeight = 1033f;
		private const float MinScreenMargin = 40f;
		private const float MinContentHeight = 300f;

		// Tab band (72) + gap under the tabs (64) + footer (144).
		private const float DialogChromeHeight = 280f;

		private const float SeTestLongDuration = 0.6f;

		[SerializeField]
		private OptionRowLibrary library;

		[SerializeField]
		private LayoutElement contentRoot;

		[SerializeField]
		private UIPartsDialogTabGroup tabGroup;

		[SerializeField]
		private GameObject[] tabPages;

		[SerializeField]
		private ScrollRect[] tabScrolls;

		[SerializeField]
		private RectTransform[] pageContents;

		private OptionSettings settings;
		private readonly Dictionary<string, OptionRow> rows = new Dictionary<string, OptionRow>();
		private readonly List<OptionRowView> views = new List<OptionRowView>();
		private string loadedSeBundle;
		private uint longSePlaybackId;
		private Coroutine stopLongSeRoutine;

		public OptionSettings Settings => settings;

		public override void Initialize(DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true)
		{
			// The footer button is 閉じる: it only closes, because every change is already saved.
			Initialize(null, null, null, dialogSize, allowCloseExternal);
			FitContentHeight(this, contentRoot, DialogChromeHeight);
			settings = OptionSettings.Load();
			BuildPages(OptionCatalog.Pages());
			RefreshRows();
			SetupTabs();
		}

		private void BuildPages(IReadOnlyList<OptionPage> pages)
		{
			rows.Clear();
			foreach (OptionPage page in pages)
			{
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
			}
			for (int i = 0; i < pages.Count && i < pageContents.Length; i++)
			{
				views.AddRange(OptionPageView.Build(pages[i], pageContents[i], library, this));
			}
		}

		private void RefreshRows()
		{
			foreach (OptionRowView view in views)
			{
				view.Refresh(settings);
			}
		}

		public void OnRowChanged(OptionRow row)
		{
			settings.Commit();
			RefreshRows();
		}

		public OptionRow FindRow(string id)
		{
			return id != null && rows.TryGetValue(id, out OptionRow row) ? row : null;
		}

		public void RunAction(string action)
		{
			switch (action)
			{
				case OptionCatalog.ActionNoteDetail:
					OpenNoteDetail();
					break;
				case OptionCatalog.ActionSeTap:
					PlaySeTest(LiveSoundDefine.SE_LIVE_PERFECT);
					break;
				case OptionCatalog.ActionSeFlick:
					PlaySeTest(LiveSoundDefine.SE_LIVE_FLICK);
					break;
				case OptionCatalog.ActionSeLong:
					PlaySeTest(LiveSoundDefine.SE_LIVE_LONG);
					break;
				case OptionCatalog.ActionSeTrace:
					PlaySeTest(LiveSoundDefine.SE_LIVE_TRACE);
					break;
			}
		}

		/// <summary>
		/// Sizes a dialog's content so the whole dialog is as tall as the original's (<see cref="FullDialogHeight"/>) when
		/// the screen allows, and keeps <see cref="MinScreenMargin"/> above and below otherwise.
		/// </summary>
		public static void FitContentHeight(Component dialog, LayoutElement content, float chromeHeight)
		{
			Canvas canvas = content != null ? dialog.GetComponentInParent<Canvas>() : null;
			if (canvas == null)
			{
				return;
			}

			float screenHeight = ((RectTransform)canvas.rootCanvas.transform).rect.height;
			float dialogHeight = Mathf.Min(FullDialogHeight, screenHeight - MinScreenMargin * 2f);
			content.preferredHeight = Mathf.Max(MinContentHeight, dialogHeight - chromeHeight);
		}

		private void SetupTabs()
		{
			if (tabGroup == null)
			{
				return;
			}

			tabGroup.Setup();
			Sekai.UI.CustomIndexToggleGroup group = tabGroup.ToggleGroup;
			if (group != null)
			{
				group.OnSelectedIndexChanged += ShowTab;
				group.SelectedIndex = 0;
			}
			ShowTab(0);
		}

		private void ShowTab(int index)
		{
			for (int i = 0; i < tabPages.Length; i++)
			{
				tabPages[i]?.SetActive(i == index);
			}
			if (index >= 0 && index < tabScrolls.Length && tabScrolls[index] != null)
			{
				// Content is pivoted at its top, so a zero position shows the top of the page even before layout runs.
				ScrollRect scroll = tabScrolls[index];
				scroll.StopMovement();
				if (scroll.content != null)
				{
					scroll.content.anchoredPosition = Vector2.zero;
				}
			}
		}

		private void OpenNoteDetail()
		{
			var dialog = ScreenManager.Instance?.ShowDialog<LiveNoteSettingDialog>(DialogType.LiveNoteSettingDialog);
			dialog?.Setup(settings, () =>
			{
				settings.Commit();
				RefreshRows();
			});
		}

		private void PlaySeTest(string cue)
		{
			string bundle = "live/tap_se/" + LiveConfig.GetNoteSeName(settings.Live.NoteSeIndex);
			if (loadedSeBundle != bundle)
			{
				SoundManager.Instance.LoadSoundBundle(bundle, false);
				loadedSeBundle = bundle;
			}

			if (cue != LiveSoundDefine.SE_LIVE_LONG)
			{
				SoundManager.Instance.PlaySE(cue);
				return;
			}

			// The long-note SE loops in a live; the test plays a short stretch of it.
			StopLongSeTest();
			longSePlaybackId = SoundManager.Instance.PlaySE(cue);
			stopLongSeRoutine = StartCoroutine(StopLongSeAfterDelay());
		}

		private IEnumerator StopLongSeAfterDelay()
		{
			yield return new WaitForSecondsRealtime(SeTestLongDuration);
			stopLongSeRoutine = null;
			StopLongSeTest();
		}

		private void StopLongSeTest()
		{
			if (stopLongSeRoutine != null)
			{
				StopCoroutine(stopLongSeRoutine);
				stopLongSeRoutine = null;
			}
			if (longSePlaybackId != 0)
			{
				SoundManager.Instance.StopSE(longSePlaybackId);
				longSePlaybackId = 0;
			}
		}

		protected override void OnDisable()
		{
			StopLongSeTest();
			base.OnDisable();
		}
	}
}
