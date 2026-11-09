using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sekai.Community;
using Sekai.CoopLive.Proto;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.CoopLive
{
	/// <summary>
	/// 協力ライブ: the room screen of the CoopLive scene. Without a room it offers フリールーム (quick match),
	/// ルーム作成 (a private room) and joining by the 5-digit ルームID. In a room it shows the five member slots
	/// and the current phase: waiting (the host's メンバー決定), song select (a community chart or おまかせ, 20 s),
	/// the draw, difficulty (10 s, 準備完了), loading, and the result after the live. スタンプ are the chat.
	/// The result of a live is its own scene (<see cref="ScreenLayerCoopResult"/>).
	/// The layout is built in code because it changes with every phase; colors and sprites follow the menus.
	/// </summary>
	public sealed class ScreenLayerCoopLive : ScreenLayer
	{
		[SerializeField]
		private Sprite roundSprite;

		[SerializeField]
		private Sprite backgroundSprite;

		private RectTransform _root;
		private TextMeshProUGUI _title;
		private TextMeshProUGUI _roomCode;
		private RectTransform _members;
		private RectTransform _main;
		private RectTransform _stampBar;
		private TextMeshProUGUI _toast;
		private float _toastUntil;

		private TextMeshProUGUI _countdown;
		private TextMeshProUGUI _status;
		private readonly List<Button> _hostButtons = new List<Button>();

		private Phase _builtPhase = Phase.Unspecified;
		private int _builtRound = -1;
		private bool _busy;
		private bool _leaving;
		private string _myPick;

		// Song select.
		private TMP_InputField _search;
		private RectTransform _chartList;
		private TextMeshProUGUI _pickLabel;
		private int _listRequest;
		private readonly Dictionary<string, Image> _chartRows = new Dictionary<string, Image>();

		private readonly Dictionary<string, (RectTransform bubble, float until)> _bubbles = new Dictionary<string, (RectTransform, float)>();
		private CancellationTokenSource _cts;

		protected override void Awake()
		{
			base.Awake();
			CoopUi.RoundSprite = roundSprite;
			BuildFrame();
		}

		protected override void OnScreenStart()
		{
			_leaving = false;
			_cts?.Cancel();
			_cts = new CancellationTokenSource();
			CoopSession.RoomChanged += OnRoom;
			CoopSession.NoticeReceived += OnNotice;
			CoopSession.StampReceived += OnStamp;
			CoopSession.Closed += OnClosed;
			CoopRoundLauncher.StatusChanged += OnLaunchStatus;
			Render(force: true);
		}

		private void OnDestroy()
		{
			_cts?.Cancel();
			CoopSession.RoomChanged -= OnRoom;
			CoopSession.NoticeReceived -= OnNotice;
			CoopSession.StampReceived -= OnStamp;
			CoopSession.Closed -= OnClosed;
			CoopRoundLauncher.StatusChanged -= OnLaunchStatus;
		}

		private CancellationToken Token => _cts?.Token ?? CancellationToken.None;

		/* ---------- frame ---------- */

		private void BuildFrame()
		{
			_root = CoopUi.Frame(transform, backgroundSprite, OnBack, out _title, out _roomCode);

			_members = CoopUi.Rect("Members", _root);
			_members.anchorMin = new Vector2(0, 1);
			_members.anchorMax = new Vector2(1, 1);
			_members.pivot = new Vector2(0.5f, 1);
			_members.offsetMin = new Vector2(24, -372);
			_members.offsetMax = new Vector2(-24, -152);
			HorizontalLayoutGroup row = CoopUi.Horizontal(_members.gameObject, 16);
			row.childForceExpandWidth = true;

			_main = CoopUi.Rect("Main", _root);
			_main.anchorMin = Vector2.zero;
			_main.anchorMax = Vector2.one;
			_main.offsetMin = new Vector2(24, 120);
			_main.offsetMax = new Vector2(-24, -392);

			_stampBar = CoopUi.StampBar(_root);
			_toast = CoopUi.Toast(_root);
		}

		/* ---------- rendering ---------- */

		private void OnRoom(Room room)
		{
			CoopRoundLauncher.OnRoom(room, Token);
			Render(force: false);
		}

		private void Render(bool force)
		{
			Room room = CoopSession.Room;
			bool inRoom = room != null;
			_members.gameObject.SetActive(inRoom);
			_stampBar.gameObject.SetActive(inRoom);
			_roomCode.text = inRoom ? $"{(room.Public ? "公开" : "私人")}房间  ID <size=44>{room.Code}</size>" : string.Empty;
			_title.text = "協力ライブ";
			_main.offsetMax = new Vector2(-24, inRoom ? -392 : -160);

			if (!inRoom)
			{
				if (force || _builtPhase != Phase.Unspecified)
				{
					_builtPhase = Phase.Unspecified;
					BuildEntrance();
				}
				return;
			}

			RenderMembers(room);
			if (force || room.Phase != _builtPhase || room.Round != _builtRound)
			{
				_builtPhase = room.Phase;
				_builtRound = room.Round;
				BuildPhase(room);
			}
			else
			{
				RefreshPhase(room);
			}
		}

		private void Update()
		{
			if (_countdown != null && CoopSession.Room != null)
			{
				long ms = CoopSession.RemainingMs;
				_countdown.text = ms >= 0 ? CoopUi.FormatSeconds(ms) : string.Empty;
			}
			if (_toast != null && _toast.transform.parent.gameObject.activeSelf && Time.unscaledTime > _toastUntil)
			{
				_toast.transform.parent.gameObject.SetActive(false);
			}
			if (_bubbles.Count > 0)
			{
				List<string> expired = null;
				foreach (var pair in _bubbles)
				{
					if (Time.unscaledTime > pair.Value.until)
					{
						(expired ??= new List<string>()).Add(pair.Key);
					}
				}
				if (expired != null)
				{
					foreach (string key in expired)
					{
						if (_bubbles[key].bubble != null)
						{
							Destroy(_bubbles[key].bubble.gameObject);
						}
						_bubbles.Remove(key);
					}
				}
			}
		}

		private void RenderMembers(Room room)
		{
			CoopUi.Clear(_members);
			_bubbles.Clear();
			int max = Mathf.Max(room.MaxMembers, room.Members.Count);
			for (int i = 0; i < max; i++)
			{
				Member member = i < room.Members.Count ? room.Members[i] : null;
				Image card = CoopUi.Image(_members, member != null ? "Member " + member.Slot : "Empty", member != null ? CoopUi.Panel : new Color(1, 1, 1, 0.45f));
				if (member == null)
				{
					TextMeshProUGUI empty = CoopUi.Text(card.transform, "Empty", "募集中…", 28, CoopUi.SubInk, true, TextAlignmentOptions.Center);
					empty.rectTransform.Stretch();
					continue;
				}
				Image stripe = CoopUi.Image(card.transform, "Slot", CoopUi.SlotColor(member.Slot));
				stripe.rectTransform.anchorMin = new Vector2(0, 1);
				stripe.rectTransform.anchorMax = new Vector2(1, 1);
				stripe.rectTransform.pivot = new Vector2(0.5f, 1);
				stripe.rectTransform.offsetMin = new Vector2(0, -14);
				stripe.rectTransform.offsetMax = Vector2.zero;

				string badges = (member.UserId == room.HostId ? "<color=#FFAA22>★ 房主</color>  " : string.Empty)
					+ (member.UserId == CoopSession.MyUserId ? "<color=#33BBAA>你</color>" : string.Empty);
				TextMeshProUGUI badge = CoopUi.Text(card.transform, "Badges", badges, 22, CoopUi.SubInk, true);
				badge.rectTransform.Place(new Vector2(0, 1), new Vector2(22, -30), new Vector2(320, 30), new Vector2(0, 1));
				TextMeshProUGUI name = CoopUi.Text(card.transform, "Name", member.Nickname, 34, CoopUi.Ink, true);
				name.rectTransform.anchorMin = new Vector2(0, 0.5f);
				name.rectTransform.anchorMax = new Vector2(1, 0.5f);
				name.rectTransform.offsetMin = new Vector2(22, -30);
				name.rectTransform.offsetMax = new Vector2(-22, 30);
				TextMeshProUGUI state = CoopUi.Text(card.transform, "State", MemberState(room, member), 24, member.Connected ? CoopUi.AccentDark : CoopUi.Danger, true);
				state.rectTransform.Place(new Vector2(0, 0), new Vector2(22, 18), new Vector2(320, 34), new Vector2(0, 0));

				if (room.Phase == Phase.Lobby && CoopSession.IsHost && member.UserId != CoopSession.MyUserId)
				{
					string target = member.UserId;
					Button kick = CoopUi.Button(card.transform, "Kick", "移出", () => CoopSession.Kick(target), CoopUi.PanelSoft, CoopUi.Danger, 22);
					kick.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(-14, 14), new Vector2(96, 46), new Vector2(1, 0));
				}
			}
		}

		private static string MemberState(Room room, Member m)
		{
			if (!m.Connected)
			{
				return "连接中断…";
			}
			switch (room.Phase)
			{
				case Phase.SongSelect:
					return m.Picked ? "已选曲" : "选曲中…";
				case Phase.SongDraw:
				case Phase.Difficulty:
					return m.Ready ? "準備完了" : "选择难度中…";
				case Phase.Loading:
					return !m.Playing ? "未参加" : m.Loaded ? "加载完成" : "加载中…";
				case Phase.Playing:
					return !m.Playing ? "观战" : m.Finished ? "FINISH" : "演奏中";
				default:
					return "等待中";
			}
		}

		private void BuildPhase(Room room)
		{
			CoopUi.Clear(_main);
			_countdown = null;
			_status = null;
			_hostButtons.Clear();
			_chartRows.Clear();
			_chartList = null;
			_pickLabel = null;
			_search = null;
			switch (room.Phase)
			{
				case Phase.Lobby:
				case Phase.Result:
					BuildLobby(room);
					break;
				case Phase.SongSelect:
					_myPick = null;
					BuildSongSelect(room);
					break;
				case Phase.SongDraw:
				case Phase.Difficulty:
					BuildDifficulty(room);
					break;
				case Phase.Loading:
				case Phase.Playing:
					BuildLoading(room);
					break;
			}
		}

		private void RefreshPhase(Room room)
		{
			switch (room.Phase)
			{
				case Phase.Lobby:
				case Phase.Result:
					// The host and the countdown may have changed.
					BuildPhase(room);
					break;
				case Phase.SongDraw:
				case Phase.Difficulty:
					BuildPhase(room);
					break;
				case Phase.Loading:
				case Phase.Playing:
					if (_status != null && !CoopRoundLauncher.IsLaunching)
					{
						_status.text = LoadingText(room);
					}
					break;
			}
		}

		private RectTransform Card(string name, Transform parent = null)
		{
			Image card = CoopUi.Image(parent ?? _main, name, CoopUi.Panel);
			return card.rectTransform;
		}

		private TextMeshProUGUI Countdown(Transform parent)
		{
			_countdown = CoopUi.Text(parent, "Countdown", string.Empty, 64, CoopUi.AccentDark, true, TextAlignmentOptions.Right);
			_countdown.rectTransform.Place(new Vector2(1, 1), new Vector2(-30, -20), new Vector2(200, 80), new Vector2(1, 1));
			return _countdown;
		}

		/* ---------- entrance ---------- */

		private void BuildEntrance()
		{
			CoopUi.Clear(_main);
			_countdown = null;
			RectTransform card = Card("Entrance");
			card.Stretch(260, 60, 260, 60);

			TextMeshProUGUI lead = CoopUi.Text(card, "Lead", "和其他玩家一起演奏社区谱面（最多 5 人）", 32, CoopUi.Ink, true, TextAlignmentOptions.Center);
			lead.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1200, 60));

			if (!CommunityAccount.IsSignedIn)
			{
				TextMeshProUGUI need = CoopUi.Text(card, "SignIn", "需要登录 OurTaiko 账号（设置 › Additionals › アカウント）", 28, CoopUi.SubInk, false, TextAlignmentOptions.Center);
				need.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1200, 60));
				Button settings = CoopUi.Button(card, "Settings", "打开设置", () =>
				{
					ScreenManager.Instance?.ShowDialog<OptionDialog>(DialogType.OptionDialog);
				});
				settings.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(360, 90));
				CommunityAccount.Changed -= OnAccountChanged;
				CommunityAccount.Changed += OnAccountChanged;
				return;
			}

			Button quick = CoopUi.Button(card, "Quick", "フリールーム\n<size=22>加入公开房间</size>", () => Connect(CoopSession.QuickMatchAsync), CoopUi.Accent, Color.white, 40);
			quick.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(-330, 60), new Vector2(560, 170));
			Button create = CoopUi.Button(card, "Create", "ルーム作成\n<size=22>创建私人房间</size>", () => Connect(CoopSession.CreatePrivateAsync), new Color32(0x66, 0x88, 0xEE, 0xFF), Color.white, 40);
			create.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(330, 60), new Vector2(560, 170));

			TMP_InputField code = CoopUi.Input(card, "Code", "输入 5 位ルームID", TMP_InputField.ContentType.IntegerNumber, 5);
			code.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(-150, -130), new Vector2(560, 90));
			Button join = CoopUi.Button(card, "Join", "参加", () =>
			{
				string value = code.text.Trim();
				if (value.Length != 5)
				{
					Toast("ルームID 是 5 位数字。");
					return;
				}
				Connect(ct => CoopSession.JoinAsync(value, ct));
			}, CoopUi.AccentDark, Color.white, 34);
			join.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(290, -130), new Vector2(260, 90));

			_status = CoopUi.Text(card, "Status", CoopTransport.HasHttp2 ? string.Empty : "（这个版本缺少 HTTP/2 支持，无法联机）", 26, CoopUi.SubInk, false, TextAlignmentOptions.Center);
			_status.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1200, 50));
		}

		private void OnAccountChanged()
		{
			if (this != null && CoopSession.Room == null)
			{
				Render(force: true);
			}
		}

		private void Connect(Func<CancellationToken, UniTask> connect)
		{
			if (_busy)
			{
				return;
			}
			ConnectAsync(connect).Forget();
		}

		private async UniTaskVoid ConnectAsync(Func<CancellationToken, UniTask> connect)
		{
			_busy = true;
			if (_status != null)
			{
				_status.text = "连接中…";
			}
			try
			{
				await connect(Token);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (CoopException exception)
			{
				Fail(exception.Message);
			}
			catch (CommunityException exception)
			{
				Fail(exception.Message);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				Fail("无法连接协力ライブ服务器。");
			}
			finally
			{
				_busy = false;
			}
			if (this != null)
			{
				Render(force: true);
			}

			void Fail(string message)
			{
				if (_status != null)
				{
					_status.text = message;
				}
				Toast(message);
			}
		}

		/* ---------- lobby ---------- */

		private void BuildLobby(Room room)
		{
			RectTransform card = Card("Lobby");
			card.Stretch();
			int present = 0;
			foreach (Member m in room.Members)
			{
				present += m.Connected ? 1 : 0;
			}
			string info = room.Public
				? "フリールーム：凑够 2 人后 30 秒自动开始，也可以由房主提前开始。"
				: $"私人房间：把ルームID <b>{room.Code}</b> 告诉朋友。人齐后由房主按「メンバー決定」。";
			TextMeshProUGUI lead = CoopUi.Text(card, "Info", info, 30, CoopUi.Ink, true);
			lead.rectTransform.Place(new Vector2(0, 1), new Vector2(40, -40), new Vector2(1500, 60), new Vector2(0, 1));
			lead.textWrappingMode = TextWrappingModes.Normal;
			Countdown(card);

			string who = CoopSession.IsHost ? "你是房主。" : "等待房主开始…";
			_status = CoopUi.Text(card, "Status", $"{present} / {room.MaxMembers} 人　{who}", 28, CoopUi.SubInk);
			_status.rectTransform.Place(new Vector2(0, 1), new Vector2(40, -120), new Vector2(1200, 50), new Vector2(0, 1));

			if (CoopSession.IsHost)
			{
				Button lockMembers = CoopUi.Button(card, "Lock", "メンバー決定", CoopSession.LockMembers, CoopUi.Accent, Color.white, 38);
				lockMembers.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(520, 110));
				lockMembers.interactable = present >= 2;
				_hostButtons.Add(lockMembers);
				if (!room.Public)
				{
					Button open = CoopUi.Button(card, "Open", "ルーム解放（公开）", CoopSession.OpenRoom, CoopUi.PanelSoft, CoopUi.Ink, 26);
					open.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(-40, 50), new Vector2(330, 80), new Vector2(1, 0));
				}
			}
			if (room.Result != null && room.Result.Round > 0)
			{
				int round = room.Result.Round;
				Button last = CoopUi.Button(card, "LastResult", "上一局结果", () => CoopResultSceneArgs.Open(round), CoopUi.PanelSoft, CoopUi.Ink, 26);
				last.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(40, 50), new Vector2(260, 80), new Vector2(0, 0));
			}
		}

		/* ---------- song select ---------- */

		private void BuildSongSelect(Room room)
		{
			RectTransform listCard = Card("Charts");
			listCard.anchorMin = new Vector2(0, 0);
			listCard.anchorMax = new Vector2(0.64f, 1);
			listCard.offsetMin = Vector2.zero;
			listCard.offsetMax = new Vector2(-10, 0);

			_search = CoopUi.Input(listCard, "Search", "搜索曲名 / 作者 / #编号", TMP_InputField.ContentType.Standard, 60);
			_search.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(24, -20), new Vector2(760, 76), new Vector2(0, 1));
			_search.onSubmit.AddListener(_ => LoadChartsAsync().Forget());
			Button go = CoopUi.Button(listCard, "Go", "搜索", () => LoadChartsAsync().Forget(), CoopUi.AccentDark, Color.white, 28);
			go.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(800, -20), new Vector2(160, 76), new Vector2(0, 1));

			RectTransform list = CoopUi.ScrollList(listCard, "List", 8, out _);
			list.parent.GetComponent<RectTransform>().Stretch(20, 110, 20, 20);
			_chartList = list;

			RectTransform side = Card("Pick");
			side.anchorMin = new Vector2(0.64f, 0);
			side.anchorMax = new Vector2(1, 1);
			side.offsetMin = new Vector2(10, 0);
			side.offsetMax = Vector2.zero;
			TextMeshProUGUI title = CoopUi.Text(side, "Title", "楽曲選択", 40, CoopUi.Ink, true);
			title.rectTransform.Place(new Vector2(0, 1), new Vector2(30, -30), new Vector2(400, 60), new Vector2(0, 1));
			Countdown(side);
			TextMeshProUGUI hint = CoopUi.Text(side, "Hint", "从大家选的歌里抽一首。", 24, CoopUi.SubInk);
			hint.rectTransform.Place(new Vector2(0, 1), new Vector2(30, -100), new Vector2(560, 40), new Vector2(0, 1));
			_pickLabel = CoopUi.Text(side, "MyPick", "还没有选", 30, CoopUi.Ink, true, TextAlignmentOptions.Center);
			_pickLabel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(560, 120));
			_pickLabel.textWrappingMode = TextWrappingModes.Normal;
			Button omakase = CoopUi.Button(side, "Omakase", "おまかせ", () =>
			{
				_myPick = string.Empty;
				CoopSession.PickSong(string.Empty);
				UpdatePick(null);
			}, new Color32(0xFF, 0x99, 0xBB, 0xFF), Color.white, 36);
			omakase.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(420, 100));

			LoadChartsAsync().Forget();
		}

		private async UniTaskVoid LoadChartsAsync()
		{
			if (_chartList == null)
			{
				return;
			}
			int request = ++_listRequest;
			RectTransform list = _chartList;
			CoopUi.Clear(list);
			_chartRows.Clear();
			CommunityChartPage page;
			try
			{
				page = await CommunityClient.ListAsync(new CommunityQuery { Sort = CommunitySort.Hot, Text = _search != null ? _search.text.Trim() : null }, Token);
			}
			catch (Exception exception) when (!(exception is OperationCanceledException))
			{
				if (request == _listRequest && list != null)
				{
					CoopUi.Text(list, "Error", "谱面列表加载失败。", 28, CoopUi.Danger);
				}
				return;
			}
			if (request != _listRequest || list == null)
			{
				return;
			}
			if (page.Items.Count == 0)
			{
				CoopUi.Size(CoopUi.Text(list, "Empty", "没有找到谱面。", 28, CoopUi.SubInk), height: 60);
			}
			foreach (CommunityChart chart in page.Items)
			{
				CommunityChart c = chart;
				Image row = CoopUi.Image(list, "Chart " + c.NumberId, CoopUi.PanelSoft);
				CoopUi.Size(row, height: 92);
				Button button = row.gameObject.AddComponent<Button>();
				button.targetGraphic = row;
				button.onClick.AddListener(() =>
				{
					_myPick = c.Id;
					CoopSession.PickSong(c.Id);
					UpdatePick(c);
				});
				Image level = CoopUi.Image(row.transform, "Level", CoopUi.DifficultyColor(c.Difficulty));
				level.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(150, 64));
				TextMeshProUGUI levelText = CoopUi.Text(level.transform, "Text", $"{CoopUi.DifficultyLabel(c.Difficulty)} {c.PlayLevel}", 22, Color.white, true, TextAlignmentOptions.Center);
				levelText.rectTransform.Stretch();
				TextMeshProUGUI name = CoopUi.Text(row.transform, "Title", c.Title, 30, CoopUi.Ink, true);
				name.rectTransform.Place(new Vector2(0, 1), new Vector2(184, -10), new Vector2(760, 40), new Vector2(0, 1));
				TextMeshProUGUI sub = CoopUi.Text(row.transform, "Sub", $"#{c.NumberId}  {c.ArtistLine}  ·  谱师 {c.Uploader?.Nickname}", 22, CoopUi.SubInk);
				sub.rectTransform.Place(new Vector2(0, 0), new Vector2(184, 10), new Vector2(760, 32), new Vector2(0, 0));
				_chartRows[c.Id] = row;
			}
			HighlightPick();
		}

		private void UpdatePick(CommunityChart chart)
		{
			if (_pickLabel != null)
			{
				_pickLabel.text = chart == null
					? "<color=#FF77AA>おまかせ</color>\n<size=24>交给随机</size>"
					: $"{chart.Title}\n<size=24><color=#{UnityEngine.ColorUtility.ToHtmlStringRGB(CoopUi.DifficultyColor(chart.Difficulty))}>{CoopUi.DifficultyLabel(chart.Difficulty)} {chart.PlayLevel}</color></size>";
			}
			HighlightPick();
		}

		private void HighlightPick()
		{
			foreach (var pair in _chartRows)
			{
				if (pair.Value != null)
				{
					pair.Value.color = pair.Key == _myPick ? new Color32(0xCC, 0xF4, 0xEE, 0xFF) : CoopUi.PanelSoft;
				}
			}
		}

		/* ---------- draw and difficulty ---------- */

		private void BuildDifficulty(Room room)
		{
			RectTransform card = Card("Song");
			card.Stretch();
			ChartInfo song = room.Song;
			Member picker = CoopSession.Find(room.SongPickedBy);
			string by = picker != null ? $"{picker.Nickname} 选的歌" : "おまかせ";
			TextMeshProUGUI head = CoopUi.Text(card, "Head", room.Phase == Phase.SongDraw ? "抽选结果" : "難易度選択", 30, CoopUi.SubInk, true);
			head.rectTransform.Place(new Vector2(0, 1), new Vector2(40, -30), new Vector2(600, 44), new Vector2(0, 1));
			TextMeshProUGUI title = CoopUi.Text(card, "Song", song != null ? song.Title : "?", 52, CoopUi.Ink, true);
			title.rectTransform.Place(new Vector2(0, 1), new Vector2(40, -80), new Vector2(1500, 70), new Vector2(0, 1));
			TextMeshProUGUI sub = CoopUi.Text(card, "By", $"{by}　·　{song?.Composer}", 26, CoopUi.SubInk);
			sub.rectTransform.Place(new Vector2(0, 1), new Vector2(40, -150), new Vector2(1500, 40), new Vector2(0, 1));
			Countdown(card);
			if (room.Phase == Phase.SongDraw)
			{
				return;
			}

			Member me = CoopSession.Me;
			// A song can have many community charts: wrap them, 7 to a row.
			RectTransform options = CoopUi.Rect("Options", card);
			options.Place(new Vector2(0, 1), new Vector2(40, -205), new Vector2(7 * 180 + 6 * 12, 252), new Vector2(0, 1));
			GridLayoutGroup grid = options.gameObject.AddComponent<GridLayoutGroup>();
			grid.cellSize = new Vector2(180, 120);
			grid.spacing = new Vector2(12, 12);
			grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			grid.constraintCount = 7;
			foreach (ChartInfo option in room.DifficultyOptions)
			{
				ChartInfo o = option;
				bool selected = me != null && me.ChartId == o.Id;
				Color color = CoopUi.DifficultyColor(o.Difficulty);
				Button button = CoopUi.Button(options, "Option " + o.Id, $"{CoopUi.DifficultyLabel(o.Difficulty)}\n<size=36>{o.PlayLevel}</size>",
					() => CoopSession.PickDifficulty(o.Id), selected ? color : Color.white, selected ? Color.white : color, 26);
				button.interactable = me != null && !me.Ready;
				if (room.DifficultyOptions.Count > 1)
				{
					// Leave the bottom of the cell for who made this chart.
					button.GetComponentInChildren<TextMeshProUGUI>().rectTransform.offsetMin = new Vector2(12, 22);
					TextMeshProUGUI uploader = CoopUi.Text(button.transform, "Uploader", o.Uploader, 16, selected ? Color.white : CoopUi.SubInk, false, TextAlignmentOptions.Center);
					uploader.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(170, 22), new Vector2(0.5f, 0));
				}
			}

			bool ready = me != null && me.Ready;
			Button readyButton = CoopUi.Button(card, "Ready", ready ? "準備完了 ✓" : "準備完了", () => CoopSession.SetReady(!ready),
				ready ? CoopUi.AccentDark : CoopUi.Accent, Color.white, 38);
			readyButton.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(-40, 40), new Vector2(420, 110), new Vector2(1, 0));
		}

		/* ---------- loading ---------- */

		private void BuildLoading(Room room)
		{
			RectTransform card = Card("Loading");
			card.Stretch();
			TextMeshProUGUI title = CoopUi.Text(card, "Song", room.Song?.Title ?? string.Empty, 48, CoopUi.Ink, true, TextAlignmentOptions.Center);
			title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1500, 70));
			_status = CoopUi.Text(card, "Status", LoadingText(room), 32, CoopUi.SubInk, true, TextAlignmentOptions.Center);
			_status.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1500, 60));
			if (room.Phase == Phase.Loading)
			{
				Countdown(card);
			}
		}

		private static string LoadingText(Room room)
		{
			Member me = CoopSession.Me;
			if (room.Phase == Phase.Playing)
			{
				return me != null && me.Playing ? "演奏中…" : "这一局开始了，你没能及时加载。下一局再一起吧。";
			}
			if (me == null || !me.Playing)
			{
				return "这一局你不参加，等待结束…";
			}
			return me.Loaded ? "等待其他成员…" : "准备中…";
		}

		private void OnLaunchStatus(string text)
		{
			if (_status != null && (_builtPhase == Phase.Loading || _builtPhase == Phase.Playing))
			{
				_status.text = text;
			}
		}

		/* ---------- stamps, notices, leaving ---------- */

		private void OnStamp(StampSent stamp)
		{
			Member member = CoopSession.Find(stamp.UserId);
			Room room = CoopSession.Room;
			if (member == null || room == null)
			{
				return;
			}
			int index = room.Members.IndexOf(member);
			if (index < 0 || index >= _members.childCount)
			{
				return;
			}
			if (_bubbles.TryGetValue(stamp.UserId, out var old) && old.bubble != null)
			{
				Destroy(old.bubble.gameObject);
			}
			Transform card = _members.GetChild(index);
			Image bubble = CoopUi.Image(card, "Stamp", new Color32(0xFF, 0xF5, 0xC8, 0xFF));
			bubble.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, -36), new Vector2(300, 64));
			TextMeshProUGUI text = CoopUi.Text(bubble.transform, "Text", CoopStamps.Text(stamp.StampId), 28, CoopUi.Ink, true, TextAlignmentOptions.Center);
			text.rectTransform.Stretch();
			_bubbles[stamp.UserId] = (bubble.rectTransform, Time.unscaledTime + 2.5f);
		}

		private void OnNotice(Notice notice)
		{
			Toast(notice.Message);
		}

		private void Toast(string message)
		{
			if (string.IsNullOrEmpty(message) || _toast == null)
			{
				return;
			}
			_toast.text = message;
			_toast.transform.parent.gameObject.SetActive(true);
			_toast.transform.parent.SetAsLastSibling();
			_toastUntil = Time.unscaledTime + 3f;
		}

		private void OnClosed(string code, string message)
		{
			if (code != "LEFT")
			{
				Toast(message);
			}
			Render(force: true);
		}

		private void OnBack()
		{
			if (_leaving)
			{
				return;
			}
			if (CoopSession.Room != null)
			{
				CoopSession.LeaveAsync().Forget();
				return;
			}
			_leaving = true;
			SceneManager.Instance.RequestScene(SceneManager.Scene.Home);
		}
	}
}
