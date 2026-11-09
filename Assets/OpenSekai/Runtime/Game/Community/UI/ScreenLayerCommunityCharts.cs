using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sekai.CustomMusicScoreManager;
using Sekai.MusicScoreMaker.Common;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Sekai.Community
{
	/// <summary>
	/// The online chart browser (CommunityCharts scene), shaped after the original CREATE CHART screen:
	/// おすすめ / ランキング (デイリー・総合) / 新着 / 検索 tabs and マイリスト, a chart list on the left and the
	/// chart window on the right with 親譜面 / 登録 / ダウンロード・プレイ. Charts come from the SekaiFanmade site.
	/// Authored in <c>Prefabs/Community/ScreenLayerCommunityCharts</c>; this only binds it.
	/// </summary>
	public sealed class ScreenLayerCommunityCharts : ScreenLayer
	{
		private enum Tab
		{
			Recommended,
			Ranking,
			New,
			Search,
			MyList,
		}

		private static readonly string[] FilterDifficultyTypes = { null, "easy", "normal", "hard", "expert", "master", "append" };

		/// <summary>優先表示カテゴリ: 人気 / 高難易度 / 新着 / ランダム.</summary>
		private static readonly string[] SearchSorts = { CommunitySort.Popular, CommunitySort.Hard, CommunitySort.New, CommunitySort.Random };

		private static readonly Color32 Teal = new Color32(103, 221, 205, 255);
		private static readonly Color32 IdleFill = new Color32(64, 58, 104, 170);
		private static readonly Color32 TabIdle = new Color32(170, 166, 210, 255);

		private const float PreviewSeconds = 20f;

		[Header("Header")]
		[SerializeField]
		private Button backButton;
		[SerializeField]
		private Button[] tabButtons = Array.Empty<Button>();
		[SerializeField]
		private TextMeshProUGUI[] tabLabels = Array.Empty<TextMeshProUGUI>();
		[SerializeField]
		private GameObject[] tabUnderlines = Array.Empty<GameObject>();
		[SerializeField]
		private Button myListButton;
		[SerializeField]
		private Image myListFace;
		[SerializeField]
		private Button accountButton;
		[SerializeField]
		private TextMeshProUGUI accountLabel;

		[Header("Filters")]
		[SerializeField]
		private GameObject difficultyRail;
		[SerializeField]
		private Button[] difficultyButtons = Array.Empty<Button>();
		[SerializeField]
		private Image[] difficultyFills = Array.Empty<Image>();
		[SerializeField]
		private GameObject periodToggle;
		[SerializeField]
		private Button dailyButton;
		[SerializeField]
		private Image dailyFill;
		[SerializeField]
		private Button totalButton;
		[SerializeField]
		private Image totalFill;
		[SerializeField]
		private Button searchBackButton;

		[Header("List")]
		[SerializeField]
		private GameObject listArea;
		[SerializeField]
		private ScrollRect listScroll;
		[SerializeField]
		private RectTransform listContent;
		[SerializeField]
		private CustomMusicScoreEntryCell cellTemplate;
		[SerializeField]
		private TextMeshProUGUI emptyText;
		[SerializeField]
		private TextMeshProUGUI listCountLabel;
		[SerializeField]
		private TextMeshProUGUI listCountText;
		[SerializeField]
		private TextMeshProUGUI statusText;

		[Header("Chart window")]
		[SerializeField]
		private GameObject detailWindow;
		[SerializeField]
		private TextMeshProUGUI windowTitleText;
		[SerializeField]
		private GameObject detailEmptyState;
		[SerializeField]
		private TextMeshProUGUI detailEmptyHint;
		[SerializeField]
		private GameObject detailContent;
		[SerializeField]
		private Image jacketImage;
		[SerializeField]
		private CustomMusicScoreDifficultyBadge detailBadge;
		[SerializeField]
		private TextMeshProUGUI detailTitle;
		[SerializeField]
		private TextMeshProUGUI detailStatus;
		[SerializeField]
		private TextMeshProUGUI detailMeta;
		[SerializeField]
		private TextMeshProUGUI infoLabel;
		[SerializeField]
		private TextMeshProUGUI infoLeft;
		[SerializeField]
		private TextMeshProUGUI infoRight;
		[SerializeField]
		private TextMeshProUGUI detailDescription;
		[SerializeField]
		private Button likeButton;
		[SerializeField]
		private TextMeshProUGUI likeLabel;
		[SerializeField]
		private Button previewButton;
		[SerializeField]
		private TextMeshProUGUI previewLabel;
		[SerializeField]
		private Button webPageButton;

		[Header("Actions")]
		[SerializeField]
		private GameObject actionButtons;
		[SerializeField]
		private Button parentButton;
		[SerializeField]
		private Button bookmarkButton;
		[SerializeField]
		private TextMeshProUGUI bookmarkLabel;
		[SerializeField]
		private Button playButton;
		[SerializeField]
		private TextMeshProUGUI playLabel;
		[SerializeField]
		private GameObject playIcon;

		[Header("Search")]
		[SerializeField]
		private GameObject searchForm;
		[SerializeField]
		private TMP_InputField searchTextInput;
		[SerializeField]
		private TMP_InputField searchTagInput;
		[SerializeField]
		private TextMeshProUGUI searchTagHint;
		[SerializeField]
		private Button[] searchDifficultyButtons = Array.Empty<Button>();
		[SerializeField]
		private Image[] searchDifficultyFills = Array.Empty<Image>();
		[SerializeField]
		private Button[] searchSortButtons = Array.Empty<Button>();
		[SerializeField]
		private Image[] searchSortFills = Array.Empty<Image>();
		[SerializeField]
		private Button searchButton;
		[SerializeField]
		private Button idSearchButton;
		[SerializeField]
		private GameObject idSearchPopup;
		[SerializeField]
		private TMP_InputField idSearchInput;
		[SerializeField]
		private Button idSearchConfirmButton;
		[SerializeField]
		private Button idSearchCancelButton;

		[Header("Account")]
		[SerializeField]
		private CommunityLoginPanel loginPanel;

		[SerializeField]
		private AudioSource previewSource;

		private readonly List<Row> _rows = new List<Row>();
		private Tab _tab = Tab.Recommended;
		private int _difficultyIndex;
		private bool _rankingDaily = true;
		private int _searchDifficultyIndex;
		private int _searchSortIndex;
		private bool _searchSubmitted;
		private CommunityQuery _searchQuery;
		private CommunityChart _idResult;
		private int _page;
		private int _pages = 1;
		private int _total;
		private bool _loading;
		private int _loadVersion;
		private CommunityChart _selected;
		private bool _downloading;
		private bool _started;
		private CancellationTokenSource _previewCts;
		private AudioClip _previewClip;
		private string _previewUrl;

		private CancellationToken DestroyToken => this.GetCancellationTokenOnDestroy();

		protected override void Awake()
		{
			base.Awake();
			backButton.onClick.AddListener(() => SceneManager.Instance.RequestScene(SceneManager.Scene.Home));
			for (int i = 0; i < tabButtons.Length; i++)
			{
				Tab tab = (Tab)i;
				tabButtons[i].onClick.AddListener(() => SelectTab(tab));
			}
			myListButton.onClick.AddListener(() => SelectTab(Tab.MyList));
			accountButton.onClick.AddListener(() => loginPanel.Show());
			for (int i = 0; i < difficultyButtons.Length; i++)
			{
				int index = i;
				difficultyButtons[i].onClick.AddListener(() => SetDifficultyFilter(index));
			}
			dailyButton.onClick.AddListener(() => SetRankingPeriod(true));
			totalButton.onClick.AddListener(() => SetRankingPeriod(false));
			searchBackButton.onClick.AddListener(ShowSearchForm);
			listScroll.onValueChanged.AddListener(_ => MaybeLoadMore());

			likeButton.onClick.AddListener(() => ToggleLikeAsync().Forget());
			previewButton.onClick.AddListener(TogglePreview);
			webPageButton.onClick.AddListener(() =>
			{
				if (_selected != null)
				{
					Application.OpenURL(CommunityClient.ChartPageUrl(_selected.Id));
				}
			});
			parentButton.onClick.AddListener(() => OpenParentAsync().Forget());
			bookmarkButton.onClick.AddListener(() => ToggleBookmarkAsync().Forget());
			playButton.onClick.AddListener(() => DownloadOrPlayAsync().Forget());

			for (int i = 0; i < searchDifficultyButtons.Length; i++)
			{
				int index = i;
				searchDifficultyButtons[i].onClick.AddListener(() =>
				{
					_searchDifficultyIndex = index;
					UpdateSearchFormVisuals();
				});
			}
			for (int i = 0; i < searchSortButtons.Length; i++)
			{
				int index = i;
				searchSortButtons[i].onClick.AddListener(() =>
				{
					_searchSortIndex = index;
					UpdateSearchFormVisuals();
				});
			}
			searchButton.onClick.AddListener(SubmitSearch);
			searchTextInput.onSubmit.AddListener(_ => SubmitSearch());
			searchTagInput.onSubmit.AddListener(_ => SubmitSearch());
			idSearchButton.onClick.AddListener(() =>
			{
				idSearchPopup.SetActive(true);
				idSearchInput.text = string.Empty;
				idSearchInput.ActivateInputField();
			});
			idSearchCancelButton.onClick.AddListener(() => idSearchPopup.SetActive(false));
			idSearchConfirmButton.onClick.AddListener(() => SearchByIdAsync().Forget());
			idSearchInput.onSubmit.AddListener(_ => SearchByIdAsync().Forget());
			idSearchInput.contentType = TMP_InputField.ContentType.IntegerNumber;

			cellTemplate.gameObject.SetActive(false);
			loginPanel.gameObject.SetActive(false);
			idSearchPopup.SetActive(false);
			CommunityAccount.Changed += OnAccountChanged;
			UpdateAccountButton();
			UpdateSearchFormVisuals();
			ApplyTabLayout();
			UpdateSelection(null);
			statusText.text = string.Empty;
		}

		private void OnDestroy()
		{
			CommunityAccount.Changed -= OnAccountChanged;
			StopPreview();
		}

		protected override void OnScreenStart()
		{
			UpdateAccountButton();
			if (!_started)
			{
				_started = true;
				Reload();
				LoadTagHintAsync().Forget();
			}
		}

		protected override void OnExited()
		{
			StopPreview();
			base.OnExited();
		}

		/* ---------- tabs and filters ---------- */

		private void SelectTab(Tab tab)
		{
			if (tab == Tab.MyList && !CommunityAccount.IsSignedIn)
			{
				loginPanel.Show("登录后才能使用マイリスト。", () => SelectTab(Tab.MyList));
				return;
			}
			if (tab == _tab && tab != Tab.Search)
			{
				return;
			}
			_tab = tab;
			if (tab == Tab.Search)
			{
				_searchSubmitted = false;
			}
			ApplyTabLayout();
			if (tab != Tab.Search)
			{
				Reload();
			}
		}

		private void ApplyTabLayout()
		{
			for (int i = 0; i < tabButtons.Length; i++)
			{
				bool active = (int)_tab == i;
				if (i < tabLabels.Length && tabLabels[i] != null)
				{
					tabLabels[i].color = active ? Color.white : TabIdle;
				}
				if (i < tabUnderlines.Length && tabUnderlines[i] != null)
				{
					tabUnderlines[i].SetActive(active);
				}
			}
			if (myListFace != null)
			{
				myListFace.color = _tab == Tab.MyList ? (Color)Teal : Color.white;
			}

			bool form = _tab == Tab.Search && !_searchSubmitted;
			searchForm.SetActive(form);
			listArea.SetActive(!form);
			detailWindow.SetActive(!form);
			actionButtons.SetActive(!form);
			statusText.gameObject.SetActive(!form);
			difficultyRail.SetActive(!form && (_tab == Tab.Ranking || _tab == Tab.New || _tab == Tab.MyList));
			periodToggle.SetActive(_tab == Tab.Ranking);
			searchBackButton.gameObject.SetActive(_tab == Tab.Search && _searchSubmitted);
			if (listCountLabel != null)
			{
				listCountLabel.text = _tab switch
				{
					Tab.Recommended => "おすすめ",
					Tab.Ranking => _rankingDaily ? "デイリー" : "総合",
					Tab.New => "新着",
					Tab.Search => "検索結果",
					_ => "マイリスト",
				};
			}
			UpdateDifficultyVisuals();
			UpdatePeriodVisuals();
			if (form)
			{
				StopPreview();
			}
		}

		private void SetDifficultyFilter(int index)
		{
			_difficultyIndex = Mathf.Clamp(index, 0, FilterDifficultyTypes.Length - 1);
			UpdateDifficultyVisuals();
			Reload();
		}

		private void SetRankingPeriod(bool daily)
		{
			if (_rankingDaily == daily)
			{
				return;
			}
			_rankingDaily = daily;
			ApplyTabLayout();
			Reload();
		}

		private void UpdateDifficultyVisuals()
		{
			PaintRadio(difficultyFills, _difficultyIndex, i => i == 0 ? Teal : CustomMusicScoreDifficultyBadge.GetDifficultyColor(FilterDifficultyTypes[i]));
		}

		private void UpdatePeriodVisuals()
		{
			if (dailyFill != null)
			{
				dailyFill.color = _rankingDaily ? Teal : IdleFill;
			}
			if (totalFill != null)
			{
				totalFill.color = _rankingDaily ? IdleFill : Teal;
			}
		}

		private void UpdateSearchFormVisuals()
		{
			PaintRadio(searchDifficultyFills, _searchDifficultyIndex, i => i == 0 ? Teal : CustomMusicScoreDifficultyBadge.GetDifficultyColor(FilterDifficultyTypes[i]));
			PaintRadio(searchSortFills, _searchSortIndex, _ => Teal);
		}

		private static void PaintRadio(Image[] fills, int selected, Func<int, Color32> colorOf)
		{
			for (int i = 0; i < fills.Length; i++)
			{
				if (fills[i] != null)
				{
					fills[i].color = i == selected ? colorOf(i) : IdleFill;
				}
			}
		}

		/* ---------- search ---------- */

		private void ShowSearchForm()
		{
			_searchSubmitted = false;
			ApplyTabLayout();
		}

		private void SubmitSearch()
		{
			_searchQuery = new CommunityQuery
			{
				Text = searchTextInput.text,
				Tag = searchTagInput.text,
				Difficulty = FilterDifficultyTypes[Mathf.Clamp(_searchDifficultyIndex, 0, FilterDifficultyTypes.Length - 1)],
				Sort = SearchSorts[Mathf.Clamp(_searchSortIndex, 0, SearchSorts.Length - 1)],
				Seed = UnityEngine.Random.Range(0, int.MaxValue).ToString(CultureInfo.InvariantCulture),
			};
			_idResult = null;
			_searchSubmitted = true;
			ApplyTabLayout();
			Reload();
		}

		private async UniTaskVoid SearchByIdAsync()
		{
			string text = idSearchInput.text.Trim().TrimStart('#');
			if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) || id <= 0)
			{
				SetStatus("请输入谱面 ID（数字）。");
				return;
			}
			idSearchPopup.SetActive(false);
			try
			{
				CommunityChart chart = await CommunityClient.GetAsync(id, DestroyToken);
				if (chart == null)
				{
					_searchSubmitted = false;
					ApplyTabLayout();
					SetStatus("找不到谱面 #" + id + "。");
					return;
				}
				_idResult = chart;
				_searchQuery = null;
				_searchSubmitted = true;
				ApplyTabLayout();
				Reload();
			}
			catch (CommunityException exception)
			{
				SetStatus(exception.Message);
			}
		}

		private async UniTaskVoid LoadTagHintAsync()
		{
			if (searchTagHint == null)
			{
				return;
			}
			try
			{
				List<CommunityTagCount> tags = await CommunityClient.ListTagsAsync(DestroyToken);
				searchTagHint.text = tags.Count == 0
					? string.Empty
					: "常用：" + string.Join("、", tags.GetRange(0, Math.Min(8, tags.Count)).ConvertAll(t => t.Tag));
			}
			catch (Exception exception) when (exception is CommunityException || exception is OperationCanceledException)
			{
				searchTagHint.text = string.Empty;
			}
		}

		/* ---------- list ---------- */

		private void Reload()
		{
			_loadVersion++;
			_page = 0;
			_pages = 1;
			_total = 0;
			_loading = false;
			ClearRows();
			UpdateSelection(null);
			emptyText.gameObject.SetActive(false);
			listScroll.verticalNormalizedPosition = 1f;
			LoadNextPageAsync().Forget();
		}

		private void MaybeLoadMore()
		{
			if (!_loading && _page < _pages && listScroll.verticalNormalizedPosition < 0.08f)
			{
				LoadNextPageAsync().Forget();
			}
		}

		private CommunityQuery BuildQuery(int page)
		{
			string difficulty = FilterDifficultyTypes[Mathf.Clamp(_difficultyIndex, 0, FilterDifficultyTypes.Length - 1)];
			switch (_tab)
			{
				case Tab.Recommended:
					return new CommunityQuery { Sort = CommunitySort.Recommended, Page = page };
				case Tab.Ranking:
					return new CommunityQuery { Sort = _rankingDaily ? CommunitySort.Daily : CommunitySort.Plays, Difficulty = difficulty, Page = page };
				case Tab.New:
					return new CommunityQuery { Sort = CommunitySort.New, Difficulty = difficulty, Page = page };
				case Tab.MyList:
					return new CommunityQuery { Sort = CommunitySort.New, Difficulty = difficulty, Page = page };
				default:
					CommunityQuery query = (_searchQuery ?? new CommunityQuery()).Clone();
					query.Page = page;
					return query;
			}
		}

		private async UniTaskVoid LoadNextPageAsync()
		{
			if (_loading || _page >= _pages)
			{
				return;
			}
			int version = _loadVersion;
			int page = _page + 1;
			_loading = true;
			if (page == 1)
			{
				SetStatus("正在加载…");
			}
			try
			{
				CommunityChartPage result;
				if (_tab == Tab.Search && _idResult != null)
				{
					result = new CommunityChartPage { Items = new List<CommunityChart> { _idResult }, Total = 1, Page = 1, Pages = 1 };
				}
				else if (_tab == Tab.MyList)
				{
					result = await CommunityClient.ListBookmarksAsync(BuildQuery(page), DestroyToken);
				}
				else
				{
					result = await CommunityClient.ListAsync(BuildQuery(page), DestroyToken);
				}
				if (version != _loadVersion)
				{
					return;
				}
				_page = result.Page;
				_pages = Math.Max(1, result.Pages);
				_total = result.Total;
				foreach (CommunityChart chart in result.Items)
				{
					_rows.Add(CreateRow(chart, _tab == Tab.Ranking ? _rows.Count + 1 : 0));
				}
				listCountText.text = _total.ToString(CultureInfo.InvariantCulture);
				emptyText.gameObject.SetActive(_rows.Count == 0);
				emptyText.text = _tab switch
				{
					Tab.MyList => "マイリスト还是空的。\n在谱面详情里点「登録」加入。",
					Tab.Search => "没有符合条件的谱面。",
					_ => "还没有谱面。",
				};
				SetStatus(string.Empty);
				if (_selected == null && _rows.Count > 0)
				{
					UpdateSelection(_rows[0].Chart);
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (CommunityException exception)
			{
				if (version != _loadVersion)
				{
					return;
				}
				if (exception.Code == "AUTH_REQUIRED" && _tab == Tab.MyList)
				{
					SelectTab(Tab.Recommended);
					loginPanel.Show("登录已过期，请重新登录。", () => SelectTab(Tab.MyList));
				}
				emptyText.gameObject.SetActive(_rows.Count == 0);
				emptyText.text = "无法加载谱面。";
				SetStatus(exception.Message);
			}
			finally
			{
				if (version == _loadVersion)
				{
					_loading = false;
				}
			}
		}

		private Row CreateRow(CommunityChart chart, int rank)
		{
			CustomMusicScoreEntryCell cell = Instantiate(cellTemplate, listContent, false);
			cell.name = "ChartCell_" + chart.Id;
			cell.gameObject.SetActive(true);
			Row row = new Row(cell, chart, rank);
			cell.Button.onClick.AddListener(() => UpdateSelection(chart));
			FillRow(row);
			if (cell.JacketImage != null)
			{
				cell.JacketImage.color = Color.clear;
				LoadRowJacketAsync(row).Forget();
			}
			return row;
		}

		private void FillRow(Row row)
		{
			CommunityChart chart = row.Chart;
			CustomMusicScoreEntryCell cell = row.Cell;
			string title = string.IsNullOrWhiteSpace(chart.ScoreTitle) ? chart.Title : chart.ScoreTitle;
			cell.TitleText.text = row.Rank > 0
				? "<color=" + RankColor(row.Rank) + "><b>" + row.Rank + "</b></color>  " + title
				: title;
			string meta = chart.ScoreTitle != chart.Title && !string.IsNullOrWhiteSpace(chart.Title) ? chart.Title : chart.ArtistLine;
			cell.MetaText.text = string.IsNullOrWhiteSpace(meta) ? chart.Uploader.Nickname : meta + "  /  " + chart.Uploader.Nickname;
			cell.StatusText.text = StatsLine(chart) + BadgesLine(chart);
			cell.StatusText.color = Color.white;
			cell.DifficultyBadge?.Set(chart.Difficulty, chart.PlayLevel);
		}

		private static string RankColor(int rank) => rank switch
		{
			1 => "#FFD54A",
			2 => "#D7DCE8",
			3 => "#F0A867",
			_ => "#9C98C8",
		};

		private static string StatsLine(CommunityChart chart)
		{
			return "<color=#9C98C8>▶</color> " + chart.PlayCount + "   <color=#FF7DA8>♥</color> " + chart.LikeCount;
		}

		private static string BadgesLine(CommunityChart chart)
		{
			string badges = string.Empty;
			if (chart.ParentChartId.HasValue)
			{
				badges += "   <color=#67DDCD>親譜面あり</color>";
			}
			if (chart.AllowDerivative)
			{
				badges += "   <color=#FF8FB8>二次利用OK</color>";
			}
			switch (CommunityLibrary.GetState(chart))
			{
				case CommunityInstallState.Installed:
					badges += "   <color=#A9E879>已下载</color>";
					break;
				case CommunityInstallState.Outdated:
					badges += "   <color=#FFC04D>有更新</color>";
					break;
			}
			return badges;
		}

		private async UniTaskVoid LoadRowJacketAsync(Row row)
		{
			(bool cancelled, Sprite sprite) = await CommunityImageCache.LoadAsync(row.Chart.JacketUrl, DestroyToken).SuppressCancellationThrow();
			if (!cancelled && row.Cell != null && row.Cell.JacketImage != null)
			{
				row.Cell.JacketImage.sprite = sprite;
				row.Cell.JacketImage.color = sprite != null ? Color.white : Color.clear;
			}
		}

		private void ClearRows()
		{
			foreach (Row row in _rows)
			{
				if (row.Cell != null)
				{
					Destroy(row.Cell.gameObject);
				}
			}
			_rows.Clear();
		}

		/* ---------- selection and chart window ---------- */

		private void UpdateSelection(CommunityChart chart)
		{
			if (_selected != chart)
			{
				StopPreview();
			}
			_selected = chart;
			for (int i = 0; i < _rows.Count; i++)
			{
				bool selected = _rows[i].Chart == chart;
				bool nextSelected = i + 1 < _rows.Count && _rows[i + 1].Chart == chart;
				CustomMusicScoreEntryCell cell = _rows[i].Cell;
				cell.Background.color = selected ? new Color32(255, 255, 255, 96) : new Color32(255, 255, 255, 0);
				cell.SelectedFrame?.SetActive(selected);
				cell.Separator?.SetActive(!selected && !nextSelected && i < _rows.Count - 1);
			}

			bool has = chart != null;
			detailEmptyState.SetActive(!has);
			detailContent.SetActive(has);
			if (!has)
			{
				windowTitleText.text = "NOT FOUND";
				if (detailEmptyHint != null)
				{
					detailEmptyHint.text = _loading ? "正在加载…" : "选择左侧的谱面。";
				}
				jacketImage.sprite = null;
				jacketImage.color = Color.clear;
				UpdateActionButtons();
				return;
			}

			windowTitleText.text = string.IsNullOrWhiteSpace(chart.ArtistLine) ? chart.Title : chart.Title + " - " + chart.ArtistLine;
			detailTitle.text = string.IsNullOrWhiteSpace(chart.ScoreTitle) ? chart.Title : chart.ScoreTitle;
			detailStatus.text = "<color=#9C98C8>谱师</color>  " + chart.Uploader.Nickname + "   <color=#9C98C8>ID</color>  #" + chart.Id + BadgesLine(chart);
			detailBadge?.Set(chart.Difficulty, chart.PlayLevel);
			List<string> credits = new List<string>();
			void Credit(string label, string value)
			{
				if (!string.IsNullOrWhiteSpace(value))
				{
					credits.Add("<color=#9C98C8>" + label + "</color>  " + value);
				}
			}
			Credit("曲名", chart.ScoreTitle != chart.Title ? chart.Title : null);
			Credit("歌手", chart.Singer);
			Credit("作曲", chart.Composer);
			Credit("作词", chart.Lyricist);
			Credit("编曲", chart.Arranger);
			Credit("标签", chart.Tags.Count > 0 ? string.Join(" · ", chart.Tags) : null);
			detailMeta.text = string.Join("\n", credits);
			infoLabel.text = "谱面信息";
			infoLeft.text = "<color=#9C98C8>游玩</color>  " + chart.PlayCount
				+ "\n<color=#9C98C8>喜欢</color>  " + chart.LikeCount
				+ "\n<color=#9C98C8>下载</color>  " + chart.DownloadCount;
			infoRight.text = "<color=#9C98C8>上传</color>  " + chart.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
				+ "\n<color=#9C98C8>更新</color>  " + chart.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
				+ "\n<color=#9C98C8>大小</color>  " + (chart.PackageSize / 1024f / 1024f).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
				+ (chart.HasVideo ? "  · MV" : string.Empty);
			detailDescription.text = string.IsNullOrWhiteSpace(chart.Description) ? "<color=#8F8BB8>（没有简介）</color>" : chart.Description;
			jacketImage.color = Color.clear;
			LoadDetailJacketAsync(chart).Forget();
			UpdateActionButtons();
		}

		private async UniTaskVoid LoadDetailJacketAsync(CommunityChart chart)
		{
			(bool cancelled, Sprite sprite) = await CommunityImageCache.LoadAsync(chart.JacketUrl, DestroyToken).SuppressCancellationThrow();
			if (!cancelled && _selected == chart)
			{
				jacketImage.sprite = sprite;
				jacketImage.color = sprite != null ? Color.white : Color.clear;
			}
		}

		private void UpdateActionButtons()
		{
			CommunityChart chart = _selected;
			bool has = chart != null;
			parentButton.interactable = has && chart.ParentChartId.HasValue && !_downloading;
			bookmarkButton.interactable = has && !_downloading;
			bookmarkLabel.text = has && chart.Bookmarked == true ? "登録済み" : "登録";
			playButton.interactable = has && !_downloading;
			if (!_downloading)
			{
				playLabel.text = !has
					? "プレイ"
					: CommunityLibrary.GetState(chart) switch
					{
						CommunityInstallState.Installed => "プレイ",
						CommunityInstallState.Outdated => "更新",
						_ => "ダウンロード",
					};
			}
			if (playIcon != null)
			{
				// The label needs the icon's room for ダウンロード and the download progress.
				playIcon.SetActive(playLabel.text == "プレイ");
			}
			likeButton.interactable = has;
			likeLabel.text = has ? (chart.Liked == true ? "<color=#FF7DA8>♥</color> " : "♡ ") + chart.LikeCount : "♡";
			previewButton.interactable = has && !string.IsNullOrEmpty(chart.AudioUrl);
			webPageButton.interactable = has;
			if (previewLabel != null && _previewCts == null)
			{
				previewLabel.text = "试听";
			}
			foreach (Button button in new[] { parentButton, bookmarkButton, playButton, likeButton, previewButton, webPageButton })
			{
				if (button != null && button.TryGetComponent(out CanvasGroup group))
				{
					group.alpha = button.interactable ? 1f : 0.4f;
				}
			}
		}

		/// <summary>The chart may be a separate copy (親譜面, ID search): carry its counters over to the list row.</summary>
		private void RefreshRowFor(CommunityChart chart)
		{
			foreach (Row row in _rows)
			{
				if (row.Chart.Id != chart.Id)
				{
					continue;
				}
				if (!ReferenceEquals(row.Chart, chart))
				{
					row.Chart.LikeCount = chart.LikeCount;
					row.Chart.Liked = chart.Liked;
					row.Chart.Bookmarked = chart.Bookmarked;
					row.Chart.DownloadCount = chart.DownloadCount;
					row.Chart.PlayCount = chart.PlayCount;
				}
				FillRow(row);
			}
		}

		/* ---------- actions ---------- */

		private bool RequireLogin(string reason, Action retry)
		{
			if (CommunityAccount.IsSignedIn)
			{
				return true;
			}
			loginPanel.Show(reason, retry);
			return false;
		}

		private async UniTaskVoid ToggleLikeAsync()
		{
			CommunityChart chart = _selected;
			if (chart == null || !RequireLogin("登录后才能点喜欢。", () => ToggleLikeAsync().Forget()))
			{
				return;
			}
			bool liked = chart.Liked != true;
			try
			{
				chart.LikeCount = await CommunityClient.SetLikeAsync(chart.Id, liked, DestroyToken);
				chart.Liked = liked;
				RefreshRowFor(chart);
				if (_selected == chart)
				{
					UpdateSelection(chart);
				}
			}
			catch (CommunityException exception)
			{
				HandleActionError(exception);
			}
		}

		private async UniTaskVoid ToggleBookmarkAsync()
		{
			CommunityChart chart = _selected;
			if (chart == null || !RequireLogin("登录后才能加入マイリスト。", () => ToggleBookmarkAsync().Forget()))
			{
				return;
			}
			bool saved = chart.Bookmarked != true;
			try
			{
				await CommunityClient.SetBookmarkAsync(chart.Id, saved, DestroyToken);
				chart.Bookmarked = saved;
				RefreshRowFor(chart);
				SetStatus(saved ? "已加入マイリスト。" : "已从マイリスト移除。");
				UpdateActionButtons();
			}
			catch (CommunityException exception)
			{
				HandleActionError(exception);
			}
		}

		private async UniTaskVoid OpenParentAsync()
		{
			CommunityChart chart = _selected;
			if (chart?.ParentChartId == null)
			{
				return;
			}
			try
			{
				CommunityChart parent = await CommunityClient.GetAsync(chart.ParentChartId.Value, DestroyToken);
				if (parent == null)
				{
					SetStatus("親譜面已经不在了。");
					return;
				}
				UpdateSelection(parent);
				SetStatus("正在显示親譜面 #" + parent.Id + "。");
			}
			catch (CommunityException exception)
			{
				SetStatus(exception.Message);
			}
		}

		private async UniTaskVoid DownloadOrPlayAsync()
		{
			CommunityChart chart = _selected;
			if (chart == null || _downloading)
			{
				return;
			}
			if (CommunityLibrary.GetState(chart) == CommunityInstallState.Installed)
			{
				StopPreview();
				string error = await CustomMusicScorePlayLauncher.PlayAsync(
					CommunityLibrary.GetEntry(chart.Id), false, MenuScreenType.CommunityCharts, SetStatus, DestroyToken);
				SetStatus(error ?? "正在开始游玩...");
				return;
			}

			_downloading = true;
			UpdateActionButtons();
			try
			{
				// Synchronous, so a late report cannot overwrite the label after the download finished.
				IProgress<float> progress = Cysharp.Threading.Tasks.Progress.Create<float>(p =>
				{
					if (_downloading)
					{
						playLabel.text = Mathf.RoundToInt(p * 100f) + "%";
					}
				});
				if (playIcon != null)
				{
					playIcon.SetActive(false);
				}
				SetStatus("正在下载 " + chart.ScoreTitle + "…");
				await CommunityLibrary.DownloadAsync(chart, progress, DestroyToken);
				chart.DownloadCount++;
				SetStatus("已下载，可以在自制谱管理里找到它。");
			}
			catch (OperationCanceledException)
			{
			}
			catch (CommunityException exception)
			{
				SetStatus(exception.Message);
			}
			finally
			{
				_downloading = false;
				RefreshRowFor(chart);
				if (_selected == chart)
				{
					UpdateSelection(chart);
				}
				else
				{
					UpdateActionButtons();
				}
			}
		}

		private void HandleActionError(CommunityException exception)
		{
			if (exception.Code == "AUTH_REQUIRED")
			{
				loginPanel.Show("登录已过期，请重新登录。");
				return;
			}
			SetStatus(exception.Message);
		}

		private void OnAccountChanged()
		{
			UpdateAccountButton();
			if (!CommunityAccount.IsSignedIn && _tab == Tab.MyList)
			{
				SelectTab(Tab.Recommended);
			}
			else if (_started && _tab != Tab.Search)
			{
				// Liked / 登録済み flags depend on who is signed in.
				Reload();
			}
		}

		private void UpdateAccountButton()
		{
			accountLabel.text = CommunityAccount.IsSignedIn ? CommunityAccount.Nickname : "登录";
		}

		private void SetStatus(string message)
		{
			statusText.text = message ?? string.Empty;
		}

		/* ---------- audio preview ---------- */

		private void TogglePreview()
		{
			if (_previewCts != null)
			{
				StopPreview();
				return;
			}
			if (_selected == null || string.IsNullOrEmpty(_selected.AudioUrl))
			{
				return;
			}
			_previewCts = CancellationTokenSource.CreateLinkedTokenSource(DestroyToken);
			PlayPreviewAsync(_selected, _previewCts.Token).Forget();
		}

		private async UniTaskVoid PlayPreviewAsync(CommunityChart chart, CancellationToken ct)
		{
			previewLabel.text = "加载中…";
			try
			{
				if (_previewClip == null || _previewUrl != chart.AudioUrl)
				{
					ReleasePreviewClip();
					AudioType type = chart.AudioUrl.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? AudioType.MPEG
						: chart.AudioUrl.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? AudioType.WAV
						: AudioType.OGGVORBIS;
					using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(chart.AudioUrl, type);
					request.timeout = 60;
					await request.SendWebRequest().WithCancellation(ct);
					_previewClip = DownloadHandlerAudioClip.GetContent(request);
					_previewUrl = chart.AudioUrl;
				}
				if (_previewClip == null)
				{
					throw new CommunityException("PREVIEW_FAILED", "无法播放试听。");
				}
				previewSource.clip = _previewClip;
				previewSource.time = Mathf.Clamp(chart.PreviewStartSec, 0f, Mathf.Max(0f, _previewClip.length - 1f));
				previewSource.Play();
				previewLabel.text = "停止";
				float until = Time.unscaledTime + PreviewSeconds;
				while (previewSource.isPlaying && Time.unscaledTime < until)
				{
					await UniTask.Yield(PlayerLoopTiming.Update, ct);
				}
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception exception) when (exception is CommunityException || exception is UnityWebRequestException)
			{
				SetStatus("无法播放试听。");
			}
			StopPreview();
		}

		private void StopPreview()
		{
			if (_previewCts != null)
			{
				_previewCts.Cancel();
				_previewCts.Dispose();
				_previewCts = null;
			}
			if (previewSource != null)
			{
				previewSource.Stop();
			}
			if (previewLabel != null)
			{
				previewLabel.text = "试听";
			}
		}

		private void ReleasePreviewClip()
		{
			if (_previewClip != null)
			{
				if (previewSource != null && previewSource.clip == _previewClip)
				{
					previewSource.clip = null;
				}
				Destroy(_previewClip);
				_previewClip = null;
				_previewUrl = null;
			}
		}

		private sealed class Row
		{
			public Row(CustomMusicScoreEntryCell cell, CommunityChart chart, int rank)
			{
				Cell = cell;
				Chart = chart;
				Rank = rank;
			}

			public CustomMusicScoreEntryCell Cell { get; }

			public CommunityChart Chart { get; }

			public int Rank { get; }
		}
	}
}
