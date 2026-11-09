using System.Collections;
using System.Collections.Generic;
using Sekai.Core.Live;
using Sekai.CoopLive.Proto;
using Sekai.Live;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.CoopLive
{
	/// <summary>
	/// Turns the Core scene's solo live into this player's part of a 協力ライブ:
	/// <list type="bullet">
	/// <item>start: once the music is ready it reports `loaded` and waits for `start_live`, so everyone starts together;</item>
	/// <item>during the live: sends this player's score, combo and life. The live UI gets the original マルチライブ UI's
	/// parts (<c>Resources/live/view/CoopLiveFrontParts</c>, taken from <c>FrontMultiUIView</c>): the team score bar in
	/// place of the solo one, every player's thumbnail (SSO avatar, combo, dying / dead, FEVER OK, YOU) and the FEVER
	/// view on the lane;</item>
	/// <item>FEVER CHANCE: reports what this player hit in it, shows the team's FEVER / SUPER FEVER, and FEVER
	/// notes score 1.5 times (<see cref="CoopScoreLogic"/>);</item>
	/// <item>end: sends the final score; the live then goes to the Result scene.</item>
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

		private const string FrontPartsPath = "live/view/CoopLiveFrontParts";

		private RectTransform _hud;
		private TextMeshProUGUI _waiting;

		// The multi-live parts grafted onto the live UI (see GraftFrontParts).
		private MultiFeverView _feverView;
		private MultiScoreView _teamScore;
		private MultiThumbnailViewGroup _thumbnails;
		private readonly List<Member> _players = new List<Member>();
		private readonly Dictionary<string, MemberStatus> _statuses = new Dictionary<string, MemberStatus>();
		// Each player's GOOD + BAD + MISS when FEVER CHANCE started; FEVER OK while it has not grown.
		private readonly Dictionary<string, int> _chanceMissBase = new Dictionary<string, int>();
		// Each player's PERFECT + GREAT when FEVER CHANCE started, for the FEVER gauge.
		private readonly Dictionary<string, int> _chanceHitBase = new Dictionary<string, int>();
		// Judgments in this player's chart during FEVER CHANCE (-1: not counted yet).
		private int _chanceJudgments = -1;
		private int[] _teamScores = System.Array.Empty<int>();

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
			GraftFrontParts();
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
			// Nothing more comes from the others: show them as cut off.
			foreach (Member player in _players)
			{
				if (player.UserId != CoopSession.MyUserId)
				{
					PlayerStatus s = _statuses.TryGetValue(player.UserId, out MemberStatus last) ? last.Status ?? new PlayerStatus() : new PlayerStatus();
					_thumbnails?.Find(player.UserId)?.UpdateStatus(s.Combo, false, s.Life, false);
				}
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
			UpdateFrontParts(score);

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

		private void OnStatuses(LiveStatuses statuses)
		{
			foreach (MemberStatus status in statuses.Members)
			{
				if (status.UserId == CoopSession.MyUserId)
				{
					continue;
				}
				_statuses[status.UserId] = status;
				PlayerStatus s = status.Status ?? new PlayerStatus();
				bool allPerfect = s.Perfect > 0 && s.Great + s.Good + s.Bad + s.Miss == 0;
				_thumbnails?.Find(status.UserId)?.UpdateStatus(s.Combo, allPerfect, s.Life, status.Connected);
			}
		}

		/* ---------- the multi-live parts of the live UI ---------- */

		/// <summary>
		/// Moves the parts of <c>CoopLiveFrontParts</c> under the live UI's anchors of the same names (as they sit in
		/// the original's <c>FrontMultiUIView</c>) and hides the solo score bar. Runs before the live UI is set up, so
		/// the parts fade in with the rest of it.
		/// </summary>
		private void GraftFrontParts()
		{
			LiveFrontView front = FindAnyObjectByType<LiveFrontView>(FindObjectsInactive.Include);
			GameObject prefab = Resources.Load<GameObject>(FrontPartsPath);
			if (front == null || prefab == null)
			{
				Debug.LogWarning($"Co-op live: no live UI or no {FrontPartsPath}; the other players are not shown.", this);
				return;
			}
			GameObject parts = Instantiate(prefab);
			foreach (string anchor in new[] { "Center", "LeftTopAnchor" })
			{
				Transform target = front.transform.Find("LiveUI/" + anchor);
				Transform holder = parts.transform.Find(anchor);
				while (target != null && holder != null && holder.childCount > 0)
				{
					holder.GetChild(0).SetParent(target, false);
				}
			}
			Destroy(parts);
			if (front.ScoreView != null)
			{
				front.ScoreView.gameObject.SetActive(false);
			}
			_feverView = front.GetComponentInChildren<MultiFeverView>(true);
			_teamScore = front.GetComponentInChildren<MultiScoreView>(true);
			_thumbnails = front.GetComponentInChildren<MultiThumbnailViewGroup>(true);

			_players.Clear();
			Room room = CoopSession.Room;
			if (room != null)
			{
				foreach (Member member in room.Members)
				{
					if (member.Playing)
					{
						_players.Add(member);
					}
				}
			}
			_players.Sort((a, b) => a.Slot.CompareTo(b.Slot));
			_teamScores = new int[_players.Count];
			Color[] colors = new Color[_players.Count];
			for (int i = 0; i < _players.Count; i++)
			{
				colors[i] = CoopUi.SlotColor(_players[i].Slot);
			}
			_thumbnails?.Setup(_players, CoopSession.MyUserId, false);
			_teamScore?.Setup(colors);
			_feverView?.Show(CoopFeverPhase.None);
		}

		private void UpdateFrontParts(LiveScore mine)
		{
			for (int i = 0; i < _players.Count; i++)
			{
				string id = _players[i].UserId;
				_teamScores[i] = id == CoopSession.MyUserId ? mine.totalScore
					: _statuses.TryGetValue(id, out MemberStatus status) && status.Status != null ? status.Status.Score : 0;
			}
			_teamScore?.UpdateScores(_teamScores);
			_thumbnails?.Find(CoopSession.MyUserId)?.UpdateStatus(mine.combo, mine.IsAllPerfect, mine.life, true);

			long t = _controller.MusicTimeMs;
			bool inChance = _chanceStartMs >= 0 && t >= _chanceStartMs && t < _chanceEndMs;
			CoopFeverPhase phase = inChance ? CoopFeverPhase.Chance
				: FeverActive ? (_fever == FeverKind.SuperFever ? CoopFeverPhase.SuperFever : CoopFeverPhase.Fever)
				: CoopFeverPhase.None;
			_feverView?.Show(phase);

			// FEVER OK: no GOOD, BAD or MISS since FEVER CHANCE started, shown from then until FEVER starts.
			// The gauge: the team's PERFECT + GREAT since then, as a share of everyone's notes in FEVER CHANCE (each
			// player is counted with this player's chart, which is the same song).
			bool showReady = _chanceStartMs >= 0 && t >= _chanceStartMs && t < _feverStartMs;
			if (inChance && _chanceJudgments < 0)
			{
				_chanceJudgments = _controller.CountJudgments(_chanceStartMs, _chanceEndMs);
			}
			int teamHits = 0;
			foreach (Member player in _players)
			{
				MultiThumbnailView view = _thumbnails?.Find(player.UserId);
				bool ready = false;
				if (showReady && TryCounts(player.UserId, mine, out int misses, out int hits, out long theirTime) && theirTime >= _chanceStartMs)
				{
					bool mineWithStart = player.UserId == CoopSession.MyUserId && _chanceOpen;
					if (!_chanceMissBase.TryGetValue(player.UserId, out int missStart))
					{
						missStart = mineWithStart ? Misses(_chanceStartScore) : misses;
						_chanceMissBase[player.UserId] = missStart;
					}
					if (!_chanceHitBase.TryGetValue(player.UserId, out int hitStart))
					{
						hitStart = mineWithStart ? Hits(_chanceStartScore) : hits;
						_chanceHitBase[player.UserId] = hitStart;
					}
					ready = misses == missStart;
					teamHits += Mathf.Max(0, hits - hitStart);
				}
				view?.SetFeverReady(ready);
			}
			if (inChance && _chanceJudgments > 0 && _players.Count > 0)
			{
				_feverView?.SetGauge(teamHits / (float)(_chanceJudgments * _players.Count));
			}
		}

		private bool TryCounts(string userId, LiveScore mine, out int misses, out int hits, out long musicTimeMs)
		{
			if (userId == CoopSession.MyUserId)
			{
				misses = Misses(mine);
				hits = Hits(mine);
				musicTimeMs = _controller.MusicTimeMs;
				return true;
			}
			if (_statuses.TryGetValue(userId, out MemberStatus status) && status.Status != null && status.Connected)
			{
				misses = status.Status.Good + status.Status.Bad + status.Status.Miss;
				hits = status.Status.Perfect + status.Status.Great;
				musicTimeMs = status.Status.MusicTimeMs;
				return true;
			}
			misses = 0;
			hits = 0;
			musicTimeMs = 0;
			return false;
		}

		private static int Misses(LiveScore score) => score.goodCount + score.badCount + score.missCount;

		private static int Hits(LiveScore score) => score.perfectCount + score.greatCount;

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

		/* ---------- the waiting text ---------- */

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

			_waiting = CoopUi.Text(_hud, "Waiting", "等待其他成员加载…", 40, Color.white, true, TextAlignmentOptions.Center);
			_waiting.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(1200, 80));
			_waiting.outlineWidth = 0.2f;
			_waiting.outlineColor = Color.black;
			_waiting.gameObject.SetActive(false);
		}
	}
}
