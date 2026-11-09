using System.Collections.Generic;
using OpenSekai.SceneFlow;
using Sekai.CoopLive.Proto;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.CoopLive
{
	/// <summary>Which round the CoopLive.Result scene shows. Without one it shows the round this player just played.</summary>
	public sealed class CoopResultSceneArgs
	{
		public int Round { get; private set; }

		/// <summary>Opens the result scene for <paramref name="round"/> (the room's 上一局结果).</summary>
		public static void Open(int round)
		{
			SceneSwitcher.SetSceneArgs(new CoopResultSceneArgs { Round = round });
			SceneManager.Instance.RequestScene(SceneManager.Scene.CoopLiveResult);
		}

		public static CoopResultSceneArgs Take() => SceneSwitcher.TakeSceneArgs<CoopResultSceneArgs>();
	}

	/// <summary>
	/// 協力ライブ: the result scene, which the live goes to when it ends. Until everyone has finished it shows the
	/// others' scores as they come in; then the ranking, team score, FEVER, MVP / SUPER STAR, and the judgements.
	/// スタンプ work here as in the room. 返回房间 goes back to the room; so does a new round starting.
	/// </summary>
	public sealed class ScreenLayerCoopResult : ScreenLayer
	{
		[SerializeField]
		private Sprite roundSprite;

		[SerializeField]
		private Sprite backgroundSprite;

		private RectTransform _root;
		private TextMeshProUGUI _title;
		private TextMeshProUGUI _subtitle;
		private RectTransform _card;
		private TextMeshProUGUI _toast;
		private float _toastUntil;
		private int _round;
		private bool _shownResult;
		private bool _leaving;
		private readonly Dictionary<string, RectTransform> _rows = new Dictionary<string, RectTransform>();
		private readonly List<(GameObject bubble, float until)> _bubbles = new List<(GameObject, float)>();

		protected override void Awake()
		{
			base.Awake();
			CoopUi.RoundSprite = roundSprite;
			_root = CoopUi.Frame(transform, backgroundSprite, BackToRoom, out _title, out _subtitle);
			_title.text = "協力ライブ  RESULT";
			_card = CoopUi.Image(_root, "Card", CoopUi.Panel).rectTransform;
			_card.Stretch(120, 160, 120, 120);
			CoopUi.StampBar(_root);
			_toast = CoopUi.Toast(_root);
		}

		protected override void OnScreenStart()
		{
			_leaving = false;
			_round = CoopResultSceneArgs.Take()?.Round ?? CoopSession.LastPlayedRound;
			CoopSession.RoomChanged += OnRoom;
			CoopSession.StatusesChanged += OnStatuses;
			CoopSession.StampReceived += OnStamp;
			CoopSession.Closed += OnClosed;
			Render();
		}

		private void OnDestroy()
		{
			CoopSession.RoomChanged -= OnRoom;
			CoopSession.StatusesChanged -= OnStatuses;
			CoopSession.StampReceived -= OnStamp;
			CoopSession.Closed -= OnClosed;
		}

		private LiveResult Result
		{
			get
			{
				LiveResult result = CoopSession.Room?.Result ?? CoopSession.LastResult;
				return result != null && result.Round == _round ? result : null;
			}
		}

		private void OnRoom(Room room)
		{
			// The room started its next round while this player was still here.
			if (room.Round > _round && room.Phase != Phase.Lobby && room.Phase != Phase.Result)
			{
				BackToRoom();
				return;
			}
			if (!_shownResult)
			{
				Render();
			}
		}

		private void Render()
		{
			Room room = CoopSession.Room;
			_subtitle.text = room != null ? $"ID <size=44>{room.Code}</size>" : string.Empty;
			LiveResult result = Result;
			CoopUi.Clear(_card);
			_rows.Clear();
			if (result != null)
			{
				_shownResult = true;
				BuildResult(result);
			}
			else
			{
				BuildWaiting();
			}
			Button back = CoopUi.Button(_card, "Back", "返回房间", BackToRoom, CoopUi.Accent, Color.white, 34);
			back.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(-40, 30), new Vector2(420, 96), new Vector2(1, 0));
		}

		/* ---------- waiting for the others ---------- */

		private void BuildWaiting()
		{
			bool connected = CoopSession.Room != null;
			TextMeshProUGUI head = CoopUi.Text(_card, "Head", connected ? "等待其他成员结束演奏…" : "已经和房间断开，没能拿到这一局的结果。", 40, CoopUi.Ink, true);
			head.rectTransform.Place(new Vector2(0, 1), new Vector2(50, -36), new Vector2(1500, 60), new Vector2(0, 1));
			if (!connected)
			{
				return;
			}
			RectTransform rows = CoopUi.Rect("Rows", _card).Stretch(40, 130, 40, 150);
			CoopUi.Vertical(rows.gameObject, 10);
			LiveStatuses statuses = CoopSession.Statuses;
			foreach (Member member in CoopSession.Room.Members)
			{
				if (!member.Playing && !member.Finished)
				{
					continue;
				}
				MemberStatus status = null;
				if (statuses != null)
				{
					foreach (MemberStatus s in statuses.Members)
					{
						if (s.UserId == member.UserId)
						{
							status = s;
						}
					}
				}
				Image row = Row(rows, member.UserId, member.Slot, member.UserId == CoopSession.MyUserId);
				TextMeshProUGUI name = CoopUi.Text(row.transform, "Name", member.Nickname, 34, CoopUi.Ink, true);
				name.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(700, 50), new Vector2(0, 0.5f));
				string state = !member.Connected ? "<color=#EE5577>切断</color>" : member.Finished || status?.Finished == true ? "<color=#33BBAA>FINISH</color>" : "演奏中…";
				TextMeshProUGUI right = CoopUi.Text(row.transform, "State", $"{(status?.Status?.Score ?? 0):N0}    {state}", 32, CoopUi.Ink, true, TextAlignmentOptions.Right);
				right.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(800, 50), new Vector2(1, 0.5f));
			}
		}

		private void OnStatuses(LiveStatuses statuses)
		{
			if (!_shownResult)
			{
				Render();
			}
		}

		/* ---------- the result ---------- */

		private void BuildResult(LiveResult result)
		{
			TextMeshProUGUI head = CoopUi.Text(_card, "Head", "LIVE RESULT", 54, CoopUi.Ink, true);
			head.rectTransform.Place(new Vector2(0, 1), new Vector2(50, -30), new Vector2(700, 70), new Vector2(0, 1));
			string fever = result.Fever == FeverKind.SuperFever ? "<color=#FF66CC>SUPER FEVER</color>" : result.Fever == FeverKind.Fever ? "<color=#FFAA22>FEVER</color>" : "<color=#9999AA>NO FEVER</color>";
			TextMeshProUGUI team = CoopUi.Text(_card, "Team", $"{fever}　　TEAM SCORE <size=56>{result.TeamScore:N0}</size>", 32, CoopUi.Ink, true, TextAlignmentOptions.Right);
			team.rectTransform.Place(new Vector2(1, 1), new Vector2(-50, -30), new Vector2(1100, 70), new Vector2(1, 1));

			RectTransform rows = CoopUi.Rect("Rows", _card).Stretch(40, 130, 40, 150);
			CoopUi.Vertical(rows.gameObject, 10);
			foreach (PlayerResult p in result.Players)
			{
				Member member = CoopSession.Find(p.UserId);
				Image row = Row(rows, p.UserId, member?.Slot ?? p.Rank - 1, p.UserId == CoopSession.MyUserId);
				TextMeshProUGUI rank = CoopUi.Text(row.transform, "Rank", p.Rank.ToString(), 46, CoopUi.Ink, true, TextAlignmentOptions.Center);
				rank.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(70, 80));
				string award = p.Award == Award.SuperStar ? "<color=#FF66CC>SUPER STAR</color>" : p.Award == Award.Mvp ? "<color=#FFAA22>MVP</color>" : string.Empty;
				string tags = p.Disconnected ? "<color=#EE5577>切断</color>" : p.AllPerfect ? "<color=#FF66CC>ALL PERFECT</color>" : p.FullCombo ? "<color=#33BBAA>FULL COMBO</color>" : p.Failed ? "<color=#EE5577>LIFE 0</color>" : string.Empty;
				TextMeshProUGUI name = CoopUi.Text(row.transform, "Name", $"{p.Nickname}  <size=26>{award}</size>", 34, CoopUi.Ink, true);
				name.rectTransform.Place(new Vector2(0, 1), new Vector2(120, -8), new Vector2(620, 46), new Vector2(0, 1));
				TextMeshProUGUI sub = CoopUi.Text(row.transform, "Tags", tags, 22, CoopUi.SubInk, true);
				sub.rectTransform.Place(new Vector2(0, 0), new Vector2(120, 8), new Vector2(620, 34), new Vector2(0, 0));
				PlayerStatus s = p.Status ?? new PlayerStatus();
				TextMeshProUGUI detail = CoopUi.Text(row.transform, "Detail",
					$"<color=#FF77CC>P</color> {s.Perfect}  <color=#FF9933>G</color> {s.Great}  <color=#33BBEE>G</color> {s.Good}  <color=#66CC66>B</color> {s.Bad}  <color=#999999>M</color> {s.Miss}　COMBO {s.MaxCombo}",
					24, CoopUi.SubInk, false, TextAlignmentOptions.Right);
				detail.rectTransform.Place(new Vector2(1, 0), new Vector2(-30, 10), new Vector2(760, 34), new Vector2(1, 0));
				TextMeshProUGUI score = CoopUi.Text(row.transform, "Score", s.Score.ToString("N0"), 42, CoopUi.Ink, true, TextAlignmentOptions.Right);
				score.rectTransform.Place(new Vector2(1, 1), new Vector2(-30, -6), new Vector2(400, 52), new Vector2(1, 1));
			}
		}

		private Image Row(RectTransform parent, string userId, int slot, bool mine)
		{
			Image row = CoopUi.Image(parent, "Row " + userId, mine ? new Color32(0xE2, 0xF7, 0xF3, 0xFF) : CoopUi.PanelSoft);
			CoopUi.Size(row, height: 96);
			Image stripe = CoopUi.Image(row.transform, "Slot", CoopUi.SlotColor(slot));
			stripe.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(12, 76));
			_rows[userId] = row.rectTransform;
			return row;
		}

		/* ---------- stamps, leaving ---------- */

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

		private void OnStamp(StampSent stamp)
		{
			string text = CoopStamps.Text(stamp.StampId);
			if (_rows.TryGetValue(stamp.UserId, out RectTransform row) && row != null)
			{
				Image bubble = CoopUi.Image(row, "Stamp", new Color32(0xFF, 0xF5, 0xC8, 0xFF));
				bubble.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(40, 0), new Vector2(300, 64));
				TextMeshProUGUI label = CoopUi.Text(bubble.transform, "Text", text, 28, CoopUi.Ink, true, TextAlignmentOptions.Center);
				label.rectTransform.Stretch();
				_bubbles.Add((bubble.gameObject, Time.unscaledTime + 2.5f));
				return;
			}
			Member member = CoopSession.Find(stamp.UserId);
			Toast($"{member?.Nickname ?? "?"}：{text}");
		}

		private void OnClosed(string code, string message)
		{
			if (code != "LEFT")
			{
				Toast(message);
			}
			if (!_shownResult)
			{
				Render();
			}
		}

		private void Toast(string message)
		{
			_toast.text = message;
			_toast.transform.parent.gameObject.SetActive(true);
			_toast.transform.parent.SetAsLastSibling();
			_toastUntil = Time.unscaledTime + 3f;
		}

		private void BackToRoom()
		{
			if (_leaving)
			{
				return;
			}
			_leaving = true;
			SceneManager.Instance.RequestScene(SceneManager.Scene.CoopLive);
		}
	}
}
