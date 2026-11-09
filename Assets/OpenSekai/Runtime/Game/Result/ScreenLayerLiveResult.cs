using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using OpenSekai.SceneFlow;
using Sekai.Community;
using Sekai.Core.Live;
using Sekai.CoopLive;
using Sekai.CoopLive.Proto;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// The Result scene, after every live started from the chart manager, the community charts or a 協力ライブ room.
	/// Like the original it has two pages under one header (jacket, difficulty, the score gauge and the SCORERANK):
	/// <list type="bullet">
	/// <item>Members (co-op only): one card per player, all the same size, in a row that scrolls sideways. Until everyone
	/// has finished the cards show the scores as they come in; then MVP / SUPER STAR, the team score and FEVER. The header
	/// gauge is the team's, one segment per player. メンバーの成績 lists everyone's judgements; スタンプ work here.</item>
	/// <item>Detail: this player's score, high score, judgements, combo and FAST / LATE (<see cref="LiveResultData"/>).</item>
	/// </list>
	/// 次へ on the last page goes back to the chart list after a solo live; after a co-op live it asks whether to go back
	/// to the room. A co-op round starting meanwhile takes the player back to the room by itself.
	/// </summary>
	public sealed class ScreenLayerLiveResult : ScreenLayer
	{
		private const float HeaderHeight = 180f;
		private const float CardWidth = 372f;
		private const float CardHeight = 520f;
		private const float CardArtHeight = 280f;
		private const float RibbonSpace = 44f;

		private static readonly Color HeaderColor = new Color32(0xD4, 0xD1, 0xE4, 0xE6);
		private static readonly Color RankBoxColor = new Color32(0x3C, 0x38, 0x5E, 0xFF);
		private static readonly Color GaugeBack = new Color32(0x3C, 0x38, 0x5E, 0xFF);
		private static readonly Color HeaderInk = new Color32(0x55, 0x55, 0x77, 0xFF);
		private static readonly Color ScorePink = new Color32(0xFF, 0x66, 0x99, 0xFF);
		private static readonly Color RowShade = new Color32(0xFF, 0xFF, 0xFF, 0x14);
		private const string DimHex = "#8E93A8";

		[SerializeField]
		private Sprite roundSprite;

		[SerializeField]
		private Sprite backgroundSprite;

		private RectTransform _root;
		private RectTransform _pageRoot;
		private TextMeshProUGUI _toast;
		private float _toastUntil;

		// Header
		private Image _jacket;
		private Image _jacketFrame;
		private TextMeshProUGUI _songTitle;
		private Image _difficultyPill;
		private TextMeshProUGUI _difficultyLabel;
		private TextMeshProUGUI _levelLabel;
		private RectTransform _gaugeFill;
		private TextMeshProUGUI _rankLetter;

		private enum Page
		{
			Members,
			Detail,
		}

		private Page _page;
		private bool _coop;
		private int _round;
		private LiveResultData _mine;
		private bool _leaving;
		private string _membersShape;
		private Sprite _gradient;
		private Sprite _localJacket;
		private RectTransform _modal;
		private RectTransform _stampPopup;
		private TextMeshProUGUI _statusLine;

		private sealed class CardView
		{
			public RectTransform Root;
			public TextMeshProUGUI Score;
			public TextMeshProUGUI State;
		}

		private readonly Dictionary<string, CardView> _cards = new Dictionary<string, CardView>();
		private readonly List<(GameObject bubble, float until)> _bubbles = new List<(GameObject, float)>();

		protected override void Awake()
		{
			base.Awake();
			CoopUi.RoundSprite = roundSprite;
			_root = CoopUi.Rect("ResultRoot", transform).Stretch();
			BuildBackground();
			BuildHeader();
			_pageRoot = CoopUi.Rect("Page", _root).Stretch(0, HeaderHeight, 0, 0);
			_toast = CoopUi.Toast(_root);
			_gradient = MakeGradient();
		}

		protected override void OnScreenStart()
		{
			_leaving = false;
			CoopResultSceneArgs args = CoopResultSceneArgs.Take();
			LiveResultData last = LiveResultData.Last;
			_coop = args != null || last?.IsCoop == true;
			_round = args?.Round ?? (last?.IsCoop == true ? last.CoopRound : CoopSession.LastPlayedRound);
			_mine = last != null && (!_coop || last.IsCoop && last.CoopRound == _round) ? last : null;
			if (_coop)
			{
				CoopSession.RoomChanged += OnRoom;
				CoopSession.StatusesChanged += OnStatuses;
				CoopSession.StampReceived += OnStamp;
				CoopSession.Closed += OnClosed;
			}
			LoadJacket();
			ShowPage(_coop ? Page.Members : Page.Detail);
		}

		private void OnDestroy()
		{
			CoopSession.RoomChanged -= OnRoom;
			CoopSession.StatusesChanged -= OnStatuses;
			CoopSession.StampReceived -= OnStamp;
			CoopSession.Closed -= OnClosed;
			DOTween.Kill(this);
			DestroySprite(_gradient);
			DestroySprite(_localJacket);
		}

		private static void DestroySprite(Sprite sprite)
		{
			if (sprite == null)
			{
				return;
			}
			if (sprite.texture != null)
			{
				Destroy(sprite.texture);
			}
			Destroy(sprite);
		}

		/* ---------- frame ---------- */

		private void BuildBackground()
		{
			Image background = CoopUi.Image(_root, "Background", Color.white, rounded: false);
			background.rectTransform.Stretch();
			background.sprite = backgroundSprite;
			background.color = backgroundSprite != null ? Color.white : new Color32(0x3A, 0x33, 0x6E, 0xFF);
			if (backgroundSprite != null)
			{
				AspectRatioFitter fitter = background.gameObject.AddComponent<AspectRatioFitter>();
				fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
				fitter.aspectRatio = backgroundSprite.rect.width / Mathf.Max(1f, backgroundSprite.rect.height);
			}
			Image tint = CoopUi.Image(_root, "Tint", new Color32(0x2C, 0x26, 0x58, 0xD8), rounded: false);
			tint.rectTransform.Stretch();
			TextMeshProUGUI watermark = CoopUi.Text(_root, "Watermark", "RESULT", 300, new Color(1f, 1f, 1f, 0.06f), true);
			watermark.rectTransform.Place(new Vector2(0, 1), new Vector2(-10, 20), new Vector2(1400, 300), new Vector2(0, 1));
			watermark.characterSpacing = 8;
		}

		private void BuildHeader()
		{
			Image header = CoopUi.Image(_root, "Header", HeaderColor);
			RectTransform rt = header.rectTransform;
			rt.anchorMin = new Vector2(0, 1);
			rt.anchorMax = new Vector2(1, 1);
			rt.pivot = new Vector2(0.5f, 1);
			rt.offsetMin = new Vector2(180, -HeaderHeight);
			rt.offsetMax = new Vector2(-180, 40);

			_jacketFrame = CoopUi.Image(rt, "JacketFrame", CoopUi.Accent);
			_jacketFrame.rectTransform.Place(new Vector2(0, 0), new Vector2(40, 22), new Vector2(136, 136), new Vector2(0, 0));
			_jacket = CoopUi.Image(_jacketFrame.transform, "Jacket", new Color32(0x55, 0x50, 0x80, 0xFF), rounded: false);
			_jacket.rectTransform.Stretch(6, 6, 6, 6);
			_jacket.preserveAspect = false;

			_songTitle = CoopUi.Text(rt, "Title", string.Empty, 40, HeaderInk, true);
			_songTitle.rectTransform.Place(new Vector2(0, 0), new Vector2(210, 100), new Vector2(540, 56), new Vector2(0, 0));

			_difficultyPill = CoopUi.Image(rt, "Difficulty", CoopUi.DifficultyColor("master"));
			_difficultyPill.rectTransform.Place(new Vector2(0, 0), new Vector2(210, 26), new Vector2(200, 60), new Vector2(0, 0));
			_difficultyLabel = CoopUi.Text(_difficultyPill.transform, "Label", string.Empty, 34, Color.white, true, TextAlignmentOptions.Center);
			_difficultyLabel.rectTransform.Stretch(8, 0, 8, 0);
			Image level = CoopUi.Image(rt, "Level", RankBoxColor);
			level.rectTransform.Place(new Vector2(0, 0), new Vector2(404, 26), new Vector2(200, 60), new Vector2(0, 0));
			_levelLabel = CoopUi.Text(level.transform, "Label", string.Empty, 30, Color.white, true, TextAlignmentOptions.Center);
			_levelLabel.rectTransform.Stretch(8, 0, 8, 0);

			Image rankBox = CoopUi.Image(rt, "RankBox", RankBoxColor, rounded: false);
			rankBox.rectTransform.anchorMin = new Vector2(1, 0);
			rankBox.rectTransform.anchorMax = new Vector2(1, 1);
			rankBox.rectTransform.pivot = new Vector2(1, 0.5f);
			rankBox.rectTransform.offsetMin = new Vector2(-185, 0);
			rankBox.rectTransform.offsetMax = new Vector2(-55, 0);
			_rankLetter = CoopUi.Text(rankBox.transform, "Rank", "-", 150, Color.white, true, TextAlignmentOptions.Center);
			_rankLetter.rectTransform.Stretch(0, -10, 0, 34);
			TextMeshProUGUI caption = CoopUi.Text(rankBox.transform, "Caption", "SCORERANK", 18, new Color32(0xFF, 0x88, 0xBB, 0xFF), true, TextAlignmentOptions.Center);
			caption.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(130, 26), new Vector2(0.5f, 0));

			RectTransform gauge = CoopUi.Rect("Gauge", rt);
			gauge.Place(new Vector2(1, 0), new Vector2(-225, 62), new Vector2(560, 28), new Vector2(1, 0));
			Image back = CoopUi.Image(gauge, "Back", GaugeBack);
			back.rectTransform.Stretch();
			RectTransform clip = CoopUi.Rect("Clip", gauge).Stretch(3, 3, 3, 3);
			clip.gameObject.AddComponent<RectMask2D>();
			_gaugeFill = CoopUi.Rect("Fill", clip);
			_gaugeFill.anchorMin = Vector2.zero;
			_gaugeFill.anchorMax = new Vector2(0, 1);
			_gaugeFill.pivot = new Vector2(0, 0.5f);
			_gaugeFill.offsetMin = Vector2.zero;
			_gaugeFill.offsetMax = Vector2.zero;
			string[] letters = { "C", "B", "A", "S" };
			float[] rates = { ScoreGaugeCalculator.RankRateC, ScoreGaugeCalculator.RankRateB, ScoreGaugeCalculator.RankRateA, ScoreGaugeCalculator.RankRateS };
			for (int i = 0; i < letters.Length; i++)
			{
				Image tick = CoopUi.Image(gauge, "Tick" + letters[i], Color.white, rounded: false);
				tick.rectTransform.Place(new Vector2(rates[i], 0), new Vector2(0, 0), new Vector2(3, 44), new Vector2(0.5f, 0));
				Image arrow = CoopUi.Image(gauge, "Arrow" + letters[i], Color.white, rounded: false);
				arrow.rectTransform.Place(new Vector2(rates[i], 1), new Vector2(0, 22), new Vector2(13, 13), new Vector2(0.5f, 0.5f));
				arrow.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
				TextMeshProUGUI letter = CoopUi.Text(gauge, "Mark" + letters[i], letters[i], 28, Color.white, true, TextAlignmentOptions.Center);
				letter.rectTransform.Place(new Vector2(rates[i], 1), new Vector2(0, 30), new Vector2(50, 38), new Vector2(0.5f, 0));
			}
		}

		private void SetHeader(string title, string difficulty, int level, long score, int players, IList<(long score, Color color)> segments)
		{
			_songTitle.text = string.IsNullOrEmpty(title) ? "-" : title;
			_difficultyPill.color = CoopUi.DifficultyColor(difficulty);
			_jacketFrame.color = CoopUi.DifficultyColor(difficulty);
			_difficultyLabel.text = CoopUi.DifficultyLabel(difficulty);
			_levelLabel.text = level > 0 ? $"<size=22>楽曲Lv.</size>{level}" : "<size=22>楽曲Lv.</size>-";
			ScoreRank rank = LiveResultData.RankOf(score, players);
			_rankLetter.text = rank.ToString();
			_rankLetter.color = Sekai.ColorUtility.GetScoreRankColor(rank);

			CoopUi.Clear(_gaugeFill);
			float width = ((RectTransform)_gaugeFill.parent).rect.width;
			if (width <= 0f)
			{
				width = 554f;
			}
			if (segments == null)
			{
				Image fill = CoopUi.Image(_gaugeFill, "Fill", Color.white, rounded: false);
				fill.sprite = _gradient;
				fill.rectTransform.Stretch();
				float to = LiveResultData.GaugeOf(score, players);
				_gaugeFill.offsetMax = new Vector2(0, 0);
				DOVirtual.Float(0f, to, 1.0f, v => _gaugeFill.offsetMax = new Vector2(width * v, 0)).SetEase(Ease.OutCubic).SetTarget(this);
				return;
			}
			_gaugeFill.offsetMax = new Vector2(width, 0);
			long sum = 0;
			foreach ((long segmentScore, Color color) in segments)
			{
				float from = LiveResultData.GaugeOf(sum, players);
				sum += segmentScore;
				float to = LiveResultData.GaugeOf(sum, players);
				if (to <= from)
				{
					continue;
				}
				Image part = CoopUi.Image(_gaugeFill, "Segment", color, rounded: false);
				part.rectTransform.anchorMin = new Vector2(from, 0);
				part.rectTransform.anchorMax = new Vector2(to, 1);
				part.rectTransform.offsetMin = Vector2.zero;
				part.rectTransform.offsetMax = Vector2.zero;
			}
		}

		private static Sprite MakeGradient()
		{
			Color[] stops = { new Color32(0x7C, 0xF5, 0xC8, 0xFF), new Color32(0x55, 0xD8, 0xF0, 0xFF), new Color32(0x55, 0x99, 0xFF, 0xFF), new Color32(0x99, 0x77, 0xFF, 0xFF), new Color32(0xFF, 0x77, 0xBB, 0xFF) };
			Texture2D texture = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
			for (int x = 0; x < 64; x++)
			{
				float t = x / 63f * (stops.Length - 1);
				int i = Mathf.Min(stops.Length - 2, Mathf.FloorToInt(t));
				texture.SetPixel(x, 0, Color.Lerp(stops[i], stops[i + 1], t - i));
			}
			texture.Apply();
			return Sprite.Create(texture, new Rect(0, 0, 64, 1), new Vector2(0.5f, 0.5f), 100f);
		}

		private void LoadJacket()
		{
			DestroySprite(_localJacket);
			_localJacket = null;
			string path = _mine?.JacketPath;
			if (!string.IsNullOrEmpty(path) && File.Exists(path))
			{
				try
				{
					Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
					if (texture.LoadImage(File.ReadAllBytes(path)))
					{
						_localJacket = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
					}
					else
					{
						Destroy(texture);
					}
				}
				catch (IOException exception)
				{
					Debug.LogWarning($"Result: jacket {path}: {exception.Message}");
				}
			}
			if (_localJacket != null)
			{
				SetJacket(_jacket, _localJacket);
				return;
			}
			string url = CoopSession.Room?.Song?.JacketUrl;
			if (_coop && !string.IsNullOrEmpty(url))
			{
				LoadRemote(_jacket, url);
			}
		}

		private static void SetJacket(Image image, Sprite sprite)
		{
			if (image == null || sprite == null)
			{
				return;
			}
			image.sprite = sprite;
			image.color = Color.white;
		}

		private void LoadRemote(Image image, string url)
		{
			LoadRemoteAsync(image, url).Forget();
		}

		private async UniTaskVoid LoadRemoteAsync(Image image, string url)
		{
			(bool cancelled, Sprite sprite) = await CommunityImageCache.LoadAsync(url, this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
			if (!cancelled && image != null && sprite != null)
			{
				image.sprite = sprite;
				image.color = Color.white;
				AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
				if (fitter != null)
				{
					fitter.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
				}
			}
		}

		/* ---------- pages ---------- */

		private void ShowPage(Page page, bool keepModal = false)
		{
			_page = page;
			DOTween.Kill(this);
			if (!keepModal)
			{
				CloseModal();
			}
			CoopUi.Clear(_pageRoot);
			_cards.Clear();
			_bubbles.Clear();
			_stampPopup = null;
			_statusLine = null;
			_membersShape = null;
			if (page == Page.Members)
			{
				BuildMembers();
			}
			else
			{
				BuildDetail();
			}
		}

		private bool HasDetail => _mine != null;

		private void OnNext()
		{
			if (_leaving)
			{
				return;
			}
			if (_page == Page.Members && HasDetail)
			{
				ShowPage(Page.Detail);
				return;
			}
			if (_coop)
			{
				AskBackToRoom();
				return;
			}
			_leaving = true;
			LiveResultData.Last = null;
			SceneManager.Instance.RequestScene(_mine?.ReturnScreenType == MenuScreenType.CommunityCharts
				? SceneManager.Scene.CommunityCharts
				: SceneManager.Scene.CustomMusicScoreManager);
		}

		private RectTransform BottomButtons(string secondary, UnityEngine.Events.UnityAction onSecondary)
		{
			RectTransform bar = CoopUi.Rect("Buttons", _pageRoot);
			bar.Place(new Vector2(1, 0), new Vector2(-70, 50), new Vector2(980, 104), new Vector2(1, 0));
			Button next = CoopUi.Button(bar, "Next", "次へ", OnNext, new Color32(0x6E, 0xEB, 0xD9, 0xFF), HeaderInk, 40);
			next.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), Vector2.zero, new Vector2(440, 104));
			if (secondary != null)
			{
				Button other = CoopUi.Button(bar, "Secondary", secondary, onSecondary, Color.white, HeaderInk, 34);
				other.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(-470, 0), new Vector2(380, 88));
			}
			return bar;
		}

		/* ---------- page 1: members ---------- */

		private LiveResult Result
		{
			get
			{
				LiveResult result = CoopSession.Room?.Result ?? CoopSession.LastResult;
				return result != null && result.Round == _round ? result : null;
			}
		}

		private sealed class Entry
		{
			public string UserId;
			public string Nickname;
			public string AvatarUrl;
			public int Slot;
			public string ChartId;
			public PlayerStatus Status;
			public Award Award;
			public string Tags;
			public bool Disconnected;
			public bool Finished;
		}

		private List<Entry> Entries(out LiveResult result)
		{
			result = Result;
			List<Entry> entries = new List<Entry>();
			if (result != null)
			{
				foreach (PlayerResult p in result.Players)
				{
					Member member = CoopSession.Find(p.UserId);
					entries.Add(new Entry
					{
						UserId = p.UserId,
						Nickname = p.Nickname,
						AvatarUrl = member?.AvatarUrl,
						Slot = member?.Slot ?? p.Rank - 1,
						ChartId = p.ChartId,
						Status = p.Status ?? new PlayerStatus(),
						Award = p.Award,
						Tags = p.AllPerfect ? "<color=#FF66CC>ALL PERFECT</color>" : p.FullCombo ? "<color=#22AA99>FULL COMBO</color>" : p.Failed ? "<color=#EE5577>LIFE 0</color>" : "CLEAR",
						Disconnected = p.Disconnected,
						Finished = true,
					});
				}
				// The server ranks them; show them in their room order, as the original does.
				entries.Sort((a, b) => a.Slot.CompareTo(b.Slot));
				return entries;
			}
			Room room = CoopSession.Room;
			if (room == null)
			{
				return entries;
			}
			foreach (Member member in room.Members)
			{
				if (!member.Playing && !member.Finished)
				{
					continue;
				}
				MemberStatus status = null;
				if (CoopSession.Statuses != null)
				{
					foreach (MemberStatus s in CoopSession.Statuses.Members)
					{
						if (s.UserId == member.UserId)
						{
							status = s;
						}
					}
				}
				entries.Add(new Entry
				{
					UserId = member.UserId,
					Nickname = member.Nickname,
					AvatarUrl = member.AvatarUrl,
					Slot = member.Slot,
					ChartId = member.ChartId,
					Status = status?.Status ?? new PlayerStatus(),
					Disconnected = !member.Connected,
					Finished = member.Finished || status?.Finished == true,
				});
			}
			entries.Sort((a, b) => a.Slot.CompareTo(b.Slot));
			return entries;
		}

		private void BuildMembers()
		{
			List<Entry> entries = Entries(out LiveResult result);
			_membersShape = Shape(entries, result);
			UpdateMembersHeader(entries, result);

			_statusLine = CoopUi.Text(_pageRoot, "Status", string.Empty, 30, Color.white, true, TextAlignmentOptions.Center);
			_statusLine.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1500, 50), new Vector2(0.5f, 1));
			UpdateStatusLine(result);

			RectTransform viewport = CoopUi.Rect("Cards", _pageRoot).Stretch(0, 76, 0, 180);
			Image hit = viewport.gameObject.AddComponent<Image>();
			hit.color = new Color(1, 1, 1, 0.001f);
			viewport.gameObject.AddComponent<RectMask2D>();
			ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
			scroll.vertical = false;
			scroll.horizontal = true;
			scroll.movementType = ScrollRect.MovementType.Elastic;
			scroll.scrollSensitivity = 40f;
			RectTransform content = CoopUi.Rect("Content", viewport);
			// Centred when the cards fit; otherwise the row starts at its left end.
			content.anchorMin = new Vector2(0.5f, 0);
			content.anchorMax = new Vector2(0.5f, 1);
			content.pivot = new Vector2(0.5f, 0.5f);
			content.offsetMin = Vector2.zero;
			content.offsetMax = Vector2.zero;
			HorizontalLayoutGroup row = CoopUi.Horizontal(content.gameObject, 28, new RectOffset(60, 60, 0, 0));
			row.childAlignment = TextAnchor.MiddleCenter;
			row.childControlHeight = false;
			row.childForceExpandHeight = false;
			ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
			fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			scroll.viewport = viewport;
			scroll.content = content;

			foreach (Entry entry in entries)
			{
				BuildCard(content, entry);
			}
			LayoutRebuilder.ForceRebuildLayoutImmediate(content);
			if (content.rect.width > viewport.rect.width)
			{
				scroll.horizontalNormalizedPosition = 0f;
			}

			Button stamp = CoopUi.Button(_pageRoot, "StampButton", "スタンプ", ToggleStamps, Color.white, HeaderInk, 30);
			stamp.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(70, 58), new Vector2(220, 88), new Vector2(0, 0));
			BottomButtons("メンバーの成績", ShowMemberScores);
		}

		private static string Shape(List<Entry> entries, LiveResult result)
		{
			System.Text.StringBuilder shape = new System.Text.StringBuilder(result != null ? "R" : "W");
			foreach (Entry entry in entries)
			{
				shape.Append('|').Append(entry.UserId).Append(entry.Disconnected ? "x" : "o").Append(entry.Finished ? "f" : "p");
			}
			return shape.ToString();
		}

		private void UpdateMembersHeader(List<Entry> entries, LiveResult result)
		{
			List<(long, Color)> segments = new List<(long, Color)>();
			long sum = 0;
			foreach (Entry entry in entries)
			{
				segments.Add((entry.Status.Score, CoopUi.SlotColor(entry.Slot)));
				sum += entry.Status.Score;
			}
			long team = result != null ? result.TeamScore : sum;
			ChartInfo song = CoopSession.Room?.Song;
			string title = _mine?.Title ?? song?.Title;
			string difficulty = _mine?.Difficulty ?? song?.Difficulty;
			int level = _mine?.PlayLevel ?? song?.PlayLevel ?? 0;
			SetHeader(title, difficulty, level, team, Mathf.Max(1, entries.Count), segments);
		}

		private void UpdateStatusLine(LiveResult result)
		{
			if (_statusLine == null)
			{
				return;
			}
			if (result != null)
			{
				string fever = result.Fever == FeverKind.SuperFever ? "<color=#FF77CC>SUPER FEVER</color>"
					: result.Fever == FeverKind.Fever ? "<color=#FFBB44>FEVER</color>"
					: "<color=#A0A0C0>NO FEVER</color>";
				_statusLine.text = $"TEAM SCORE  <size=40>{result.TeamScore:N0}</size>　　{fever}";
				return;
			}
			_statusLine.text = CoopSession.Room != null ? "等待其他成员结束演奏…" : "<color=#FF8899>已经和房间断开，没能拿到这一局的结果。</color>";
		}

		private void BuildCard(RectTransform parent, Entry entry)
		{
			bool mine = entry.UserId == CoopSession.MyUserId;
			RectTransform slot = CoopUi.Rect("Card " + entry.UserId, parent);
			CoopUi.Size(slot, CardWidth, CardHeight + RibbonSpace);
			slot.sizeDelta = new Vector2(CardWidth, CardHeight + RibbonSpace);

			if (mine)
			{
				Image glow = CoopUi.Image(slot, "Mine", CoopUi.Accent);
				glow.rectTransform.Stretch(-6, RibbonSpace - 6, -6, -6);
			}
			Image card = CoopUi.Image(slot, "Body", new Color32(0xEE, 0xEE, 0xF5, 0xFF));
			card.rectTransform.Stretch(0, RibbonSpace, 0, 0);
			RectTransform body = card.rectTransform;

			RectTransform art = CoopUi.Rect("Art", body);
			art.anchorMin = new Vector2(0, 1);
			art.anchorMax = new Vector2(1, 1);
			art.pivot = new Vector2(0.5f, 1);
			art.offsetMin = new Vector2(8, -CardArtHeight);
			art.offsetMax = new Vector2(-8, -8);
			art.gameObject.AddComponent<RectMask2D>();
			Image artBack = CoopUi.Image(art, "Fill", CoopUi.SlotColor(entry.Slot), rounded: false);
			artBack.rectTransform.Stretch();
			string initial = string.IsNullOrEmpty(entry.Nickname) ? "?" : entry.Nickname.Substring(0, 1);
			TextMeshProUGUI letter = CoopUi.Text(art, "Initial", initial, 180, new Color(1, 1, 1, 0.75f), true, TextAlignmentOptions.Center);
			letter.rectTransform.Stretch(0, 0, 0, 40);
			if (!string.IsNullOrEmpty(entry.AvatarUrl))
			{
				Image avatar = CoopUi.Image(art, "Avatar", new Color(1, 1, 1, 0), rounded: false);
				avatar.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
				avatar.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
				avatar.rectTransform.sizeDelta = new Vector2(CardWidth, CardArtHeight);
				AspectRatioFitter envelope = avatar.gameObject.AddComponent<AspectRatioFitter>();
				envelope.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
				envelope.aspectRatio = 1f;
				LoadRemote(avatar, entry.AvatarUrl);
			}

			Image band = CoopUi.Image(art, "ScoreBand", new Color(1, 1, 1, 0.72f), rounded: false);
			band.rectTransform.anchorMin = new Vector2(0, 0);
			band.rectTransform.anchorMax = new Vector2(1, 0);
			band.rectTransform.pivot = new Vector2(0.5f, 0);
			band.rectTransform.offsetMin = Vector2.zero;
			band.rectTransform.offsetMax = new Vector2(0, 64);
			TextMeshProUGUI score = CoopUi.Text(band.transform, "Score", Digits(entry.Status.Score, 8), 50, new Color32(0x44, 0x44, 0x5E, 0xFF), true, TextAlignmentOptions.Center);
			score.rectTransform.Stretch();

			TextMeshProUGUI name = CoopUi.Text(body, "Name", entry.Nickname, 34, HeaderInk, true, TextAlignmentOptions.Center);
			name.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -CardArtHeight - 10), new Vector2(CardWidth - 30, 48), new Vector2(0.5f, 1));

			ChartInfo chart = FindChart(entry.ChartId);
			if (chart != null)
			{
				Image difficulty = CoopUi.Image(body, "Difficulty", CoopUi.DifficultyColor(chart.Difficulty));
				difficulty.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -CardArtHeight - 64), new Vector2(240, 42), new Vector2(0.5f, 1));
				TextMeshProUGUI label = CoopUi.Text(difficulty.transform, "Label", $"{CoopUi.DifficultyLabel(chart.Difficulty)}  <size=20>Lv.</size>{chart.PlayLevel}", 26, Color.white, true, TextAlignmentOptions.Center);
				label.rectTransform.Stretch();
			}

			PlayerStatus s = entry.Status;
			TextMeshProUGUI state = CoopUi.Text(body, "State", string.Empty, 30, HeaderInk, true, TextAlignmentOptions.Center);
			state.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -CardArtHeight - 112), new Vector2(CardWidth - 30, 42), new Vector2(0.5f, 1));
			TextMeshProUGUI judge = CoopUi.Text(body, "Judge",
				$"<color=#FF77CC>P</color>{s.Perfect} <color=#CC66EE>G</color>{s.Great} <color=#3399EE>G</color>{s.Good} <color=#55BB55>B</color>{s.Bad} <color=#999999>M</color>{s.Miss}\nCOMBO {s.MaxCombo}",
				21, CoopUi.SubInk, true, TextAlignmentOptions.Center);
			judge.overflowMode = TextOverflowModes.Overflow;
			judge.enableAutoSizing = true;
			judge.fontSizeMin = 16;
			judge.fontSizeMax = 21;
			judge.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -CardArtHeight - 158), new Vector2(CardWidth - 30, 64), new Vector2(0.5f, 1));
			judge.gameObject.SetActive(entry.Finished);

			Image stripe = CoopUi.Image(body, "Slot", CoopUi.SlotColor(entry.Slot), rounded: false);
			stripe.rectTransform.Place(new Vector2(0.5f, 0), Vector2.zero, new Vector2(68, 16), new Vector2(0.5f, 0));

			if (entry.Award == Award.Mvp || entry.Award == Award.SuperStar)
			{
				bool star = entry.Award == Award.SuperStar;
				Image ribbon = CoopUi.Image(slot, "Award", star ? new Color32(0xFF, 0xAA, 0x33, 0xFF) : new Color32(0xFF, 0x55, 0x99, 0xFF));
				ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -RibbonSpace + 4), new Vector2(star ? 260 : 200, 60), new Vector2(0.5f, 0.5f));
				TextMeshProUGUI award = CoopUi.Text(ribbon.transform, "Label", star ? "SUPER STAR" : "MVP", 38, Color.white, true, TextAlignmentOptions.Center);
				award.rectTransform.Stretch();
			}

			if (entry.Disconnected)
			{
				Image shade = CoopUi.Image(slot, "Disconnected", new Color32(0x50, 0x4C, 0x70, 0xB0));
				shade.rectTransform.Stretch(0, RibbonSpace, 0, 0);
				TextMeshProUGUI cut = CoopUi.Text(shade.transform, "Label", "切断されました", 38, Color.white, true, TextAlignmentOptions.Center);
				cut.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -CardArtHeight + 70), new Vector2(CardWidth, 60), new Vector2(0.5f, 1));
			}

			CardView view = new CardView { Root = slot, Score = score, State = state };
			_cards[entry.UserId] = view;
			UpdateCard(view, entry);
		}

		private static void UpdateCard(CardView view, Entry entry)
		{
			view.Score.text = Digits(entry.Status.Score, 8);
			view.State.text = entry.Tags ?? (entry.Disconnected ? "<color=#EE5577>切断</color>" : entry.Finished ? "<color=#22AA99>FINISH</color>" : "演奏中…");
		}

		private static ChartInfo FindChart(string chartId)
		{
			Room room = CoopSession.Room;
			if (room == null || string.IsNullOrEmpty(chartId))
			{
				return null;
			}
			foreach (ChartInfo option in room.DifficultyOptions)
			{
				if (option.Id == chartId)
				{
					return option;
				}
			}
			return room.Song != null && room.Song.Id == chartId ? room.Song : null;
		}

		private void RefreshMembers()
		{
			if (_page != Page.Members)
			{
				return;
			}
			List<Entry> entries = Entries(out LiveResult result);
			if (Shape(entries, result) != _membersShape)
			{
				ShowPage(Page.Members, keepModal: true);
				return;
			}
			foreach (Entry entry in entries)
			{
				if (_cards.TryGetValue(entry.UserId, out CardView view))
				{
					UpdateCard(view, entry);
				}
			}
			UpdateMembersHeader(entries, result);
			UpdateStatusLine(result);
		}

		private void ShowMemberScores()
		{
			List<Entry> entries = Entries(out _);
			RectTransform panel = OpenModal(new Vector2(1500, 170 + 86 * Mathf.Max(1, entries.Count)), "メンバーの成績");
			string[] heads = { "", "SCORE", "PERFECT", "GREAT", "GOOD", "BAD", "MISS", "COMBO" };
			float[] xs = { 40, 520, 700, 840, 980, 1110, 1230, 1360 };
			Color[] colors = { HeaderInk, HeaderInk, new Color32(0xFF, 0x66, 0xCC, 0xFF), new Color32(0xCC, 0x55, 0xEE, 0xFF), new Color32(0x33, 0x99, 0xEE, 0xFF), new Color32(0x55, 0xBB, 0x55, 0xFF), CoopUi.SubInk, HeaderInk };
			for (int c = 1; c < heads.Length; c++)
			{
				TextMeshProUGUI head = CoopUi.Text(panel, "Head" + c, heads[c], 22, colors[c], true, TextAlignmentOptions.Right);
				head.rectTransform.Place(new Vector2(0, 1), new Vector2(xs[c] + 100, -100), new Vector2(200, 30), new Vector2(1, 1));
			}
			for (int i = 0; i < entries.Count; i++)
			{
				Entry e = entries[i];
				PlayerStatus s = e.Status;
				float y = -140 - 86 * i;
				Image row = CoopUi.Image(panel, "Row" + i, e.UserId == CoopSession.MyUserId ? new Color32(0xE2, 0xF7, 0xF3, 0xFF) : CoopUi.PanelSoft);
				row.rectTransform.anchorMin = new Vector2(0, 1);
				row.rectTransform.anchorMax = new Vector2(1, 1);
				row.rectTransform.pivot = new Vector2(0.5f, 1);
				row.rectTransform.offsetMin = new Vector2(24, y - 76);
				row.rectTransform.offsetMax = new Vector2(-24, y);
				Image stripe = CoopUi.Image(row.transform, "Slot", CoopUi.SlotColor(e.Slot));
				stripe.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(10, 56));
				string[] cells = { e.Nickname, s.Score.ToString("N0"), s.Perfect.ToString(), s.Great.ToString(), s.Good.ToString(), s.Bad.ToString(), s.Miss.ToString(), s.MaxCombo.ToString() };
				for (int c = 0; c < cells.Length; c++)
				{
					TextMeshProUGUI cell = CoopUi.Text(row.transform, "Cell" + c, cells[c], c == 0 ? 30 : 28, HeaderInk, true, c == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
					cell.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(c == 0 ? xs[c] - 24 + 10 : xs[c] + 100 - 24, 0), new Vector2(c == 0 ? 420 : 200, 50), new Vector2(c == 0 ? 0 : 1, 0.5f));
				}
			}
		}

		private void ToggleStamps()
		{
			if (_stampPopup != null)
			{
				Destroy(_stampPopup.gameObject);
				_stampPopup = null;
				return;
			}
			Image back = CoopUi.Image(_pageRoot, "StampPopup", new Color(0.15f, 0.13f, 0.28f, 0.92f));
			_stampPopup = back.rectTransform;
			_stampPopup.anchorMin = new Vector2(0, 0);
			_stampPopup.anchorMax = new Vector2(1, 0);
			_stampPopup.pivot = new Vector2(0.5f, 0);
			_stampPopup.offsetMin = new Vector2(60, 160);
			_stampPopup.offsetMax = new Vector2(-60, 280);
			RectTransform bar = CoopUi.StampBar(_stampPopup);
			bar.offsetMin = new Vector2(20, 20);
			bar.offsetMax = new Vector2(-20, 100);
			foreach (Button button in bar.GetComponentsInChildren<Button>())
			{
				button.onClick.AddListener(ToggleStamps);
			}
		}

		/* ---------- page 2: this player's detail ---------- */

		private void BuildDetail()
		{
			LiveResultData data = _mine;
			if (data == null)
			{
				return;
			}
			LiveScore score = data.Score;
			SetHeader(data.Title, data.Difficulty, data.PlayLevel, score.totalScore, 1, null);

			RectTransform left = CoopUi.Rect("Detail", _pageRoot);
			left.Place(new Vector2(0, 0.5f), new Vector2(230, -10), new Vector2(880, 760), new Vector2(0, 0.5f));

			TextMeshProUGUI scoreLabel = CoopUi.Text(left, "ScoreLabel", "スコア", 68, Color.white, true);
			scoreLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, -30), new Vector2(220, 100), new Vector2(0, 1));
			TextMeshProUGUI scoreValue = CoopUi.Text(left, "Score", Digits(0, 8, ScorePink), 124, ScorePink, true, TextAlignmentOptions.Right);
			scoreValue.rectTransform.Place(new Vector2(1, 1), new Vector2(0, 0), new Vector2(680, 140), new Vector2(1, 1));
			scoreValue.characterSpacing = 2;
			DOVirtual.Int(0, score.totalScore, 1.0f, v => scoreValue.text = Digits(v, 8, ScorePink)).SetEase(Ease.OutCubic).SetTarget(this);

			TextMeshProUGUI highLabel = CoopUi.Text(left, "HighLabel", "ハイスコア", 38, Color.white, true);
			highLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, -160), new Vector2(300, 60), new Vector2(0, 1));
			TextMeshProUGUI highValue = CoopUi.Text(left, "HighScore", data.HighScore > 0 ? Digits(data.HighScore, 8) : "--------", 64, Color.white, true, TextAlignmentOptions.Right);
			highValue.rectTransform.Place(new Vector2(1, 1), new Vector2(0, -146), new Vector2(560, 80), new Vector2(1, 1));
			if (data.IsNewRecord)
			{
				Image badge = CoopUi.Image(left, "NewRecord", new Color32(0xFF, 0x55, 0x99, 0xFF));
				badge.rectTransform.Place(new Vector2(0, 1), new Vector2(230, -160), new Vector2(170, 44), new Vector2(0, 1));
				TextMeshProUGUI label = CoopUi.Text(badge.transform, "Label", "NEW RECORD", 22, Color.white, true, TextAlignmentOptions.Center);
				label.rectTransform.Stretch();
			}

			// Judgements
			RectTransform table = CoopUi.Rect("Judgements", left);
			table.Place(new Vector2(0, 1), new Vector2(-20, -290), new Vector2(400, 340), new Vector2(0, 1));
			(string name, int count, VertexGradient gradient)[] rows =
			{
				("PERFECT", score.perfectCount, new VertexGradient(new Color32(0xFF, 0x99, 0xEE, 0xFF), new Color32(0x88, 0xEE, 0xFF, 0xFF), new Color32(0xFF, 0xEE, 0x99, 0xFF), new Color32(0x99, 0xFF, 0xCC, 0xFF))),
				("GREAT", score.greatCount, Solid(new Color32(0xF0, 0x88, 0xEE, 0xFF))),
				("GOOD", score.goodCount, Solid(new Color32(0x77, 0xCC, 0xFF, 0xFF))),
				("BAD", score.badCount, Solid(new Color32(0x88, 0xE0, 0x77, 0xFF))),
				("MISS", score.missCount, Solid(new Color32(0xD0, 0xD0, 0xDC, 0xFF))),
			};
			for (int i = 0; i < rows.Length; i++)
			{
				float y = -68 * i;
				if (i % 2 == 0)
				{
					Image shade = CoopUi.Image(table, "Shade" + i, RowShade);
					shade.rectTransform.Place(new Vector2(0, 1), new Vector2(0, y), new Vector2(400, 62), new Vector2(0, 1));
				}
				TextMeshProUGUI name = CoopUi.Text(table, rows[i].name, rows[i].name, 40, Color.white, true);
				name.enableVertexGradient = true;
				name.colorGradient = rows[i].gradient;
				name.rectTransform.Place(new Vector2(0, 1), new Vector2(26, y - 4), new Vector2(240, 56), new Vector2(0, 1));
				TextMeshProUGUI count = CoopUi.Text(table, rows[i].name + "Count", Digits(rows[i].count, 4), 40, Color.white, false, TextAlignmentOptions.Right);
				count.rectTransform.Place(new Vector2(0, 1), new Vector2(380, y - 4), new Vector2(160, 56), new Vector2(1, 1));
			}

			// Combo
			TextMeshProUGUI comboLabel = CoopUi.Text(left, "ComboLabel", "COMBO", 38, Color.white, true);
			comboLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(440, -310), new Vector2(170, 60), new Vector2(0, 1));
			TextMeshProUGUI combo = CoopUi.Text(left, "Combo", Digits(score.maxCombo, 4), 76, Color.white, true, TextAlignmentOptions.Right);
			combo.rectTransform.Place(new Vector2(1, 1), new Vector2(0, -290), new Vector2(260, 90), new Vector2(1, 1));

			// FAST / LATE
			Image timing = CoopUi.Image(left, "Timing", new Color32(0x30, 0x2C, 0x55, 0xC8));
			timing.rectTransform.Place(new Vector2(0, 1), new Vector2(440, -420), new Vector2(440, 210), new Vector2(0, 1));
			TextMeshProUGUI late = CoopUi.Text(timing.transform, "Late", "LATE", 32, new Color32(0xFF, 0x55, 0x88, 0xFF), true);
			late.rectTransform.Place(new Vector2(0, 1), new Vector2(40, -18), new Vector2(160, 44), new Vector2(0, 1));
			TextMeshProUGUI fast = CoopUi.Text(timing.transform, "Fast", "FAST", 32, new Color32(0x44, 0x99, 0xFF, 0xFF), true, TextAlignmentOptions.Right);
			fast.rectTransform.Place(new Vector2(1, 1), new Vector2(-40, -18), new Vector2(160, 44), new Vector2(1, 1));
			RectTransform split = CoopUi.Rect("Split", timing.transform);
			split.Place(new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(360, 44), new Vector2(0.5f, 1));
			int total = score.lateCount + score.fastCount;
			float lateShare = total > 0 ? (float)score.lateCount / total : 0.5f;
			Image lateBar = CoopUi.Image(split, "LateBar", new Color32(0xFF, 0x44, 0x77, 0xFF));
			lateBar.rectTransform.anchorMin = Vector2.zero;
			lateBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp(lateShare, 0.12f, 0.88f), 1);
			lateBar.rectTransform.offsetMin = Vector2.zero;
			lateBar.rectTransform.offsetMax = Vector2.zero;
			Image fastBar = CoopUi.Image(split, "FastBar", new Color32(0x44, 0x99, 0xFF, 0xFF));
			fastBar.rectTransform.anchorMin = new Vector2(Mathf.Clamp(lateShare, 0.12f, 0.88f), 0);
			fastBar.rectTransform.anchorMax = Vector2.one;
			fastBar.rectTransform.offsetMin = Vector2.zero;
			fastBar.rectTransform.offsetMax = Vector2.zero;
			TextMeshProUGUI lateCount = CoopUi.Text(split, "LateCount", score.lateCount.ToString(), 30, Color.white, true);
			lateCount.rectTransform.Stretch(14, 0, 0, 0);
			TextMeshProUGUI fastCount = CoopUi.Text(split, "FastCount", score.fastCount.ToString(), 30, Color.white, true, TextAlignmentOptions.Right);
			fastCount.rectTransform.Stretch(0, 0, 14, 0);
			TextMeshProUGUI flick = CoopUi.Text(timing.transform, "Flick", "<color=#FFCC33>! FLICK</color>", 30, Color.white, true);
			flick.rectTransform.Place(new Vector2(0, 0), new Vector2(40, 22), new Vector2(200, 50), new Vector2(0, 0));
			TextMeshProUGUI flickCount = CoopUi.Text(timing.transform, "FlickCount", score.flickCount.ToString(), 30, Color.white, true, TextAlignmentOptions.Right);
			flickCount.rectTransform.Place(new Vector2(1, 0), new Vector2(-40, 22), new Vector2(160, 50), new Vector2(1, 0));

			BuildDetailArt(data);
			if (_coop)
			{
				BottomButtons("メンバーの成績", () => ShowPage(Page.Members));
			}
			else
			{
				BottomButtons(null, null);
			}
		}

		/// <summary>The right half: the jacket (the original has the leader character there) and how the live ended.</summary>
		private void BuildDetailArt(LiveResultData data)
		{
			RectTransform art = CoopUi.Rect("Art", _pageRoot);
			art.Place(new Vector2(1, 0.5f), new Vector2(-220, 40), new Vector2(560, 640), new Vector2(1, 0.5f));
			art.localEulerAngles = new Vector3(0, 0, -4);
			Image shadow = CoopUi.Image(art, "Shadow", new Color(0, 0, 0, 0.35f));
			shadow.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(14, -14), new Vector2(520, 520), new Vector2(0.5f, 1));
			Image frame = CoopUi.Image(art, "Frame", Color.white);
			frame.rectTransform.Place(new Vector2(0.5f, 1), Vector2.zero, new Vector2(520, 520), new Vector2(0.5f, 1));
			Image jacket = CoopUi.Image(frame.transform, "Jacket", new Color32(0x55, 0x50, 0x80, 0xFF), rounded: false);
			jacket.rectTransform.Stretch(14, 14, 14, 14);
			if (_jacket.sprite != null)
			{
				SetJacket(jacket, _jacket.sprite);
			}

			string state = data.IsAuto ? "AUTO LIVE"
				: data.Failed ? "LIVE FAILED"
				: data.Score.totalComboCount > 0 && data.Score.IsAllPerfect && data.Score.perfectCount >= data.Score.totalComboCount ? "ALL PERFECT"
				: data.Score.totalComboCount > 0 && data.Score.maxCombo >= data.Score.totalComboCount ? "FULL COMBO"
				: "LIVE CLEAR";
			Color color = data.IsAuto ? new Color32(0x99, 0x99, 0xBB, 0xFF)
				: data.Failed ? CoopUi.Danger
				: state == "ALL PERFECT" ? new Color32(0xFF, 0x66, 0xCC, 0xFF)
				: state == "FULL COMBO" ? new Color32(0xFF, 0xAA, 0x33, 0xFF)
				: CoopUi.Accent;
			Image ribbon = CoopUi.Image(art, "State", color);
			ribbon.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(420, 84), new Vector2(0.5f, 0));
			TextMeshProUGUI label = CoopUi.Text(ribbon.transform, "Label", state, 44, Color.white, true, TextAlignmentOptions.Center);
			label.rectTransform.Stretch();
		}

		private static VertexGradient Solid(Color color) => new VertexGradient(color, color, color, color);

		/// <summary>Zero-pads like the original: the padding zeros are dimmed.</summary>
		private static string Digits(long value, int digits, Color? color = null)
		{
			string text = System.Math.Max(0, value).ToString();
			string colored = color.HasValue ? $"<color=#{UnityEngine.ColorUtility.ToHtmlStringRGB(color.Value)}>{text}</color>" : text;
			return text.Length >= digits ? colored : $"<color={DimHex}>{new string('0', digits - text.Length)}</color>{colored}";
		}

		/* ---------- dialogs ---------- */

		private RectTransform OpenModal(Vector2 size, string title)
		{
			CloseModal();
			Image shade = CoopUi.Image(_root, "Modal", new Color(0.05f, 0.04f, 0.12f, 0.6f), rounded: false);
			shade.rectTransform.Stretch();
			Button close = shade.gameObject.AddComponent<Button>();
			close.transition = Selectable.Transition.None;
			close.onClick.AddListener(CloseModal);
			_modal = shade.rectTransform;
			Image panel = CoopUi.Image(shade.transform, "Panel", CoopUi.Panel);
			panel.raycastTarget = true;
			panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, size);
			TextMeshProUGUI head = CoopUi.Text(panel.transform, "Title", title, 40, HeaderInk, true, TextAlignmentOptions.Center);
			head.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(size.x - 80, 60), new Vector2(0.5f, 1));
			return panel.rectTransform;
		}

		private void CloseModal()
		{
			if (_modal != null)
			{
				Destroy(_modal.gameObject);
				_modal = null;
			}
		}

		private void AskBackToRoom()
		{
			bool inRoom = CoopSession.Room != null;
			RectTransform panel = OpenModal(new Vector2(960, 420), inRoom ? "要回到房间吗？" : "已经和房间断开");
			TextMeshProUGUI body = CoopUi.Text(panel, "Body", inRoom ? "回到房间后可以和大家继续下一首。" : "回到協力ライブ，重新加入或开个新房间。", 30, CoopUi.SubInk, false, TextAlignmentOptions.Center);
			body.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(860, 100), new Vector2(0.5f, 1));
			body.textWrappingMode = TextWrappingModes.Normal;
			Button leave = CoopUi.Button(panel, "Leave", inRoom ? "退出房间" : "回到首页", LeaveToHome, CoopUi.PanelSoft, HeaderInk, 32);
			leave.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(-200, 40), new Vector2(360, 96), new Vector2(0.5f, 0));
			Button back = CoopUi.Button(panel, "Back", inRoom ? "回到房间" : "協力ライブ", BackToRoom, CoopUi.Accent, Color.white, 32);
			back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(200, 40), new Vector2(360, 96), new Vector2(0.5f, 0));
		}

		private void LeaveToHome()
		{
			if (_leaving)
			{
				return;
			}
			_leaving = true;
			LeaveToHomeAsync().Forget();
		}

		private async UniTaskVoid LeaveToHomeAsync()
		{
			CloseModal();
			if (CoopSession.Room != null)
			{
				await CoopSession.LeaveAsync();
			}
			LiveResultData.Last = null;
			if (this != null)
			{
				SceneManager.Instance.RequestScene(SceneManager.Scene.Home);
			}
		}

		private void BackToRoom()
		{
			if (_leaving)
			{
				return;
			}
			_leaving = true;
			CloseModal();
			SceneManager.Instance.RequestScene(SceneManager.Scene.CoopLive);
		}

		/* ---------- co-op events ---------- */

		private void OnRoom(Room room)
		{
			// The room started its next round while this player was still here.
			if (room.Round > _round && room.Phase != Phase.Lobby && room.Phase != Phase.Result)
			{
				BackToRoom();
				return;
			}
			RefreshMembers();
		}

		private void OnStatuses(LiveStatuses statuses)
		{
			if (Result == null)
			{
				RefreshMembers();
			}
		}

		private void OnClosed(string code, string message)
		{
			if (_leaving)
			{
				return;
			}
			if (code != "LEFT")
			{
				Toast(message);
			}
			RefreshMembers();
		}

		private void OnStamp(StampSent stamp)
		{
			string text = CoopStamps.Text(stamp.StampId);
			if (_cards.TryGetValue(stamp.UserId, out CardView card) && card.Root != null)
			{
				Image bubble = CoopUi.Image(card.Root, "Stamp", new Color32(0xFF, 0xF5, 0xC8, 0xFF));
				bubble.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -RibbonSpace - 110), new Vector2(CardWidth - 30, 72));
				TextMeshProUGUI label = CoopUi.Text(bubble.transform, "Text", text, 28, CoopUi.Ink, true, TextAlignmentOptions.Center);
				label.rectTransform.Stretch(8, 0, 8, 0);
				_bubbles.Add((bubble.gameObject, Time.unscaledTime + 2.5f));
				return;
			}
			Member member = CoopSession.Find(stamp.UserId);
			Toast($"{member?.Nickname ?? "?"}：{text}");
		}

		private void Update()
		{
			if (_toast.transform.parent.gameObject.activeSelf && Time.unscaledTime > _toastUntil)
			{
				_toast.transform.parent.gameObject.SetActive(false);
			}
			for (int i = _bubbles.Count - 1; i >= 0; i--)
			{
				if (_bubbles[i].bubble == null || Time.unscaledTime > _bubbles[i].until)
				{
					if (_bubbles[i].bubble != null)
					{
						Destroy(_bubbles[i].bubble);
					}
					_bubbles.RemoveAt(i);
				}
			}
		}

		private void Toast(string message)
		{
			if (string.IsNullOrEmpty(message))
			{
				return;
			}
			_toast.text = message;
			_toast.transform.parent.gameObject.SetActive(true);
			_toast.transform.parent.SetAsLastSibling();
			_toastUntil = Time.unscaledTime + 3f;
		}
	}
}
