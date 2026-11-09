using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Platform;
using Sekai.Community;
using Sekai.Live;
using Sekai.MusicScoreMaker.Common;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Presenters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.CustomMusicScoreManager
{
	public sealed class ScreenLayerCustomMusicScoreManager : ScreenLayer
	{
		private const string BaseFontEbPath = "font/FOT-RodinNTLGPro-EB SDF_Base";

		private const string DynamicFontEbPath = "font/FOT-RodinNTLGPro-EB SDF_Dynamic";

		private const string BaseFontDbPath = "font/FOT-RodinNTLGPro-DB SDF_Base";

		private const string DynamicFontDbPath = "font/FOT-RodinNTLGPro-DB SDF_Dynamic";

		private const int MaxManifestFieldColumnCount = 2;

		private const float ManifestFieldMinWidth = 260f;

		private const float ManifestFieldSpacingX = 18f;

		private const float ManifestFieldLeftPadding = 8f;

		private const float ManifestFieldRightScrollPadding = 24f;

		private static readonly string[] DifficultyTypes =
		{
			"easy",
			"normal",
			"hard",
			"expert",
			"master",
			"append"
		};

		private static readonly string[] DifficultyDisplayNames =
		{
			"EASY",
			"NORMAL",
			"HARD",
			"EXPERT",
			"MASTER",
			"APPEND"
		};

		private static TMP_FontAsset _baseFontEB;

		private static TMP_FontAsset _baseFontDB;

		private static bool _fontAssetSetup;

		private static readonly string[] FilterDifficultyTypes =
		{
			null,
			"easy",
			"normal",
			"hard",
			"expert",
			"master",
			"append"
		};

		private readonly List<RowView> _rows = new List<RowView>();
		[SerializeField]
		private RectTransform _listContent;
		[SerializeField]
		private ScrollRect _manifestFormScroll;
		[SerializeField]
		private RectTransform _manifestFieldGrid;
		[SerializeField]
		private GridLayoutGroup _manifestFieldLayout;
		[SerializeField]
		private TextMeshProUGUI _emptyText;
		[SerializeField]
		private TextMeshProUGUI _statusText;
		[SerializeField]
		private TextMeshProUGUI _listCountText;
		[SerializeField]
		private Button[] _difficultyFilterButtons = Array.Empty<Button>();
		[SerializeField]
		private Image[] _difficultyFilterFills = Array.Empty<Image>();
		[SerializeField]
		private TextMeshProUGUI[] _difficultyFilterLabels = Array.Empty<TextMeshProUGUI>();
		private int _difficultyFilterIndex;
		[SerializeField]
		private GameObject _createMenuOverlay;
		[SerializeField]
		private Button _openCreateMenuButton;
		[SerializeField]
		private Button _createMenuCloseButton;
		[SerializeField]
		private TextMeshProUGUI _windowTitleText;
		[SerializeField]
		private GameObject _detailEmptyState;
		[SerializeField]
		private GameObject _detailContent;
		[SerializeField]
		private GameObject _overviewPage;
		[SerializeField]
		private GameObject _formPage;
		[SerializeField]
		private Button _detailToggleButton;
		[SerializeField]
		private TextMeshProUGUI _detailToggleLabel;
		private bool _showingForm;
		private bool _uploading;
		[SerializeField]
		private CustomMusicScoreDifficultyBadge _detailBadge;
		[SerializeField]
		private TextMeshProUGUI _detailDescription;
		[SerializeField]
		private TextMeshProUGUI _detailTitle;
		[SerializeField]
		private TextMeshProUGUI _detailMeta;
		[SerializeField]
		private TextMeshProUGUI _detailStatus;
		[SerializeField]
		private TextMeshProUGUI _bestResultLeftText;
		[SerializeField]
		private TextMeshProUGUI _bestResultRightText;
		[SerializeField]
		private Image _jacketImage;
		[SerializeField]
		private Button _editButton;
		[SerializeField]
		private Button _playButton;
		[SerializeField]
		private Button _autoButton;
		[SerializeField]
		private Button _duplicateButton;
		[SerializeField]
		private Button _deleteButton;
		[SerializeField]
		private Button _exportButton;
		[SerializeField]
		private Button _exportSusButton;
		/// <summary>上传: sends the chart to the community site (sekai.ourtaiko.org) as the signed-in player.</summary>
		[SerializeField]
		private Button _uploadButton;
		[SerializeField]
		private Button _saveManifestButton;
		[SerializeField]
		private Button _audioSelectButton;
		[SerializeField]
		private Button _jacketSelectButton;
		[SerializeField]
		private Button _scoreSelectButton;
		[SerializeField]
		private Button _videoSelectButton;
		[SerializeField]
		private TMP_InputField _titleInput;
		[SerializeField]
		private TMP_InputField _scoreTitleInput;
		[SerializeField]
		private TMP_InputField _userInput;
		[SerializeField]
		private TMP_InputField _composerInput;
		[SerializeField]
		private TMP_InputField _lyricistInput;
		[SerializeField]
		private TMP_InputField _arrangerInput;
		[SerializeField]
		private TMP_InputField _singerInput;
		[SerializeField]
		private TMP_InputField _collaborationLabelInput;
		[SerializeField]
		private TMP_InputField _descriptionInput;
		[SerializeField]
		private RectTransform _difficultyFieldRect;
		[SerializeField]
		private TextMeshProUGUI _difficultyLabel;
		[SerializeField]
		private Button _difficultyButton;
		private int _difficultyIndex = 4;
		[SerializeField]
		private TMP_InputField _levelInput;
		[SerializeField]
		private TMP_InputField _durationInput;
		[SerializeField]
		private TMP_InputField _fillerInput;
		[SerializeField]
		private TMP_InputField _audioInput;
		[SerializeField]
		private TMP_InputField _jacketInput;
		[SerializeField]
		private TMP_InputField _scoreInput;
		[SerializeField]
		private TMP_InputField _videoInput;
		[SerializeField]
		private Button _homeButton;
		[SerializeField]
		private Button _settingsButton;
		[SerializeField]
		private Button _refreshButton;
		[SerializeField]
		private Button _newButton;
		[SerializeField]
		private Button _importButton;
		[SerializeField]
		private CustomMusicScoreEntryCell _entryCellTemplate;
		private IReadOnlyList<CustomMusicScoreManagerItem> _allItems = Array.Empty<CustomMusicScoreManagerItem>();
		private IReadOnlyList<CustomMusicScoreManagerItem> _items = Array.Empty<CustomMusicScoreManagerItem>();
		private CustomMusicScoreManagerItem _selected;
		private Sprite _jacketSprite;

		// The view is authored in the prefab (Prefabs/CustomMusicScoreManager); this only binds it, as OurTaikoPlay's
		// scenes do with their [SerializeField] references.
		protected override void Awake()
		{
			base.Awake();
			SetupOriginalFontAssets();
			BindEvents();
			UpdateSelection(null);
		}

		private void BindEvents()
		{
			if (_homeButton != null)
			{
				_homeButton.onClick.AddListener(() => SceneManager.Instance.RequestScene(SceneManager.Scene.Home));
			}
			_settingsButton.onClick.AddListener(OpenSettings);
			_refreshButton.onClick.AddListener(RefreshList);
			_newButton.onClick.AddListener(() =>
			{
				SetCreateMenuVisible(false);
				CreateEntry();
			});
			_importButton.onClick.AddListener(() =>
			{
				SetCreateMenuVisible(false);
				ImportEntry();
			});
			if (_openCreateMenuButton != null)
			{
				_openCreateMenuButton.onClick.AddListener(() => SetCreateMenuVisible(true));
			}
			if (_createMenuCloseButton != null)
			{
				_createMenuCloseButton.onClick.AddListener(() => SetCreateMenuVisible(false));
			}
			SetCreateMenuVisible(false);

			_editButton.onClick.AddListener(OpenEditor);
			_playButton.onClick.AddListener(PlaySelected);
			_autoButton.onClick.AddListener(AutoPlaySelected);
			_duplicateButton.onClick.AddListener(DuplicateSelected);
			_exportButton.onClick.AddListener(ExportSelected);
			_exportSusButton.onClick.AddListener(ExportSelectedSus);
			if (_uploadButton != null)
			{
				_uploadButton.onClick.AddListener(UploadSelected);
			}
			_deleteButton.onClick.AddListener(DeleteSelected);

			_audioSelectButton.onClick.AddListener(ReplaceSelectedAudio);
			_jacketSelectButton.onClick.AddListener(ReplaceSelectedJacket);
			_scoreSelectButton.onClick.AddListener(ReplaceSelectedScore);
			_videoSelectButton.onClick.AddListener(ReplaceSelectedVideo);
			_difficultyButton.onClick.AddListener(CycleDifficulty);
			_saveManifestButton.onClick.AddListener(SaveSelectedManifest);


			if (_detailToggleButton != null)
			{
				_detailToggleButton.onClick.AddListener(() => SetDetailPage(!_showingForm));
			}
			for (int i = 0; i < _difficultyFilterButtons.Length; i++)
			{
				int filterIndex = i;
				if (_difficultyFilterButtons[i] != null)
				{
					_difficultyFilterButtons[i].onClick.AddListener(() => SetDifficultyFilter(filterIndex));
				}
			}
			UpdateDifficultyFilterVisuals();
			SetDetailPage(false);
		}

		private void SetCreateMenuVisible(bool visible)
		{
			if (_createMenuOverlay == null)
			{
				return;
			}

			_createMenuOverlay.SetActive(visible);
			if (visible)
			{
				_createMenuOverlay.transform.SetAsLastSibling();
			}
		}

		private void SetDetailPage(bool showForm)
		{
			_showingForm = showForm;
			if (_overviewPage != null)
			{
				_overviewPage.SetActive(!showForm);
			}
			if (_formPage != null)
			{
				_formPage.SetActive(showForm);
			}
			if (_detailToggleLabel != null)
			{
				_detailToggleLabel.text = showForm ? "概要" : "编辑信息";
			}
		}

		private void SetDifficultyFilter(int filterIndex)
		{
			_difficultyFilterIndex = Mathf.Clamp(filterIndex, 0, FilterDifficultyTypes.Length - 1);
			UpdateDifficultyFilterVisuals();
			RebuildRows();
		}

		private void UpdateDifficultyFilterVisuals()
		{
			Color32 teal = new Color32(103, 221, 205, 255);
			for (int i = 0; i < _difficultyFilterButtons.Length; i++)
			{
				bool selected = i == _difficultyFilterIndex;
				Color32 color = i == 0 ? teal : CustomMusicScoreDifficultyBadge.GetDifficultyColor(FilterDifficultyTypes[i]);
				if (i < _difficultyFilterFills.Length && _difficultyFilterFills[i] != null)
				{
					_difficultyFilterFills[i].color = selected ? color : new Color32(64, 58, 104, 170);
				}
				if (i < _difficultyFilterLabels.Length && _difficultyFilterLabels[i] != null)
				{
					_difficultyFilterLabels[i].color = Color.white;
				}
			}
		}

		private static bool MatchesDifficultyFilter(CustomMusicScoreManagerItem item, string difficultyType)
		{
			if (string.IsNullOrEmpty(difficultyType))
			{
				return true;
			}
			return string.Equals(NormalizeDifficulty(item.Entry.Manifest.musicDifficultyType), difficultyType, StringComparison.OrdinalIgnoreCase);
		}

		protected override void OnBoot(BootArgBase bootData)
		{
			RefreshList();
		}

		protected override void OnScreenStart()
		{
			RefreshList();
		}

		private void LateUpdate()
		{
			UpdateManifestFieldLayout();
		}

		protected override void OnExited()
		{
			ClearLoadedJacket();
			ClearRows();
			base.OnExited();
		}

		private void OpenSettings()
		{
			// The official option dialog (UI/Option/OptionDialog): ライブ / 音量 / システム / Additionals.
			ScreenManager.Instance?.ShowDialog<OptionDialog>(DialogType.OptionDialog);
		}

		private void UpdateManifestFieldLayout()
		{
			if (_manifestFieldGrid == null || _manifestFieldLayout == null)
			{
				return;
			}

			float availableWidth = GetManifestFieldAvailableWidth();
			if (availableWidth <= 0f)
			{
				return;
			}

			int columnCount = CalculateManifestColumnCount(availableWidth);
			float spacingWidth = _manifestFieldLayout.spacing.x * (columnCount - 1);
			float cellWidth = Mathf.Floor((availableWidth - spacingWidth) / columnCount);
			cellWidth = Mathf.Max(1f, cellWidth);
			Vector2 cellSize = _manifestFieldLayout.cellSize;
			bool gridWidthChanged = Mathf.Abs(_manifestFieldGrid.rect.width - availableWidth) > 0.5f;
			if (_manifestFieldLayout.constraintCount == columnCount && Mathf.Abs(cellSize.x - cellWidth) <= 0.5f && !gridWidthChanged)
			{
				return;
			}

			_manifestFieldGrid.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, availableWidth);
			_manifestFieldLayout.constraintCount = columnCount;
			_manifestFieldLayout.cellSize = new Vector2(cellWidth, cellSize.y);
			LayoutRebuilder.MarkLayoutForRebuild(_manifestFieldGrid);
			if (_manifestFieldGrid.parent is RectTransform parent)
			{
				LayoutRebuilder.MarkLayoutForRebuild(parent);
			}
		}

		private float GetManifestFieldAvailableWidth()
		{
			if (_manifestFormScroll != null && _manifestFormScroll.viewport != null)
			{
				return _manifestFormScroll.viewport.rect.width - ManifestFieldLeftPadding - ManifestFieldRightScrollPadding;
			}

			return _manifestFieldGrid.rect.width;
		}

		private static int CalculateManifestColumnCount(float availableWidth)
		{
			for (int columns = MaxManifestFieldColumnCount; columns > 1; columns--)
			{
				float requiredWidth = ManifestFieldMinWidth * columns + ManifestFieldSpacingX * (columns - 1);
				if (availableWidth >= requiredWidth)
				{
					return columns;
				}
			}

			return 1;
		}

		private void RefreshList()
		{
			_allItems = CustomMusicScoreManagerService.LoadItems();
			RebuildRows();
			SetStatus("已加载 " + _allItems.Count.ToString(CultureInfo.InvariantCulture) + " 个谱面。");
		}

		private void RebuildRows()
		{
			ClearRows();

			string difficultyFilter = FilterDifficultyTypes[Mathf.Clamp(_difficultyFilterIndex, 0, FilterDifficultyTypes.Length - 1)];
			List<CustomMusicScoreManagerItem> visibleItems = new List<CustomMusicScoreManagerItem>();
			foreach (CustomMusicScoreManagerItem item in _allItems)
			{
				if (MatchesDifficultyFilter(item, difficultyFilter))
				{
					visibleItems.Add(item);
				}
			}
			_items = visibleItems;

			foreach (CustomMusicScoreManagerItem item in _items)
			{
				RowView row = CreateRow(_listContent, item);
				_rows.Add(row);
			}

			_emptyText.gameObject.SetActive(_items.Count == 0);
			_emptyText.text = _allItems.Count == 0
				? "还没有本地谱面。\n点击右上角的「新建」或「导入」开始制作。"
				: "没有符合条件的谱面。";
			if (_listCountText != null)
			{
				_listCountText.text = _items.Count.ToString(CultureInfo.InvariantCulture) + " / " + _allItems.Count.ToString(CultureInfo.InvariantCulture);
			}

			CustomMusicScoreManagerItem selected = null;
			if (_selected != null)
			{
				string selectedPath = _selected.Entry.RootDirectory;
				for (int i = 0; i < _items.Count; i++)
				{
					if (string.Equals(_items[i].Entry.RootDirectory, selectedPath, StringComparison.OrdinalIgnoreCase))
					{
						selected = _items[i];
						break;
					}
				}
			}
			UpdateSelection(selected ?? (_items.Count > 0 ? _items[0] : null));
		}

		private void ClearRows()
		{
			foreach (RowView row in _rows)
			{
				row.ReleaseJacket();
				if (row.Root != null)
				{
					Destroy(row.Root);
				}
			}
			_rows.Clear();
		}

		private RowView CreateRow(RectTransform parent, CustomMusicScoreManagerItem item)
		{
			CustomMusicScoreEntryCell cell = Instantiate(_entryCellTemplate, parent, false);
			cell.name = "EntryCell";
			cell.gameObject.SetActive(true);
			cell.Button.onClick.AddListener(() => UpdateSelection(item));

			CustomMusicScoreManifest manifest = item.Entry.Manifest;
			string metaText = string.IsNullOrEmpty(manifest.title) ? "-" : manifest.title;
			if (!string.IsNullOrEmpty(manifest.userName))
			{
				metaText += "  /  " + manifest.userName;
			}

			cell.TitleText.text = string.IsNullOrWhiteSpace(manifest.scoreTitle) ? manifest.title : manifest.scoreTitle;
			cell.MetaText.text = metaText;
			cell.StatusText.text = item.StatusText;
			cell.StatusText.color = GetStatusColor(item);
			if (cell.DifficultyBadge != null)
			{
				cell.DifficultyBadge.Set(NormalizeDifficulty(manifest.musicDifficultyType), manifest.playLevel);
			}

			Sprite jacket = null;
			if (cell.JacketImage != null)
			{
				jacket = LoadSpriteFromFile(item.Entry.JacketPath);
				cell.JacketImage.sprite = jacket;
				cell.JacketImage.color = jacket != null ? Color.white : Color.clear;
			}
			return new RowView(cell.gameObject, cell, item, jacket);
		}

		/// <summary>Buttons with a CanvasGroup fade as a whole (outline and label too) while not interactable.</summary>
		private void SyncDisabledButtonVisuals()
		{
			Button[] buttons =
			{
				_editButton, _playButton, _autoButton, _duplicateButton, _deleteButton, _exportButton, _exportSusButton, _uploadButton, _saveManifestButton,
				_audioSelectButton, _jacketSelectButton, _scoreSelectButton, _videoSelectButton, _difficultyButton, _detailToggleButton
			};
			foreach (Button button in buttons)
			{
				if (button != null && button.TryGetComponent(out CanvasGroup group))
				{
					group.alpha = button.interactable ? 1f : 0.4f;
				}
			}
		}

		private static Color32 GetStatusColor(CustomMusicScoreManagerItem item)
		{
			return item.HasAudio && item.HasScore ? new Color32(103, 221, 205, 255) : new Color32(255, 184, 100, 255);
		}

		private void UpdateSelection(CustomMusicScoreManagerItem item)
		{
			_selected = item;
			for (int i = 0; i < _rows.Count; i++)
			{
				RowView row = _rows[i];
				bool selected = row.Item == item;
				bool nextSelected = i + 1 < _rows.Count && _rows[i + 1].Item == item;
				row.Cell.Background.color = selected ? new Color32(255, 255, 255, 96) : new Color32(255, 255, 255, 0);
				if (row.Cell.SelectedFrame != null)
				{
					row.Cell.SelectedFrame.SetActive(selected);
				}
				if (row.Cell.Separator != null)
				{
					row.Cell.Separator.SetActive(!selected && !nextSelected && i < _rows.Count - 1);
				}
			}

			bool hasSelection = item != null;
			_editButton.interactable = hasSelection && item.IsReadyForEdit;
			_playButton.interactable = hasSelection && item.HasScore && item.HasAudio;
			_autoButton.interactable = hasSelection && item.HasScore && item.HasAudio;
			_duplicateButton.interactable = hasSelection;
			_deleteButton.interactable = hasSelection;
			_exportButton.interactable = hasSelection;
			_exportSusButton.interactable = hasSelection && item.HasScore;
			if (_uploadButton != null)
			{
				_uploadButton.interactable = CanUpload(item);
			}
			_saveManifestButton.interactable = hasSelection;
			_audioSelectButton.interactable = hasSelection;
			_jacketSelectButton.interactable = hasSelection;
			_scoreSelectButton.interactable = hasSelection;
			_videoSelectButton.interactable = hasSelection;
			if (_detailToggleButton != null)
			{
				_detailToggleButton.interactable = hasSelection;
			}
			SetFormInteractable(hasSelection);
			SyncDisabledButtonVisuals();
			if (_detailEmptyState != null)
			{
				_detailEmptyState.SetActive(!hasSelection);
			}
			if (_detailContent != null)
			{
				_detailContent.SetActive(hasSelection);
			}

			if (!hasSelection)
			{
				if (_windowTitleText != null)
				{
					_windowTitleText.text = "NOT FOUND";
				}
				_detailTitle.text = string.Empty;
				_detailMeta.text = string.Empty;
				_detailStatus.text = string.Empty;
				if (_detailDescription != null)
				{
					_detailDescription.text = string.Empty;
				}
				SetBestResultText(null);
				ClearLoadedJacket();
				ClearForm();
				return;
			}

			CustomMusicScoreManifest manifest = item.Entry.Manifest;
			if (_windowTitleText != null)
			{
				string artist = !string.IsNullOrWhiteSpace(manifest.singer) ? manifest.singer : manifest.composer;
				_windowTitleText.text = string.IsNullOrWhiteSpace(artist) ? manifest.title : manifest.title + " - " + artist;
			}
			_detailTitle.text = string.IsNullOrWhiteSpace(manifest.scoreTitle) ? manifest.title : manifest.scoreTitle;
			_detailMeta.text = string.Format(
				CultureInfo.InvariantCulture,
				"<color=#9C98C8>谱师</color>  {0}\n<color=#9C98C8>更新</color>  {1:yyyy-MM-dd HH:mm}",
				string.IsNullOrWhiteSpace(manifest.userName) ? "-" : manifest.userName,
				item.LastWriteTime);
			_detailStatus.text = item.StatusText;
			_detailStatus.color = GetStatusColor(item);
			if (_detailBadge != null)
			{
				_detailBadge.Set(NormalizeDifficulty(manifest.musicDifficultyType), manifest.playLevel);
			}
			if (_detailDescription != null)
			{
				_detailDescription.text = string.IsNullOrWhiteSpace(manifest.description) ? "<color=#8F8BB8>（没有简介）</color>" : manifest.description;
			}
			UpdateBestResult(manifest);
			LoadJacket(item.Entry.JacketPath);
			LoadForm(manifest);
		}

		private void UpdateBestResult(CustomMusicScoreManifest manifest)
		{
			if (manifest != null && CustomMusicScorePlayHistoryStorage.TryLoadBestResult(manifest.id, out CustomMusicScorePlayHistoryRecord record))
			{
				SetBestResultText(record);
				return;
			}

			SetBestResultText(null);
		}

		private void SetBestResultText(CustomMusicScorePlayHistoryRecord record)
		{
			if (_bestResultLeftText == null || _bestResultRightText == null)
			{
				return;
			}

			if (record == null)
			{
				_bestResultLeftText.text = _selected == null ? string.Empty : "暂无游玩记录";
				_bestResultRightText.text = string.Empty;
				return;
			}

			_bestResultLeftText.text = string.Format(
				CultureInfo.InvariantCulture,
				"COMBO: {0}/{1}\nPERFECT: {2}\nGREAT: {3}",
				record.maxCombo,
				record.totalComboCount,
				record.perfectCount,
				record.greatCount);
			_bestResultRightText.text = string.Format(
				CultureInfo.InvariantCulture,
				"GOOD: {0}\nBAD: {1}\nMISS: {2}",
				record.goodCount,
				record.badCount,
				record.missCount);
		}

		private void CreateEntry()
		{
			CustomMusicScoreEntry entry = CustomMusicScoreManagerService.CreateNewEntry();
			_selected = entry == null ? null : new CustomMusicScoreManagerItem(
				entry,
				DateTime.Now,
				true,
				File.Exists(entry.ScorePath),
				File.Exists(entry.AudioPath),
				File.Exists(entry.JacketPath));
			RefreshList();
			SetStatus("已创建谱面。");
		}

		private void OpenEditor()
		{
			if (_selected?.Entry == null)
			{
				return;
			}

			CustomMusicScoreEntry entry = CustomMusicScoreStorage.LoadEntry(_selected.Entry.RootDirectory);
			if (entry == null)
			{
				SetStatus("无法加载谱面。");
				RefreshList();
				return;
			}

			MusicScoreMakerSceneArgs.Prepare(MusicScoreMakerSceneArgs.CreateBootArg(entry));
			SceneManager.Instance.RequestScene(SceneManager.Scene.MusicScoreMaker);
		}

		private void PlaySelected()
		{
			PlaySelectedAsync(false).Forget();
		}

		private void AutoPlaySelected()
		{
			PlaySelectedAsync(true).Forget();
		}

		private async UniTask PlaySelectedAsync(bool isAuto)
		{
			if (_selected?.Entry == null)
			{
				return;
			}

			CustomMusicScoreEntry entry = SaveSelectedManifestFromForm(refreshList: false);
			entry ??= CustomMusicScoreStorage.LoadEntry(_selected.Entry.RootDirectory);
			if (entry == null)
			{
				SetStatus("无法加载谱面。");
				RefreshList();
				return;
			}

			string error = await CustomMusicScorePlayLauncher.PlayAsync(
				entry, isAuto, MenuScreenType.MusicScoreMakerTop, SetStatus, this.GetCancellationTokenOnDestroy());
			if (error != null)
			{
				SetStatus(error);
				return;
			}
			SetStatus(isAuto ? "正在开始自动游玩..." : "正在开始游玩...");
		}

		/// <summary>Builds the boot data for playing a custom chart from the manager (also used by direct Core runs).</summary>
		public static FreeLiveBootData CreateDirectPlayBootData(
			CustomMusicScoreEntry entry,
			MusicScoreMakerData scoreData,
			bool isAuto,
			MenuScreenType returnScreenType = MenuScreenType.MusicScoreMakerTop)
		{
			if (entry == null || scoreData == null)
			{
				return null;
			}

			LiveBundleBuildData liveBundleBuildData = Resources.Load<LiveBundleBuildData>(LiveConfig.ConfigBundleNamePath);
			MusicScore musicScore = scoreData.ToMusicScore(liveBundleBuildData);
			int deckId = UserDataManager.Instance.SelectedDeckId;
			MasterMusicDifficulty difficulty = CreateDirectPlayDifficulty(entry, musicScore);
			string difficultyString = difficulty?.musicDifficulty ?? "master";
			LiveSettingData liveSettingData = LiveSettingData.LoadFromStorage();
			MusicCategory musicCategory = ResolveDirectPlayMusicCategory(liveSettingData);

			// Matches the original manager play route: normal FreeLive, not editor test play.
			FreeLiveBootData bootData = new FreeLiveBootData(
				entry.MusicId,
				difficultyString,
				0,
				deckId,
				LivePlayMode.Free,
				LiveMusicData.CollaborationModeState.Off,
				musicCategory);

			bootData.LiveEventData = new LiveEventData(Array.Empty<IngameLotterySkill>(), Array.Empty<IngameComboCutin>(), deckId, isAuto);
			bootData.LiveSettingData = liveSettingData;
			bootData.MVQualityType = bootData.LiveSettingData?.QualityType ?? Sekai.MVQualityType.Default;
			bootData.MusicCategory = musicCategory;
			bootData.IsAuto = isAuto;
			bootData.IsCustomMusicScore = true;
			bootData.IsOfficialMusicScore = false;
			bootData.ReturnScreenType = returnScreenType;
			bootData.canSkipDisplayMusicInfo = false;
			bootData.ReleaseTransitionBeforeMusicStart = true;
			bootData.CustomMusicScoreId = entry.Manifest.id;
			bootData.CustomMusicScorePath = entry.RootDirectory;
			bootData.CustomMusicScoreTitle = entry.Manifest.scoreTitle;
			bootData.CustomMusicScoreAuthorName = entry.Manifest.userName;
			bootData.CustomMusicScoreCollaborationLabel = entry.Manifest.collaborationLabel;

			if (bootData.MusicData != null)
			{
				bootData.MusicData.Music = CreateDirectPlayMusic(entry);
				bootData.MusicData.Difficulty = difficulty;
				bootData.MusicData.Vocal = CreateDirectPlayVocal(entry);
				bootData.MusicData.Score = new MasterPlayLevelScore
				{
					liveType = LiveType.solo.ToString(),
					playLevel = entry.Manifest.playLevel
				};
				bootData.MusicData.IsTestPlay = false;
				bootData.MusicData.IsUseCustomScore = true;
				bootData.MusicData.CustomPlayLevel = entry.Manifest.playLevel;
				bootData.MusicData.MusicScore = musicScore;
				bootData.MusicData.StartMusicTimeMs = 0L;
				bootData.MusicData.PlayStartEffectEnabled = true;
			}

			return bootData;
		}

		private static MusicCategory ResolveDirectPlayMusicCategory(LiveSettingData liveSettingData)
		{
			return liveSettingData?.UsesCustomMusicScore2DMVBackground == true
				? MusicCategory.mv_2d
				: MusicCategory.image;
		}

		private static MasterMusicDifficulty CreateDirectPlayDifficulty(CustomMusicScoreEntry entry, MusicScore musicScore)
		{
			return new MasterMusicDifficulty
			{
				id = MasterMusicDifficulty.INVALID_ID,
				musicId = entry.MusicId,
				musicDifficulty = NormalizeDifficulty(entry.Manifest.musicDifficultyType),
				playLevel = entry.Manifest.playLevel,
				totalNoteCount = LiveUtility.CalculateTotalComboCount(musicScore)
			};
		}

		private static MasterMusic CreateDirectPlayMusic(CustomMusicScoreEntry entry)
		{
			return new MasterMusic
			{
				id = entry.MusicId,
				title = entry.Manifest.title,
				lyricist = entry.Manifest.lyricist,
				composer = entry.Manifest.composer,
				arranger = entry.Manifest.arranger,
				assetbundleName = entry.AudioCueName,
				fillerSec = entry.Manifest.fillerSec,
				secForMusicScoreMaker = entry.MusicDurationSec,
				isAvailableForMusicScoreMaker = true
			};
		}

		private static MasterMusicVocal CreateDirectPlayVocal(CustomMusicScoreEntry entry)
		{
			return new MasterMusicVocal
			{
				id = 0,
				musicId = entry.MusicId,
				musicVocalType = MusicVocalType.original_song.ToString(),
				caption = entry.Manifest.singer,
				assetbundleName = entry.AudioCueName
			};
		}

		private static string NormalizeDifficulty(string difficulty)
		{
			return string.IsNullOrWhiteSpace(difficulty) || string.Equals(difficulty, "none", StringComparison.OrdinalIgnoreCase)
				? "master"
				: difficulty.ToLowerInvariant();
		}

		private void DuplicateSelected()
		{
			if (_selected == null)
			{
				return;
			}

			CustomMusicScoreEntry entry = CustomMusicScoreManagerService.DuplicateEntry(_selected.Entry);
			_selected = entry == null ? null : new CustomMusicScoreManagerItem(entry, DateTime.Now, true, File.Exists(entry.ScorePath), File.Exists(entry.AudioPath), File.Exists(entry.JacketPath));
			RefreshList();
			SetStatus("已复制谱面。");
		}

		private void DeleteSelected()
		{
			if (_selected?.Entry == null)
			{
				return;
			}

			CustomMusicScoreEntry entry = _selected.Entry;
			string title = string.IsNullOrWhiteSpace(entry.Manifest.scoreTitle) ? entry.Manifest.title : entry.Manifest.scoreTitle;
			Common2ButtonDialog dialog = ScreenManager.Instance?.Show2ButtonDialog<Common2ButtonDialog>(
				DialogType.Common2ButtonDialog,
				null,
				"WORD_DELETE",
				"WORD_CANCEL",
				() => ConfirmDeleteSelected(entry, title),
				null,
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				true);
			if (dialog != null)
			{
				dialog.SetMessageBodyText("确定要删除谱面吗？\n\n" + title + "\n\n这个操作无法撤销。");
				return;
			}

			ConfirmDeleteSelected(entry, title);
		}

		private void ConfirmDeleteSelected(CustomMusicScoreEntry entry, string title)
		{
			if (entry == null)
			{
				return;
			}

			string deletedRoot = entry.RootDirectory;
			CustomMusicScoreManagerService.DeleteEntry(entry);
			if (_selected != null && string.Equals(_selected.Entry.RootDirectory, deletedRoot, StringComparison.OrdinalIgnoreCase))
			{
				_selected = null;
			}
			RefreshList();
			SetStatus("已删除 " + title + "。");
		}

		private void ExportSelected()
		{
			if (_selected == null)
			{
				return;
			}

			CustomMusicScoreEntry entry = _selected.Entry;
			SetStatus("请选择导出位置...");
			PlatformFileDialog.Export(
				"导出自制谱",
				CustomMusicScoreStorage.RootDirectory,
				entry.Manifest.scoreTitle + "_" + entry.Manifest.id,
				"zip",
				destination => CustomMusicScoreManagerService.ExportZip(entry, destination),
				(result, path) => ShowExportResult(result, path, string.Empty));
		}

		private void ExportSelectedSus()
		{
			if (_selected == null)
			{
				return;
			}

			CustomMusicScoreEntry entry = _selected.Entry;
			List<string> warnings = null;
			SetStatus("请选择导出位置...");
			PlatformFileDialog.Export(
				"导出SUS谱面",
				CustomMusicScoreStorage.RootDirectory,
				entry.Manifest.scoreTitle + "_" + entry.Manifest.id,
				"sus",
				destination => CustomMusicScoreManagerService.ExportSus(entry, out warnings, destination),
				(result, path) => ShowExportResult(result, path, FormatSusWarnings(warnings), "导出失败：没有谱面。"));
		}

		private bool CanUpload(CustomMusicScoreManagerItem item)
		{
			return item != null && item.HasScore && item.HasAudio && !_uploading;
		}

		private void SetUploading(bool uploading)
		{
			_uploading = uploading;
			if (this != null && _uploadButton != null)
			{
				_uploadButton.interactable = CanUpload(_selected);
				SyncDisabledButtonVisuals();
			}
		}

		private void UploadSelected()
		{
			if (_selected?.Entry == null || _uploading)
			{
				return;
			}
			if (!CommunityAccount.IsSignedIn)
			{
				SetStatus("上传前请先登录 OurTaiko 账号：右上角菜单 → Additionals → アカウント。");
				return;
			}
			PrepareUploadAsync(_selected.Entry).Forget();
		}

		/// <summary>
		/// Works out whether the upload adds a chart or replaces one: a local chart linked to the player's own site
		/// chart (uploaded from here before, or downloaded) becomes its new version. Then asks before sending.
		/// </summary>
		private async UniTaskVoid PrepareUploadAsync(CustomMusicScoreEntry entry)
		{
			string title = string.IsNullOrWhiteSpace(entry.Manifest.scoreTitle) ? entry.Manifest.title : entry.Manifest.scoreTitle;
			CancellationToken token = this.GetCancellationTokenOnDestroy();
			CommunityChart linked = null;
			SetUploading(true);
			try
			{
				string linkedId = CommunityLibrary.FindChartId(entry.RootDirectory);
				if (linkedId != null)
				{
					SetStatus("正在确认社区上的谱面…");
					try
					{
						linked = await CommunityClient.GetAsync(linkedId, token);
					}
					catch (CommunityException exception) when (exception.Status == 404)
					{
						CommunityLibrary.Forget(linkedId);
					}
				}
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (CommunityException exception)
			{
				SetStatus("上传失败：" + exception.Message);
				return;
			}
			finally
			{
				SetUploading(false);
			}

			SetStatus(string.Empty);
			// The dialog has room for about three lines.
			bool own = linked != null && string.Equals(linked.Uploader?.Id, CommunityAccount.UserId, StringComparison.Ordinal);
			string replaceId = own ? linked.Id : null;
			string message;
			if (own)
			{
				message = "用当前版本更新 Sekai Fanmade 上的「" + title + "」（#" + linked.NumberId + "）？\n简介、标签、得分和评论会保留。";
			}
			else if (linked != null)
			{
				message = "「" + title + "」是从 Sekai Fanmade 下载的（谱师 " + linked.Uploader?.Nickname + "），会作为新谱面上传。\n改编别人的谱面前，请确认对方允许二次利用。";
			}
			else
			{
				message = "把「" + title + "」上传到 Sekai Fanmade？\n上传后所有人都能下载，请只上传你有权分享的内容。";
			}

			Common2ButtonDialog dialog = ScreenManager.Instance?.Show2ButtonDialog<Common2ButtonDialog>(
				DialogType.Common2ButtonDialog,
				null,
				"WORD_DECIDE",
				"WORD_CANCEL",
				() => UploadAsync(entry, replaceId).Forget(),
				() => SetStatus("已取消上传。"),
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				true);
			if (dialog != null)
			{
				dialog.SetMessageBodyText(message);
				return;
			}
			UploadAsync(entry, replaceId).Forget();
		}

		private async UniTaskVoid UploadAsync(CustomMusicScoreEntry entry, string replaceId)
		{
			if (_uploading)
			{
				return;
			}
			CancellationToken token = this.GetCancellationTokenOnDestroy();
			string folder = Path.Combine(GamePaths.Root, "CommunityUploads");
			string zipPath = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".zip");
			SetUploading(true);
			try
			{
				SetStatus("正在打包谱面…");
				Directory.CreateDirectory(folder);
				// The server keeps only manifest.json, the score, audio, jacket and MV, so the folder goes as it is.
				await UniTask.RunOnThreadPool(() => CustomMusicScoreManagerService.ExportZip(entry, zipPath), cancellationToken: token);

				int shown = -1;
				IProgress<float> progress = Progress.Create<float>(value =>
				{
					int percent = Mathf.Clamp(Mathf.FloorToInt(value * 100f), 0, 100);
					if (percent != shown)
					{
						shown = percent;
						SetStatus(percent >= 100 ? "服务器正在检查谱面包…" : "正在上传 " + percent + "%…");
					}
				});
				string id = await CommunityClient.UploadChartAsync(zipPath, replaceId, progress, token);

				CommunityChart chart = null;
				try
				{
					chart = await CommunityClient.GetAsync(id, token);
				}
				catch (CommunityException)
				{
				}
				CommunityLibrary.RememberUpload(id, chart?.Revision ?? 1, entry.RootDirectory);
				string number = chart != null ? "（#" + chart.NumberId + "）" : string.Empty;
				SetStatus(replaceId == null ? "已上传到 Sekai Fanmade" + number + "。" : "已更新 Sekai Fanmade 上的谱面" + number + "。");
			}
			catch (OperationCanceledException)
			{
			}
			catch (CommunityException exception)
			{
				SetStatus("上传失败：" + exception.Message);
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				SetStatus("打包失败：" + exception.Message);
			}
			finally
			{
				try
				{
					if (File.Exists(zipPath))
					{
						File.Delete(zipPath);
					}
				}
				catch (IOException)
				{
				}
				SetUploading(false);
			}
		}

		private void ShowExportResult(PlatformFileDialog.ExportResult result, string path, string suffix, string writeFailedStatus = "导出失败。")
		{
			if (this == null)
			{
				return;
			}

			switch (result)
			{
				case PlatformFileDialog.ExportResult.Exported:
					SetStatus("已导出：" + path + suffix);
					break;
				case PlatformFileDialog.ExportResult.SavedInAppFolder:
					SetStatus("当前平台不支持文件导出，已导出到：" + path + suffix);
					break;
				case PlatformFileDialog.ExportResult.Busy:
					SetStatus("文件选择器已经打开。");
					break;
				case PlatformFileDialog.ExportResult.WriteFailed:
					SetStatus(writeFailedStatus);
					break;
				default:
					SetStatus(string.IsNullOrEmpty(path) ? "已取消导出。" : "导出已取消或失败。");
					break;
			}
		}

		private static string FormatSusWarnings(List<string> warnings)
		{
			return warnings == null || warnings.Count == 0 ? string.Empty : "\n" + string.Join("\n", warnings);
		}

		private void ImportEntry()
		{
			PickFile("导入自制谱ZIP", "导入已取消或失败。", "自制谱ZIP", new[] { "zip" }, path =>
			{
				if (!string.IsNullOrEmpty(path))
				{
					ApplyImportedEntry(CustomMusicScoreManagerService.ImportZip(path));
					return true;
				}

				// No ZIP picked: desktop dialogs offer a chart folder instead.
				if (!PlatformFileDialog.CanPickFolder)
				{
					return false;
				}

				string folder = PlatformFileDialog.PickFolder("导入自制谱文件夹");
				ApplyImportedEntry(string.IsNullOrEmpty(folder) ? null : CustomMusicScoreManagerService.ImportFolder(folder));
				return true;
			}, "当前平台暂不支持运行时导入，请手动复制谱面。");
		}

		private void ApplyImportedEntry(CustomMusicScoreEntry entry)
		{
			if (entry != null)
			{
				_selected = new CustomMusicScoreManagerItem(entry, DateTime.Now, true, File.Exists(entry.ScorePath), File.Exists(entry.AudioPath), File.Exists(entry.JacketPath));
				RefreshList();
				SetStatus("已导入谱面。");
			}
			else
			{
				SetStatus("导入已取消或失败。");
			}
		}

		private void ReplaceSelectedAudio()
		{
			ReplaceSelectedFromPicker("导入音频文件", "已取消导入音频。", "音频文件", new[] { "ogg", "mp3", "wav" },
				CustomMusicScoreManagerService.ReplaceAudioFile, "已导入音频", "当前平台暂不支持导入音频文件，请手动复制。");
		}

		private void ReplaceSelectedJacket()
		{
			ReplaceSelectedFromPicker("导入封面文件", "已取消导入封面。", "图片文件", new[] { "png", "jpg", "jpeg" },
				CustomMusicScoreManagerService.ReplaceJacketFile, "已导入封面", "当前平台暂不支持导入封面文件，请手动复制。");
		}

		private void ReplaceSelectedScore()
		{
			ReplaceSelectedFromPicker("导入谱面文件", "已取消导入谱面。", "谱面文件", new[] { "json", "txt", "sus" },
				CustomMusicScoreManagerService.ReplaceScoreFile, "已导入谱面", "当前平台暂不支持导入谱面文件，请手动复制。");
		}

		private void ReplaceSelectedVideo()
		{
			ReplaceSelectedFromPicker("导入2DMV视频", "已取消导入2DMV。", "MP4视频", new[] { "mp4" },
				CustomMusicScoreManagerService.ReplaceVideoFile, "已导入2DMV", "当前平台暂不支持导入2DMV视频，请手动复制。");
		}

		private void ReplaceSelectedFromPicker(
			string title,
			string cancelStatus,
			string filterName,
			string[] extensions,
			Func<CustomMusicScoreEntry, string, CustomMusicScoreEntry> replaceFile,
			string successStatus,
			string unsupportedStatus)
		{
			if (_selected?.Entry == null)
			{
				return;
			}

			PickFile(title, cancelStatus, filterName, extensions, path =>
			{
				if (string.IsNullOrEmpty(path) || _selected?.Entry == null)
				{
					return false;
				}

				ReplaceSelectedFile(path, replaceFile, successStatus);
				return true;
			}, unsupportedStatus);
		}

		/// <summary>
		/// Opens the platform file picker. <paramref name="onPicked"/> gets the path (empty when cancelled) and
		/// returns false to show <paramref name="cancelStatus"/>.
		/// </summary>
		private void PickFile(string title, string cancelStatus, string filterName, string[] extensions, Func<string, bool> onPicked, string unsupportedStatus)
		{
			SetStatus(title + "...");
			PlatformFileDialog.PickResult result = PlatformFileDialog.PickFile(title, filterName, extensions, path =>
			{
				if (this == null)
				{
					return;
				}

				if (!onPicked(path))
				{
					SetStatus(cancelStatus);
				}
			});

			if (result == PlatformFileDialog.PickResult.Busy)
			{
				SetStatus("文件选择器已经打开。");
			}
			else if (result == PlatformFileDialog.PickResult.Unsupported)
			{
				SetStatus(unsupportedStatus);
			}
		}

		private void ReplaceSelectedFile(
			string sourcePath,
			Func<CustomMusicScoreEntry, string, CustomMusicScoreEntry> replaceFile,
			string successStatus)
		{
			try
			{
				CustomMusicScoreEntry entry = replaceFile(_selected.Entry, sourcePath);
				if (entry == null)
				{
					SetStatus("导入失败。");
					return;
				}

				_selected = new CustomMusicScoreManagerItem(
					entry,
					DateTime.Now,
					File.Exists(entry.ManifestPath),
					File.Exists(entry.ScorePath),
					File.Exists(entry.AudioPath),
					File.Exists(entry.JacketPath));
				RefreshList();
				SetStatus(successStatus + ": " + Path.GetFileName(sourcePath));
			}
			catch (Exception ex)
			{
				SetStatus(ex.Message);
			}
		}

		private void SaveSelectedManifest()
		{
			CustomMusicScoreEntry savedEntry = SaveSelectedManifestFromForm(refreshList: true);
			SetStatus(savedEntry != null ? "配置已保存。" : "保存配置失败。");
		}

		private CustomMusicScoreEntry SaveSelectedManifestFromForm(bool refreshList)
		{
			if (_selected?.Entry == null)
			{
				return null;
			}

			CustomMusicScoreManifest manifest = _selected.Entry.Manifest;
			manifest.title = _titleInput.text;
			manifest.scoreTitle = _scoreTitleInput.text;
			manifest.userName = _userInput.text;
			manifest.composer = _composerInput.text;
			manifest.lyricist = _lyricistInput.text;
			manifest.arranger = _arrangerInput.text;
			manifest.singer = _singerInput.text;
			manifest.collaborationLabel = _collaborationLabelInput.text;
			manifest.description = _descriptionInput.text;
			manifest.musicDifficultyType = GetSelectedDifficultyType();
			manifest.audioFileName = Path.GetFileName(_audioInput.text);
			manifest.jacketFileName = Path.GetFileName(_jacketInput.text);
			manifest.scoreFileName = Path.GetFileName(_scoreInput.text);
			manifest.videoFileName = Path.GetFileName(_videoInput.text);
			if (int.TryParse(_levelInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
			{
				manifest.playLevel = level;
			}
			if (int.TryParse(_durationInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int duration))
			{
				manifest.secForMusicScoreMaker = duration;
			}
			if (float.TryParse(_fillerInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float filler))
			{
				manifest.fillerSec = filler;
			}

			CustomMusicScoreEntry savedEntry = CustomMusicScoreManagerService.SaveManifest(_selected.Entry, manifest);
			if (savedEntry != null)
			{
				_selected = new CustomMusicScoreManagerItem(
					savedEntry,
					DateTime.Now,
					File.Exists(savedEntry.ManifestPath),
					File.Exists(savedEntry.ScorePath),
					File.Exists(savedEntry.AudioPath),
					File.Exists(savedEntry.JacketPath));
			}
			if (refreshList)
			{
				RefreshList();
			}
			return savedEntry;
		}

		private void LoadForm(CustomMusicScoreManifest manifest)
		{
			_titleInput.SetTextWithoutNotify(manifest.title);
			_scoreTitleInput.SetTextWithoutNotify(manifest.scoreTitle);
			_userInput.SetTextWithoutNotify(manifest.userName);
			_composerInput.SetTextWithoutNotify(manifest.composer);
			_lyricistInput.SetTextWithoutNotify(manifest.lyricist);
			_arrangerInput.SetTextWithoutNotify(manifest.arranger);
			_singerInput.SetTextWithoutNotify(manifest.singer);
			_collaborationLabelInput.SetTextWithoutNotify(manifest.collaborationLabel);
			_descriptionInput.SetTextWithoutNotify(manifest.description);
			SetDifficultyDropdownValue(manifest.musicDifficultyType);
			_levelInput.SetTextWithoutNotify(manifest.playLevel.ToString(CultureInfo.InvariantCulture));
			_durationInput.SetTextWithoutNotify(manifest.secForMusicScoreMaker.ToString(CultureInfo.InvariantCulture));
			_fillerInput.SetTextWithoutNotify(manifest.fillerSec.ToString(CultureInfo.InvariantCulture));
			_audioInput.SetTextWithoutNotify(manifest.audioFileName);
			_jacketInput.SetTextWithoutNotify(manifest.jacketFileName);
			_scoreInput.SetTextWithoutNotify(manifest.scoreFileName);
			_videoInput.SetTextWithoutNotify(manifest.videoFileName);
		}

		private void ClearForm()
		{
			TMP_InputField[] inputs =
			{
				_titleInput, _scoreTitleInput, _userInput, _composerInput, _lyricistInput, _arrangerInput, _singerInput, _collaborationLabelInput, _descriptionInput, _levelInput,
				_durationInput, _fillerInput, _audioInput, _jacketInput, _scoreInput, _videoInput
			};
			foreach (TMP_InputField input in inputs)
			{
				input.SetTextWithoutNotify(string.Empty);
			}
			SetDifficultyDropdownValue("master");
		}

		private void SetFormInteractable(bool interactable)
		{
			TMP_InputField[] inputs =
			{
				_titleInput, _scoreTitleInput, _userInput, _composerInput, _lyricistInput, _arrangerInput, _singerInput, _collaborationLabelInput, _descriptionInput, _levelInput,
				_durationInput, _fillerInput, _audioInput, _jacketInput, _scoreInput, _videoInput
			};
			foreach (TMP_InputField input in inputs)
			{
				input.interactable = interactable;
			}
			_difficultyButton.interactable = interactable;
		}

		private string GetSelectedDifficultyType()
		{
			int index = _difficultyIndex;
			if (index < 0 || index >= DifficultyTypes.Length)
			{
				return "master";
			}
			return DifficultyTypes[index];
		}

		private void SetDifficultyDropdownValue(string difficultyType)
		{
			int index = GetDifficultyIndex(difficultyType);
			_difficultyIndex = index;
			if (_difficultyLabel != null)
			{
				_difficultyLabel.text = DifficultyDisplayNames[index];
			}
		}

		private static int GetDifficultyIndex(string difficultyType)
		{
			for (int i = 0; i < DifficultyTypes.Length; i++)
			{
				if (string.Equals(DifficultyTypes[i], difficultyType, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}
			return 4;
		}

		private void LoadJacket(string path)
		{
			ClearLoadedJacket();
			_jacketSprite = LoadSpriteFromFile(path);
			_jacketImage.sprite = _jacketSprite;
			_jacketImage.color = _jacketSprite != null ? Color.white : Color.clear;
		}

		private static Sprite LoadSpriteFromFile(string path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return null;
			}

			byte[] bytes;
			try
			{
				bytes = File.ReadAllBytes(path);
			}
			catch (IOException)
			{
				return null;
			}

			Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (!texture.LoadImage(bytes))
			{
				Destroy(texture);
				return null;
			}

			return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
		}

		private static void DestroySpriteAndTexture(Sprite sprite)
		{
			if (sprite == null)
			{
				return;
			}

			Texture texture = sprite.texture;
			Destroy(sprite);
			if (texture != null)
			{
				Destroy(texture);
			}
		}

		private void ClearLoadedJacket()
		{
			if (_jacketSprite == null)
			{
				return;
			}

			DestroySpriteAndTexture(_jacketSprite);
			_jacketSprite = null;
			if (_jacketImage != null)
			{
				_jacketImage.sprite = null;
			}
		}

		private void SetStatus(string message)
		{
			if (_statusText != null)
			{
				_statusText.text = message ?? string.Empty;
			}
		}

		private void CycleDifficulty()
		{
			_difficultyIndex = (_difficultyIndex + 1) % DifficultyTypes.Length;
			SetDifficultyDropdownValue(DifficultyTypes[_difficultyIndex]);
		}

		private static void SetupOriginalFontAssets()
		{
			if (_fontAssetSetup)
			{
				return;
			}

			_baseFontEB = Resources.Load<TMP_FontAsset>(BaseFontEbPath);
			_baseFontDB = Resources.Load<TMP_FontAsset>(BaseFontDbPath);
			TMP_FontAsset dynamicFontEB = Resources.Load<TMP_FontAsset>(DynamicFontEbPath);
			TMP_FontAsset dynamicFontDB = Resources.Load<TMP_FontAsset>(DynamicFontDbPath);
			AddFallbackFontAsset(_baseFontEB, dynamicFontEB);
			AddFallbackFontAsset(_baseFontDB, dynamicFontDB);
			_fontAssetSetup = true;
		}

		private static void AddFallbackFontAsset(TMP_FontAsset fontAsset, TMP_FontAsset fallbackFontAsset)
		{
			if (fontAsset == null || fallbackFontAsset == null)
			{
				return;
			}

			if (fontAsset.fallbackFontAssetTable == null)
			{
				fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
			}

			if (!fontAsset.fallbackFontAssetTable.Contains(fallbackFontAsset))
			{
				fontAsset.fallbackFontAssetTable.Add(fallbackFontAsset);
			}
		}

		private sealed class RowView
		{
			private Sprite _jacket;

			public GameObject Root { get; }

			public CustomMusicScoreEntryCell Cell { get; }

			public CustomMusicScoreManagerItem Item { get; }

			public RowView(GameObject root, CustomMusicScoreEntryCell cell, CustomMusicScoreManagerItem item, Sprite jacket)
			{
				Root = root;
				Cell = cell;
				Item = item;
				_jacket = jacket;
			}

			public void ReleaseJacket()
			{
				DestroySpriteAndTexture(_jacket);
				_jacket = null;
			}
		}
	}
}
