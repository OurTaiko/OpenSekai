using System.Collections;
using System.Collections.Generic;
using Sekai.Core.Live;
using Sekai.CoopLive.Proto;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.CoopLive
{
	/// <summary>
	/// Turns the Core scene's solo live into this player's part of a 協力ライブ:
	/// <list type="bullet">
	/// <item>start: once the music is ready it reports `loaded` and waits for `start_live`, so everyone starts together;</item>
	/// <item>during the live: sends this player's score, combo and life, and shows the other players at the left;</item>
	/// <item>FEVER CHANCE: reports what this player hit in it, shows the team's FEVER / SUPER FEVER, and FEVER
	/// notes score 1.5 times (<see cref="CoopScoreLogic"/>);</item>
	/// <item>end: sends the final score; the live then returns to the co-op room, which shows the result.</item>
	/// </list>
	/// The live keeps going at life 0 and cannot be paused, as in the original.
	/// </summary>
	public sealed class CoopLiveBridge : MonoBehaviour
	{
		private const float StatusInterval = 0.2f;
		private const float StartTimeoutSeconds = 100f;

		// FEVER CHANCE: same span of music time for every player (see the proto's FeverReport).
		private const double FeverChanceStartRate = 0.45;
		private const double FeverSpanRate = 0.15;
		private const long FeverSpanMaxMs = 20_000;
		private const long FeverGapMs = 3_000;

		private SoloLiveController _controller;
		private bool _started;
		private bool _abort;
		private bool _ended;
		private float _nextStatusAt;
		private PlayerStatus _lastSent;
		private int _minLife = int.MaxValue;

		private long _chanceStartMs = -1;
		private long _chanceEndMs;
		private long _feverStartMs;
		private long _feverEndMs;
		private bool _chanceOpen;
		private bool _chanceReported;
		private LiveScore _chanceStartScore;
		private FeverKind _fever = FeverKind.Unspecified;

		private RectTransform _hud;
		private RectTransform _players;
		private TextMeshProUGUI _banner;
		private TextMeshProUGUI _waiting;
		private readonly Dictionary<string, PlayerRow> _rows = new Dictionary<string, PlayerRow>();

		private sealed class PlayerRow
		{
			public RectTransform Root;
			public TextMeshProUGUI Name;
			public TextMeshProUGUI Score;
			public TextMeshProUGUI Detail;
			public RectTransform LifeFill;
			public CanvasGroup Group;
		}

		/// <summary>Whether FEVER is on now: decided FEVER or SUPER FEVER, within the FEVER span.</summary>
		public bool FeverActive
		{
			get
			{
				if (_fever != FeverKind.Fever && _fever != FeverKind.SuperFever || _controller == null || _chanceStartMs < 0)
				{
					return false;
				}
				long t = _controller.MusicTimeMs;
				return t >= _feverStartMs && t < _feverEndMs;
			}
		}

		/// <summary>Hooks the controller before it boots. Called by the Core scene's EntryPoint.</summary>
		public void Attach(SoloLiveController controller)
		{
			_controller = controller;
			controller.IsCoop = true;
			controller.StartGate = WaitForEveryone;
			controller.ScoreLogicFactory = data => new CoopScoreLogic(data, () => FeverActive);
			controller.LiveEnded += OnLiveEnded;
			CoopSession.LiveStarted += OnLiveStarted;
			CoopSession.StatusesChanged += OnStatuses;
			CoopSession.FeverDecided += OnFever;
			CoopSession.RoomChanged += OnRoom;
			CoopSession.Closed += OnClosed;
			BuildHud();
		}

		private void OnDestroy()
		{
			if (_controller != null)
			{
				_controller.LiveEnded -= OnLiveEnded;
			}
			CoopSession.LiveStarted -= OnLiveStarted;
			CoopSession.StatusesChanged -= OnStatuses;
			CoopSession.FeverDecided -= OnFever;
			CoopSession.RoomChanged -= OnRoom;
			CoopSession.Closed -= OnClosed;
		}

		/* ---------- start ---------- */

		private IEnumerator WaitForEveryone()
		{
			if (!CoopSession.IsConnected || CoopSession.Room == null)
			{
				_controller.AbortToMenu();
				yield break;
			}
			CoopSession.Loaded(true);
			_waiting.gameObject.SetActive(true);
			float until = Time.realtimeSinceStartup + StartTimeoutSeconds;
			while (!_started && !_abort && Time.realtimeSinceStartup < until)
			{
				yield return null;
			}
			_waiting.gameObject.SetActive(false);
			if (!_started)
			{
				// The round started without this player, the room went away, or nobody answered.
				_controller.AbortToMenu();
				// Keep the live from starting while the scene changes.
				while (true)
				{
					yield return null;
				}
			}
		}

		private void OnLiveStarted(int round)
		{
			if (CoopSession.Room == null || round == CoopSession.Room.Round)
			{
				_started = true;
			}
		}

		private void OnRoom(Room room)
		{
			if (_started)
			{
				return;
			}
			Member me = CoopSession.Me;
			bool stillLoading = room.Phase == Phase.Loading && me != null && me.Playing;
			bool startedWithMe = room.Phase == Phase.Playing && me != null && me.Playing;
			if (startedWithMe)
			{
				_started = true;
			}
			else if (!stillLoading)
			{
				_abort = true;
			}
		}

		private void OnClosed(string code, string message)
		{
			if (!_started)
			{
				_abort = true;
			}
			foreach (PlayerRow row in _rows.Values)
			{
				row.Detail.text = "切断";
			}
		}

		/* ---------- during the live ---------- */

		private void Update()
		{
			if (_controller == null || !_started || _ended || !_controller.IsPlayableState)
			{
				return;
			}
			LiveScore score = _controller.CurrentScore;
			_minLife = Mathf.Min(_minLife, score.life);
			UpdateFeverChance(score);
			UpdateBanner();

			if (Time.unscaledTime >= _nextStatusAt)
			{
				_nextStatusAt = Time.unscaledTime + StatusInterval;
				PlayerStatus status = ToStatus(score, _controller.MusicTimeMs);
				if (_lastSent == null || !SameScore(_lastSent, status))
				{
					_lastSent = status;
					CoopSession.SendStatus(status);
				}
			}
		}

		private void UpdateFeverChance(LiveScore score)
		{
			if (_chanceStartMs < 0)
			{
				long length = _controller.MusicLengthMs;
				if (length <= 0)
				{
					return;
				}
				long span = System.Math.Min(FeverSpanMaxMs, (long)(length * FeverSpanRate));
				_chanceStartMs = (long)(length * FeverChanceStartRate);
				_chanceEndMs = _chanceStartMs + span;
				_feverStartMs = _chanceEndMs + FeverGapMs;
				_feverEndMs = _feverStartMs + span;
			}
			long t = _controller.MusicTimeMs;
			if (!_chanceOpen && !_chanceReported && t >= _chanceStartMs && t < _chanceEndMs)
			{
				_chanceOpen = true;
				_chanceStartScore = score;
			}
			if (!_chanceReported && t >= _chanceEndMs)
			{
				ReportFeverChance(score);
			}
		}

		private void ReportFeverChance(LiveScore end)
		{
			_chanceReported = true;
			LiveScore start = _chanceOpen ? _chanceStartScore : end;
			_chanceOpen = false;
			int perfectOrGreat = end.perfectCount + end.greatCount - start.perfectCount - start.greatCount;
			int misses = end.goodCount + end.badCount + end.missCount - start.goodCount - start.badCount - start.missCount;
			CoopSession.SendFeverReport(new FeverReport
			{
				Judged = Mathf.Max(0, perfectOrGreat + misses),
				PerfectOrGreat = Mathf.Max(0, perfectOrGreat),
				ComboKept = misses == 0,
			});
		}

		private void OnFever(FeverKind kind)
		{
			_fever = kind;
		}

		private void UpdateBanner()
		{
			long t = _controller.MusicTimeMs;
			string text = string.Empty;
			Color color = Color.white;
			if (_chanceStartMs >= 0 && t >= _chanceStartMs && t < _chanceEndMs)
			{
				text = "FEVER CHANCE!";
				color = new Color32(0xFF, 0xEE, 0x88, 0xFF);
			}
			else if (FeverActive)
			{
				text = _fever == FeverKind.SuperFever ? "SUPER FEVER!!" : "FEVER!";
				color = _fever == FeverKind.SuperFever ? new Color32(0xFF, 0x88, 0xDD, 0xFF) : new Color32(0xFF, 0xCC, 0x44, 0xFF);
			}
			if (_banner.text != text)
			{
				_banner.text = text;
				_banner.color = color;
			}
			if (text.Length > 0)
			{
				float pulse = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 8f);
				_banner.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
			}
		}

		private void OnStatuses(LiveStatuses statuses)
		{
			foreach (MemberStatus status in statuses.Members)
			{
				if (status.UserId == CoopSession.MyUserId)
				{
					continue;
				}
				PlayerRow row = Row(status.UserId);
				if (row == null)
				{
					continue;
				}
				PlayerStatus s = status.Status ?? new PlayerStatus();
				row.Score.text = s.Score.ToString("N0");
				string detail = s.Combo > 0 ? $"{s.Combo} COMBO" : string.Empty;
				if (!status.Connected)
				{
					detail = "切断";
				}
				else if (status.Finished)
				{
					detail = "FINISH";
				}
				else if (s.Life <= 0 && s.MusicTimeMs > 0)
				{
					detail = "LIFE 0";
				}
				row.Detail.text = detail;
				row.Detail.color = s.Life <= 0 || !status.Connected ? CoopUi.Danger : Color.white;
				row.LifeFill.anchorMax = new Vector2(Mathf.Clamp01(s.Life / 1000f), 1f);
				row.Group.alpha = status.Connected ? 1f : 0.5f;
			}
		}

		/* ---------- end ---------- */

		private void OnLiveEnded(LiveScore final)
		{
			if (_ended || !_started)
			{
				_ended = true;
				return;
			}
			_ended = true;
			if (!_chanceReported && _chanceStartMs >= 0 && _controller.MusicTimeMs >= _chanceStartMs)
			{
				ReportFeverChance(final);
			}
			_minLife = Mathf.Min(_minLife, final.life);
			CoopSession.Finish(ToStatus(final, _controller.MusicTimeMs), _minLife <= 0);
		}

		private static PlayerStatus ToStatus(LiveScore s, long musicTimeMs)
		{
			return new PlayerStatus
			{
				Score = s.totalScore,
				Combo = s.combo,
				MaxCombo = s.maxCombo,
				Life = s.life,
				Perfect = s.perfectCount,
				Great = s.greatCount,
				Good = s.goodCount,
				Bad = s.badCount,
				Miss = s.missCount,
				MusicTimeMs = (int)System.Math.Min(int.MaxValue, musicTimeMs),
			};
		}

		private static bool SameScore(PlayerStatus a, PlayerStatus b)
		{
			return a.Score == b.Score && a.Combo == b.Combo && a.Life == b.Life && a.Miss == b.Miss && a.Good == b.Good && a.Bad == b.Bad;
		}

		/* ---------- HUD ---------- */

		private void BuildHud()
		{
			GameObject canvasGo = new GameObject("CoopLiveHud", typeof(RectTransform));
			canvasGo.transform.SetParent(transform, false);
			Canvas canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 500;
			CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920, 1080);
			scaler.matchWidthOrHeight = 1f;
			_hud = (RectTransform)canvasGo.transform;

			_players = CoopUi.Rect("Players", _hud).Place(new Vector2(0, 1), new Vector2(24, -150), new Vector2(330, 420));
			CoopUi.Vertical(_players.gameObject, 10);

			_banner = CoopUi.Text(_hud, "FeverBanner", string.Empty, 72, Color.white, true, TextAlignmentOptions.Center);
			_banner.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(1200, 100));
			_banner.outlineWidth = 0.25f;
			_banner.outlineColor = new Color32(0x33, 0x22, 0x55, 0xFF);

			_waiting = CoopUi.Text(_hud, "Waiting", "等待其他成员加载…", 40, Color.white, true, TextAlignmentOptions.Center);
			_waiting.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(1200, 80));
			_waiting.outlineWidth = 0.2f;
			_waiting.outlineColor = Color.black;
			_waiting.gameObject.SetActive(false);

			Room room = CoopSession.Room;
			if (room != null)
			{
				foreach (Member member in room.Members)
				{
					if (member.UserId != CoopSession.MyUserId && member.Playing)
					{
						Row(member.UserId);
					}
				}
			}
		}

		private PlayerRow Row(string userId)
		{
			if (_rows.TryGetValue(userId, out PlayerRow existing))
			{
				return existing;
			}
			Member member = CoopSession.Find(userId);
			if (member == null || _players == null)
			{
				return null;
			}
			Image background = CoopUi.Image(_players, "Player " + member.Slot, new Color(0.08f, 0.06f, 0.18f, 0.6f));
			CoopUi.Size(background, height: 74);
			Image stripe = CoopUi.Image(background.transform, "Slot", CoopUi.SlotColor(member.Slot));
			stripe.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(10, 58));
			TextMeshProUGUI name = CoopUi.Text(background.transform, "Name", member.Nickname, 24, Color.white, true);
			name.rectTransform.Place(new Vector2(0, 1), new Vector2(28, -6), new Vector2(180, 32), new Vector2(0, 1));
			TextMeshProUGUI score = CoopUi.Text(background.transform, "Score", "0", 26, Color.white, true, TextAlignmentOptions.Right);
			score.rectTransform.Place(new Vector2(1, 1), new Vector2(-14, -6), new Vector2(150, 32), new Vector2(1, 1));
			TextMeshProUGUI detail = CoopUi.Text(background.transform, "Detail", string.Empty, 20, Color.white, false, TextAlignmentOptions.Right);
			detail.rectTransform.Place(new Vector2(1, 0), new Vector2(-14, 8), new Vector2(200, 28), new Vector2(1, 0));
			Image lifeBack = CoopUi.Image(background.transform, "Life", new Color(1, 1, 1, 0.2f), false);
			lifeBack.rectTransform.Place(new Vector2(0, 0), new Vector2(28, 14), new Vector2(110, 8), new Vector2(0, 0));
			Image lifeFill = CoopUi.Image(lifeBack.transform, "Fill", new Color32(0x77, 0xEE, 0x99, 0xFF), false);
			lifeFill.rectTransform.Stretch();
			CanvasGroup group = background.gameObject.AddComponent<CanvasGroup>();
			PlayerRow row = new PlayerRow { Root = background.rectTransform, Name = name, Score = score, Detail = detail, LifeFill = lifeFill.rectTransform, Group = group };
			_rows[userId] = row;
			return row;
		}
	}
}
