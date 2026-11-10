using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Sekai.Core.Live
{
	public class LiveLogic
	{
		private const int InputTmpCount = 128;
		private const int MaxFingerCount = 10;
		private const float TargetFrameTime = 1f / 60f;
		// Below this |hi-speed| a note past the judge line falls back to sliding off at 1x.
		private const float MinScrollSpeed = 0.0001f;
		// Distances (seconds at 1x) this close to zero count as "on the judge line".
		private const float JudgeLineDistanceEpsilon = 0.00001f;
		private const int MouseFallbackTouchId = -1;
		private const int MouseFallbackFingerId = 0;

		[Serializable]
		public class InputTmp
		{
			public LiveTouch Touch;
			[NonSerialized]
			public float Lane;
			[NonSerialized]
			public List<NoteBase> CandidateNotes;

			public InputTmp()
			{
				CandidateNotes = new List<NoteBase>(4);
			}

			public InputTmp(InputTmp source)
				: this()
			{
				if (source == null)
				{
					return;
				}

				Touch = source.Touch;
				Lane = source.Lane;
				CandidateNotes.AddRange(source.CandidateNotes);
			}

			public void Reset(ref LiveTouch touch, float lane)
			{
				Touch = touch;
				Lane = lane;
				CandidateNotes.Clear();
			}
		}

		[Serializable]
		public class InputLogInfo
		{
			public List<LiveTouch> buffer;

			public InputLogInfo(LiveTouch[] buffer)
			{
				this.buffer = buffer != null ? new List<LiveTouch>(buffer) : new List<LiveTouch>();
			}
		}

		private readonly LiveBundleBuildData liveBundleBuildData;
		private LiveBootDataBase bootData;
		private LiveViewBase[] liveViews;
		private MusicScore musicScore;
		private NoteBase[] noteArray = Array.Empty<NoteBase>();

		/// <summary>Auto play: the notes whose judgment plays a sound, by time (see <see cref="PlayAutoSeAhead"/>).</summary>
		private NoteBase[] autoSeNotes = Array.Empty<NoteBase>();

		private int autoSeIndex;
		private NoteScrollTimeline scrollTimeline = NoteScrollTimeline.Create(null);
		// noteArray indices ordered by the time each note (or one of its children) first comes on screen.
		private int[] spawnOrder = Array.Empty<int>();
		private float[] spawnTimes = Array.Empty<float>();
		private int spawnIndex;
		// noteArray indices of spawned, unfinished notes, kept in score-time order for judgment.
		private readonly List<int> activeNoteIndices = new List<int>();
		private EventBase[] eventArray = Array.Empty<EventBase>();
		private int eventStartIndex;
		private readonly Dictionary<int, (double time, InputTouchPhase phase)> lastTouches;
		private readonly List<NoteBase> judgmentNoteList;
		private readonly InputTmp[] inputTmpArray;
		private readonly List<InputTmp> activeInputList;
		private Camera calcCamera;
		private InputLogInfo recordedInputLogger;
		private readonly List<NoteBase> connectionNoteList;
		private readonly Dictionary<int, (double time, Vector2 position)> touchPositionList;
		private readonly List<int> usedFingerList;
		private readonly Dictionary<int, double> InputedNormalNoteTouch;
		private float noteDisplayTimeOffset;
		private float seBaseVolume = 1f;
		private float seScoreVolume = 1f;

		public event Action OnFinished;
		public event Action OnFailure;

		public ScoreLogic scoreLogic;
		public SkillLogic skillLogic = new NullSkillLogic();
		public int noteStartIndex;
		public int result;
		public int TapCount { get; set; }
		public double currentGameTime;
		public MusicScoreInfo currentFrameInfo;

		public LiveScore Score => scoreLogic?.score ?? default;

		public bool IsNotesAllFinished => spawnIndex >= spawnOrder.Length && activeNoteIndices.Count == 0;

		/// <summary>
		/// How many judgments the chart has between two chart times (seconds, end excluded), counted like
		/// <see cref="LiveUtility.CalculateTotalComboCount"/> (every judged part, long-note holds included).
		/// </summary>
		public int CountJudgments(float fromTime, float toTime)
		{
			int count = 0;
			foreach (NoteBase rootNote in noteArray)
			{
				if (rootNote?.NoteList == null)
				{
					continue;
				}
				foreach (NoteBase note in rootNote.NoteList)
				{
					float time = note != null ? note.MusicScoreInfo.time : -1f;
					if (note != null && note.HasJudgment && time >= fromTime && time < toTime)
					{
						count++;
					}
				}
			}
			return count;
		}

		public bool IsPerfectCombo => scoreLogic?.IsPerfectCombo ?? false;

		public bool IsAllPerfectCombo => scoreLogic?.IsAllPerfectCombo ?? false;

		public LiveLogic(LiveBundleBuildData data)
		{
			NativeInput.Enable();
			int fingerCapacity = Mathf.Max(NativeInput.Fingers.Count, MaxFingerCount);
			lastTouches = new Dictionary<int, (double time, InputTouchPhase phase)>(fingerCapacity);
			judgmentNoteList = new List<NoteBase>();
			inputTmpArray = new InputTmp[InputTmpCount];
			for (int i = 0; i < inputTmpArray.Length; i++)
			{
				inputTmpArray[i] = new InputTmp();
			}
			activeInputList = new List<InputTmp>(InputTmpCount);
			connectionNoteList = new List<NoteBase>(5);
			touchPositionList = new Dictionary<int, (double time, Vector2 position)>(MaxFingerCount);
			usedFingerList = new List<int>(MaxFingerCount);
			InputedNormalNoteTouch = new Dictionary<int, double>(MaxFingerCount);
			seScoreVolume = 1f;
			liveBundleBuildData = data;
		}

		public void Setup(LiveBootDataBase bootData, LiveViewBase[] liveViews)
		{
			this.bootData = bootData;
			this.liveViews = liveViews;
			seBaseVolume = ApplicationLocalSettings.LoadFromStorage().LiveVolume?.Se ?? 1f;
			noteDisplayTimeOffset = LiveConfig.GetNoteDisplayOffsetTime(bootData?.LiveSettingData?.NoteSpeed ?? 6f);

			musicScore = bootData?.MusicData?.MusicScore ?? new MusicScore();
			if (bootData?.MusicData != null)
			{
				bootData.MusicData.MusicScore = musicScore;
			}

			musicScore.InjectSkillFeverForCreatorScore(bootData?.MusicData?.Vocal?.musicId ?? bootData?.MusicData?.Music?.id ?? 0);
			noteArray = SortNotes(musicScore.NoteArray);
			scrollTimeline = NoteScrollTimeline.Create(musicScore.musicScoreInfoArray);
			BuildSpawnOrder();
			eventArray = SortEvents(musicScore.EventArray);
			BindNoteCallbacks(noteArray);
			autoSeNotes = BuildAutoSeNotes(noteArray);
			autoSeIndex = 0;
			BindEventCallbacks(eventArray);
			LiveViewExt.CreateNotePool(liveViews, LiveUtility.CalculateMaxSimultaneousNotes(musicScore, CalcFirstVisibleTime));
			calcCamera = CameraUtility.GetFrontCamera();
			if (calcCamera == null)
			{
				calcCamera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
			}
			ResetScoreState();
			RefreshInput();
		}

		public void SetSkillLogic(SkillLogic logic)
		{
			skillLogic = logic ?? new NullSkillLogic();
			skillLogic.Setup(bootData, scoreLogic?.score ?? default);
		}

		public void SetScoreLogic(ScoreLogic logic)
		{
			scoreLogic = logic;
			scoreLogic?.Setup(bootData, musicScore);
			LiveViewExt.SetupScore(liveViews, scoreLogic?.score ?? default);
		}

		public void RefreshInput()
		{
			foreach (var device in InputSystem.devices)
			{
				InputSystem.ResetDevice(device);
			}

			NativeInput.Enable();
			lastTouches.Clear();
			activeInputList.Clear();
			touchPositionList.Clear();
			InputedNormalNoteTouch.Clear();
			var activeTouches = NativeInput.ActiveTouches;
			for (int i = 0; i < activeTouches.Count; i++)
			{
				EnhancedTouch touch = activeTouches[i];
				lastTouches[touch.touchId] = (touch.time, touch.phase);
			}
		}

		private void ResetScoreState()
		{
			autoSeIndex = 0;
			noteStartIndex = 0;
			spawnIndex = 0;
			activeNoteIndices.Clear();
			eventStartIndex = 0;
			result = 0;
			TapCount = 0;
			musicScore?.ResetNote();
			musicScore?.ResetEvent();
		}

		public void OnUpdate(float scoreInfoTime, double currentGameTime)
		{
			if (result > 2)
			{
				return;
			}

			musicScore?.Update(scoreInfoTime);
			currentFrameInfo = MusicScore.CurrentFrameInfo;
			this.currentGameTime = currentGameTime;

			UpdateSeVolume();
			InitializeUpdate();
			UpdateEvent();
			UpdateSkill();
			UpdateNote();

			if (scoreLogic.HasValue() && scoreLogic.score.life <= 0)
			{
				Result = 2;
			}

			if (IsNotesAllFinished && result == 0)
			{
				result = 3;
				OnFinished?.Invoke();
			}
		}

		public int Result
		{
			get => result;
			set
			{
				if (result == value)
				{
					return;
				}

				result = value;
				if (value == 2)
				{
					OnFailure?.Invoke();
				}
				if (value >= 3)
				{
					OnFinished?.Invoke();
				}
			}
		}

		/// <summary>
		/// Auto play: plays the sound of every note due by <paramref name="aheadTime"/> (the judgment time plus the output
		/// latency) now, so it is heard together with the music; the note is still judged, and its effect shown, on time.
		/// The note sounds used to be started at the judgment and so came one output latency after the music.
		/// </summary>
		public void PlayAutoSeAhead(float aheadTime)
		{
			while (autoSeIndex < autoSeNotes.Length && autoSeNotes[autoSeIndex].MusicScoreInfo.time <= aheadTime)
			{
				NoteBase note = autoSeNotes[autoSeIndex++];
				// Already judged (before the start of a test play, or before this pass began): its sound has gone.
				if (note.State == NoteState.Done)
				{
					continue;
				}
				foreach (LiveViewBase view in liveViews ?? Array.Empty<LiveViewBase>())
				{
					if (view is LiveFrontView frontView)
					{
						frontView.PlayAutoSeAhead(note);
					}
				}
			}
		}

		/// <summary>
		/// The notes auto play judges with a result that sounds: every note, and each long note's start, relay points and
		/// end. Guide notes are never judged, hidden relay points are judged Pass, and hold combos make no sound.
		/// </summary>
		private static NoteBase[] BuildAutoSeNotes(NoteBase[] notes)
		{
			List<NoteBase> result = new List<NoteBase>();
			foreach (NoteBase note in notes)
			{
				if (note == null || note is GuideNote)
				{
					continue;
				}
				if (note is LongNote longNote && longNote.NoteList != null)
				{
					foreach (NoteBase child in longNote.NoteList)
					{
						if (child != null && !(child is HiddenConnectionNote) && !(child is LongHoldCombo))
						{
							result.Add(child);
						}
					}
					continue;
				}
				result.Add(note);
			}
			return result.OrderBy(note => note.MusicScoreInfo.time).ToArray();
		}

		public void OnAutoInput()
		{
			int index = noteStartIndex;
			while (index < noteArray.Length)
			{
				NoteBase note = noteArray[index];
				if (note == null || note.State == NoteState.Done)
				{
					index++;
					continue;
				}
				if (note.MusicScoreInfo.time > currentFrameInfo.time)
				{
					return;
				}
				note.AutoJudgment(currentFrameInfo);
				index++;
			}
		}

		public void OnInput()
		{
			int activeInputCount = 0;
			activeInputList.Clear();
			EnhancedTouch[] activeTouches = NativeInput.ActiveTouches.ToArray();

			for (int i = 0; i < activeTouches.Length; i++)
			{
				EnhancedTouch touch = activeTouches[i];
				if (ShouldProcessTouch(touch))
				{
					TouchExecution(ref touch, ref activeInputCount);
					UpdateLastTouch(ref touch);
				}
			}

			for (int i = 0; i < activeTouches.Length; i++)
			{
				EnhancedTouch touch = activeTouches[i];
				UpdateTouchPositionCache(ref touch);
			}

#if UNITY_EDITOR || UNITY_STANDALONE
			ProcessMouseFallback(ref activeInputCount);
#endif
			Judgment();
		}

		public void Continue()
		{
			Result = 0;
		}

		private void InitializeUpdate()
		{
			judgmentNoteList.Clear();
		}

		private void UpdateEvent()
		{
			while (eventStartIndex < eventArray.Length)
			{
				EventBase eventBase = eventArray[eventStartIndex];
				if (eventBase == null || eventBase.State == EventState.Done)
				{
					eventStartIndex++;
					continue;
				}

				if (eventBase.MusicScoreInfo.time > currentFrameInfo.time)
				{
					return;
				}

				eventBase.Update(currentFrameInfo, ExcuteEvent);
				if (eventBase.State == EventState.Done)
				{
					eventStartIndex++;
				}
			}
		}

		private void UpdateSkill()
		{
			skillLogic?.Update(currentGameTime);
		}

		private void UpdateNote()
		{
			float spawnLimit = currentFrameInfo.time + TargetFrameTime;
			while (spawnIndex < spawnOrder.Length && spawnTimes[spawnIndex] <= spawnLimit)
			{
				InsertActiveNote(spawnOrder[spawnIndex]);
				spawnIndex++;
			}

			int keep = 0;
			for (int i = 0; i < activeNoteIndices.Count; i++)
			{
				int index = activeNoteIndices[i];
				NoteBase note = noteArray[index];
				if (note.State != NoteState.Done)
				{
					note.Excute(currentFrameInfo, CalcTimeOffset(note));
				}

				if (note.State == NoteState.Done)
				{
					continue;
				}

				UpdateJudgmentNoteArray(note);
				activeNoteIndices[keep++] = index;
			}

			activeNoteIndices.RemoveRange(keep, activeNoteIndices.Count - keep);
			AdvanceFinishedNotes();
		}

		private void InsertActiveNote(int index)
		{
			int position = activeNoteIndices.BinarySearch(index);
			if (position < 0)
			{
				activeNoteIndices.Insert(~position, index);
			}
		}

		private void AdvanceFinishedNotes()
		{
			while (noteStartIndex < noteArray.Length)
			{
				NoteBase note = noteArray[noteStartIndex];
				if (note != null && note.State != NoteState.Done)
				{
					return;
				}
				noteStartIndex++;
			}
		}

		/// <summary>
		/// Orders notes by when they first come on screen. With negative or zero hi-speed this is not score-time
		/// order, so the update loop can no longer stop at the first note that is still off screen.
		/// </summary>
		private void BuildSpawnOrder()
		{
			spawnTimes = new float[noteArray.Length];
			spawnOrder = new int[noteArray.Length];
			for (int i = 0; i < noteArray.Length; i++)
			{
				NoteBase note = noteArray[i];
				float spawnTime = CalcFirstVisibleTime(note);
				if (note.NoteList != null)
				{
					foreach (NoteBase child in note.NoteList)
					{
						if (child != null && child != note)
						{
							spawnTime = Mathf.Min(spawnTime, CalcFirstVisibleTime(child));
						}
					}
				}

				spawnTimes[i] = spawnTime;
				spawnOrder[i] = i;
			}

			// Ties keep score-time order (noteArray is sorted by time).
			float[] keys = spawnTimes;
			Array.Sort(spawnOrder, (a, b) =>
			{
				int compare = keys[a].CompareTo(keys[b]);
				return compare != 0 ? compare : a.CompareTo(b);
			});
			float[] sortedTimes = new float[spawnOrder.Length];
			for (int i = 0; i < spawnOrder.Length; i++)
			{
				sortedTimes[i] = keys[spawnOrder[i]];
			}
			spawnTimes = sortedTimes;
		}

		private float CalcFirstVisibleTime(NoteBase note)
		{
			float noteTime = note.MusicScoreInfo.time;
			return scrollTimeline.FirstVisibleTime(scrollTimeline.PositionAt(noteTime), note.speedRatio, noteDisplayTimeOffset, noteTime);
		}

		private void UpdateJudgmentNoteArray(NoteBase note)
		{
			// FrictionHideLongNote itself has no scoring judgment, but it is the hidden input carrier for trace long notes.
			// It must still receive held input frames so its generated LongHoldCombo points are judged by LongNote.Judgment.
			if (note != null && (note.HasJudgment || note is FrictionHideLongNote))
			{
				judgmentNoteList.Add(note);
			}
		}

		private bool ShouldProcessTouch(EnhancedTouch touch)
		{
			if (!lastTouches.TryGetValue(touch.touchId, out var lastTouch))
			{
				return true;
			}

			if (touch.time < lastTouch.time)
			{
				return false;
			}

			return lastTouch.phase != InputTouchPhase.Ended;
		}

		private void UpdateLastTouch(ref EnhancedTouch touch)
		{
			lastTouches[touch.touchId] = (touch.time, touch.phase);
		}

		private void TouchExecution(ref EnhancedTouch touch, ref int activeInputCount)
		{
			int touchId = touch.touchId;
			InputTouchPhase phase = touch.phase;
			double time = touch.time;
			if (lastTouches.TryGetValue(touchId, out var lastTouch)
				&& time.Equals(lastTouch.time))
			{
				if (IsEndedOrCanceled(phase) || phase == InputTouchPhase.None)
				{
					return;
				}

				if (phase == InputTouchPhase.Began || phase == InputTouchPhase.Stationary)
				{
					phase = InputTouchPhase.Moved;
				}
			}

			float musicTime = (float)((time - currentGameTime) * TargetFrameTime + currentFrameInfo.time);
			CreateLiveTouch(touchId, musicTime, ref touch, out LiveTouch liveTouch, phase);
			OnProcessTouch(ref liveTouch, ref activeInputCount);
		}

		private void CreateLiveTouch(int touchId, float musicTime, ref EnhancedTouch processTouch, out LiveTouch liveTouch, InputTouchPhase phase)
		{
			Vector2 screenPosition = processTouch.screenPosition;
			Vector3 screenPoint = new Vector3(screenPosition.x, screenPosition.y, 0f);
			Vector3 worldPosition = calcCamera != null
				? calcCamera.ScreenToWorldPoint(screenPoint)
				: new Vector3(screenPosition.x, screenPosition.y, 0f);
			Vector2 delta = LiveUtility.Vector2Zero;

			if (processTouch.phase == InputTouchPhase.Began)
			{
				touchPositionList[touchId] = (processTouch.time, screenPosition);
			}

			if (touchPositionList.TryGetValue(touchId, out var cachedPosition))
			{
				if (processTouch.time >= cachedPosition.time)
				{
					delta = screenPosition - cachedPosition.position;
				}
			}
			else
			{
				delta = screenPosition;
			}

			int fingerId = processTouch.finger != null ? processTouch.finger.index : touchId;
			liveTouch = new LiveTouch(fingerId, touchId, delta, worldPosition, phase, musicTime);
		}

		private void OnProcessTouch(ref LiveTouch processTouch, ref int activeInputCount)
		{
			float judgmentLane = GetJudgmentLane(processTouch.worldPosition, true);
			if (activeInputCount <= InputTmpCount - 1)
			{
				InputTmp input = inputTmpArray[activeInputCount++];
				input.Reset(ref processTouch, judgmentLane);
				activeInputList.Add(input);
			}

			if (processTouch.phase == InputTouchPhase.Began)
			{
				TapCount++;
			}
		}

#if UNITY_EDITOR || UNITY_STANDALONE
		private void ProcessMouseFallback(ref int activeInputCount)
		{
			Mouse mouse = Mouse.current;
			if (mouse == null)
			{
				return;
			}

			InputTouchPhase phase;
			if (mouse.leftButton.wasPressedThisFrame)
			{
				phase = InputTouchPhase.Began;
			}
			else if (mouse.leftButton.wasReleasedThisFrame)
			{
				phase = InputTouchPhase.Ended;
			}
			else if (mouse.leftButton.isPressed)
			{
				Vector2 mouseDelta = mouse.delta.ReadValue();
				phase = mouseDelta.sqrMagnitude > 0f ? InputTouchPhase.Moved : InputTouchPhase.Stationary;
			}
			else
			{
				return;
			}

			Vector2 screenPosition = mouse.position.ReadValue();
			double time = Time.realtimeSinceStartupAsDouble;
			Vector2 delta = mouse.delta.ReadValue();
			if (phase == InputTouchPhase.Began)
			{
				touchPositionList[MouseFallbackTouchId] = (time, screenPosition);
				delta = LiveUtility.Vector2Zero;
			}
			else if (touchPositionList.TryGetValue(MouseFallbackTouchId, out var cachedPosition))
			{
				delta = screenPosition - cachedPosition.position;
			}

			Vector3 screenPoint = new Vector3(screenPosition.x, screenPosition.y, 0f);
			Vector3 worldPosition = calcCamera != null
				? calcCamera.ScreenToWorldPoint(screenPoint)
				: new Vector3(screenPosition.x, screenPosition.y, 0f);
			float musicTime = (float)((time - currentGameTime) * TargetFrameTime + currentFrameInfo.time);
			var liveTouch = new LiveTouch(MouseFallbackFingerId, MouseFallbackTouchId, delta, worldPosition, phase, musicTime);
			OnProcessTouch(ref liveTouch, ref activeInputCount);

			if (IsEndedOrCanceled(phase))
			{
				touchPositionList.Remove(MouseFallbackTouchId);
				lastTouches.Remove(MouseFallbackTouchId);
			}
			else
			{
				touchPositionList[MouseFallbackTouchId] = (time, screenPosition);
				lastTouches[MouseFallbackTouchId] = (time, phase);
			}
		}
#endif

		private void UpdateTouchPositionCache(ref EnhancedTouch touch)
		{
			int touchId = touch.touchId;
			if (touchPositionList.ContainsKey(touchId))
			{
				if (IsEndedOrCanceled(touch.phase))
				{
					touchPositionList.Remove(touchId);
					return;
				}

				if (touch.time > touchPositionList[touchId].time)
				{
					touchPositionList[touchId] = (touch.time, touch.screenPosition);
				}
				return;
			}

			touchPositionList.Add(touchId, (touch.time, touch.screenPosition));
		}

		private void Judgment()
		{
			connectionNoteList.Clear();
			AllocateInputJudgableEachNotes();
			OverlapJudgmentSorting();
			LeaveNotesFastGeneration();
			PickUpNotBeginInputNotes();
			EvaluateJudgment();
		}

		private void AllocateInputJudgableEachNotes()
		{
			for (int noteIndex = 0; noteIndex < judgmentNoteList.Count; noteIndex++)
			{
				NoteBase judgmentNote = judgmentNoteList[noteIndex];
				if (judgmentNote == null)
				{
					continue;
				}

				bool isConnection = true;
				bool isJudged = false;
				for (int inputIndex = activeInputList.Count - 1; inputIndex >= 0; inputIndex--)
				{
					InputTmp activeInput = activeInputList[inputIndex];
					NoteBase note = judgmentNote;
					float lane = ConvertLaneBy(ref note, ref activeInput);
					if (lane >= 0f && note.IsJudgment(ref activeInput.Touch, lane))
					{
						isJudged = true;
						int touchId = activeInput.Touch.touchId;
						NoteBase lastNote = GetLastNote(judgmentNote);
						if (judgmentNote.NoteList != null && judgmentNote.NoteList.Count == 1)
						{
							AddCheckSingleInputedNormalNoteTouch(ref judgmentNote, ref lastNote, ref activeInput, ref isConnection, ref isJudged, ref touchId);
						}
						else
						{
							AddCheckLongInputedNormalNoteTouch(ref judgmentNote, ref lastNote, ref activeInput, ref isConnection, ref isJudged, ref touchId);
						}
						continue;
					}

					NoteBase firstNote = GetFirstNote(judgmentNote);
					if (firstNote != null && TryAddConnectionCandidate(firstNote, activeInput))
					{
						isConnection = true;
						isJudged = true;
					}
				}

				if (isJudged && isConnection)
				{
					connectionNoteList.Add(judgmentNote);
				}
			}
		}

		private void OverlapJudgmentSorting()
		{
			// Original keeps per-input candidate order as score-time order here.
			// Same-touch fast-generation pruning is handled by LeaveNotesFastGeneration.
			ResolveOverlappingCandidatesAcrossInputs();
		}

		private void ResolveOverlappingCandidatesAcrossInputs()
		{
			for (int inputIndex = activeInputList.Count - 1; inputIndex >= 0; inputIndex--)
			{
				InputTmp input = activeInputList[inputIndex];
				if (input.CandidateNotes.Count == 0)
				{
					continue;
				}

				for (int otherInputIndex = inputIndex - 1; otherInputIndex >= 0; otherInputIndex--)
				{
					InputTmp otherInput = activeInputList[otherInputIndex];
					if (otherInput.CandidateNotes.Count == 0)
					{
						continue;
					}

					for (int candidateIndex = input.CandidateNotes.Count - 1; candidateIndex >= 0; candidateIndex--)
					{
						NoteBase candidate = input.CandidateNotes[candidateIndex];
						int otherCandidateIndex = otherInput.CandidateNotes.IndexOf(candidate);
						if (candidate == null || otherCandidateIndex < 0)
						{
							continue;
						}

						NoteBase inputNote = ResolveJudgmentNote(candidate, input);
						NoteBase otherNote = ResolveJudgmentNote(candidate, otherInput);
						float inputDistance = inputNote != null
							? inputNote.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref inputNote, ref input))
							: float.MaxValue;
						float otherDistance = otherNote != null
							? otherNote.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref otherNote, ref otherInput))
							: float.MaxValue;

						if (otherDistance <= inputDistance)
						{
							input.CandidateNotes.RemoveAt(candidateIndex);
						}
						else
						{
							otherInput.CandidateNotes.RemoveAt(otherCandidateIndex);
						}
					}
				}
			}
		}

		private void LeaveNotesFastGeneration()
		{
			for (int inputIndex = activeInputList.Count - 1; inputIndex >= 0; inputIndex--)
			{
				InputTmp input = activeInputList[inputIndex];
				for (int i = input.CandidateNotes.Count - 1; i >= 0; i--)
				{
					if (input.CandidateNotes.Count < 2)
					{
						break;
					}

					NoteBase first = ResolveFastGenerationNote(input.CandidateNotes[0], input);
					NoteBase current = ResolveFastGenerationNote(input.CandidateNotes[i], input);
					if (first == null || current == null)
					{
						continue;
					}

					if (IsSameJudgmentTime(first, current))
					{
						float firstDistance = first.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref first, ref input));
						float currentDistance = current.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref current, ref input));
						if (firstDistance < currentDistance)
						{
							input.CandidateNotes.RemoveAt(i);
						}
						else
						{
							input.CandidateNotes.RemoveAt(0);
						}
					}
					else if (current.CalcNoteResult(input.Touch.musicTime) >= NoteResult.Great
						&& first.CalcNoteResult(input.Touch.musicTime) <= NoteResult.Good)
					{
						input.CandidateNotes.RemoveAt(0);
					}
					else if (first.MusicScoreInfo.time < current.MusicScoreInfo.time)
					{
						input.CandidateNotes.RemoveAt(i);
					}
					else
					{
						input.CandidateNotes.RemoveAt(0);
					}

					i = input.CandidateNotes.Count;
				}
			}
		}

		private void PickUpNotBeginInputNotes()
		{
			for (int inputIndex = activeInputList.Count - 1; inputIndex >= 0; inputIndex--)
			{
				InputTmp input = activeInputList[inputIndex];
				for (int noteIndex = connectionNoteList.Count - 1; noteIndex >= 0; noteIndex--)
				{
					NoteBase note = connectionNoteList[noteIndex];
					float lane = ConvertLaneBy(ref note, ref input);
					if (lane >= 0f && note != null && note.IsJudgment(ref input.Touch, lane))
					{
						input.CandidateNotes.Add(note);
						connectionNoteList.RemoveAt(noteIndex);
					}
				}
			}
		}

		private void EvaluateJudgment()
		{
			usedFingerList.Clear();
			for (int inputIndex = 0; inputIndex < activeInputList.Count; inputIndex++)
			{
				InputTmp input = activeInputList[inputIndex];
				bool hasCandidate = input.CandidateNotes.Count > 0;
				for (int candidateIndex = 0; candidateIndex < input.CandidateNotes.Count; candidateIndex++)
				{
					NoteBase note = input.CandidateNotes[candidateIndex];
					float lane = ConvertLaneBy(ref note, ref input);
					if (note == null)
					{
						continue;
					}

					note.Judgment(ref input.Touch, lane);
					if (note.Result == NoteResult.None || note.Result == NoteResult.Bad)
					{
						InputedNormalNoteTouch[input.Touch.touchId] = input.Touch.musicTime;
					}

					judgmentNoteList.Remove(note);
				}

				if (hasCandidate)
				{
					input.CandidateNotes.Clear();
					usedFingerList.Add(input.Touch.fingerId);
				}
			}

			for (int inputIndex = 0; inputIndex < activeInputList.Count; inputIndex++)
			{
				InputTmp input = activeInputList[inputIndex];
				bool used = false;
				for (int i = usedFingerList.Count - 1; i >= 0; i--)
				{
					used |= usedFingerList[i] == input.Touch.fingerId;
				}

				if (!used && input.Lane >= 0f)
				{
					LiveViewExt.Unpicked(liveViews, Mathf.RoundToInt(input.Lane), ref input.Touch);
				}

				if (IsEndedOrCanceled(input.Touch.phase))
				{
					InputedNormalNoteTouch.Remove(input.Touch.touchId);
				}
			}

			activeInputList.Clear();
		}

		private void AddCheckSingleInputedNormalNoteTouch(ref NoteBase judgmentNote, ref NoteBase note, ref InputTmp activeInput, ref bool isConnection, ref bool isJudged, ref int touchId)
		{
			if (judgmentNote is FrictionNote)
			{
				float lane = ConvertLaneBy(ref note, ref activeInput);
				if (judgmentNote.IsJudgment(ref activeInput.Touch, lane))
				{
					isConnection = true;
					isJudged = true;
					return;
				}
			}

			if (note is FrictionLongNote or FrictionHideLongNote)
			{
				float lane = ConvertLaneBy(ref note, ref activeInput);
				if (note.IsJudgment(ref activeInput.Touch, lane))
				{
					isConnection = true;
					isJudged = true;
					return;
				}
			}

			activeInput.CandidateNotes.Add(judgmentNote);
			isConnection = false;
			InputedNormalNoteTouch[touchId] = activeInput.Touch.musicTime;
		}

		private void AddCheckLongInputedNormalNoteTouch(ref NoteBase judgmentNote, ref NoteBase note, ref InputTmp activeInput, ref bool isConnection, ref bool isJudged, ref int touchId)
		{
			float lane = ConvertLaneBy(ref note, ref activeInput);
			bool hadInput = InputedNormalNoteTouch.ContainsKey(touchId);
			bool isLastNoteJudgment = note != null && note.IsJudgment(ref activeInput.Touch, lane);
			if (hadInput)
			{
				if (isLastNoteJudgment)
				{
					isConnection = false;
					if (note.CalcNoteResult(activeInput.Touch.musicTime) > NoteResult.Great
						|| activeInput.Touch.musicTime - InputedNormalNoteTouch[touchId] > 1f / 6f)
					{
						activeInput.CandidateNotes.Add(judgmentNote);
						return;
					}
				}
			}
			else if (isLastNoteJudgment)
			{
				activeInput.CandidateNotes.Add(judgmentNote);
				isConnection = false;
				return;
			}

			note = GetFirstNote(judgmentNote);
			lane = ConvertLaneBy(ref note, ref activeInput);
			if ((note is FrictionLongNote or FrictionHideLongNote)
				&& note.IsJudgment(ref activeInput.Touch, lane))
			{
				isConnection = true;
				isJudged = true;
				return;
			}

			if (note != null && note.Result == NoteResult.None)
			{
				lane = ConvertLaneBy(ref note, ref activeInput);
				if (note.IsJudgment(ref activeInput.Touch, lane))
				{
					activeInput.CandidateNotes.Add(judgmentNote);
					isConnection = false;
					return;
				}
			}

			if (note is LongNote)
			{
				lane = ConvertLaneBy(ref note, ref activeInput);
				if (note.IsJudgment(ref activeInput.Touch, lane))
				{
					isConnection = true;
					isJudged = true;
				}
			}
		}

		private float ConvertLaneBy(ref NoteBase note, ref InputTmp input)
		{
			if (note == null || input == null)
			{
				return input?.Lane ?? -1f;
			}

			if ((note.Category == NoteCategory.Flick || note.Category == NoteCategory.FrictionFlick)
				&& note.Direction == NoteDirection.Left)
			{
				return GetJudgmentLane(input.Touch.worldPosition, !note.FinishFirstAction);
			}

			return input.Lane;
		}

		private float GetJudgmentLane(Vector3 touchPosition, bool checkJudgmentY)
		{
			Vector2[] positions = LiveConfig.JudgmentPositions;
			if (positions == null || positions.Length == 0)
			{
				return -1f;
			}

			float judgmentOffsetX = liveBundleBuildData?.JudgmentOffsetX ?? 0f;
			float judgmentOffsetY = liveBundleBuildData?.JudgmentOffsetY ?? 0f;
			if (checkJudgmentY && touchPosition.y - positions[0].y > judgmentOffsetY)
			{
				return -1f;
			}

			if (positions[0].x - touchPosition.x > judgmentOffsetX)
			{
				return -1f;
			}

			int lastIndex = positions.Length - 1;
			if (touchPosition.x - positions[lastIndex].x > judgmentOffsetX)
			{
				return -1f;
			}

			for (int i = 0; i < positions.Length; i++)
			{
				if (touchPosition.x - positions[i].x <= 0f)
				{
					if (i == 0)
					{
						return 0f;
					}

					float previousX = positions[i - 1].x;
					float currentX = positions[i].x;
					return (touchPosition.x - previousX) / (currentX - previousX) + i - 1;
				}
			}

			return lastIndex;
		}

		private bool TryAddConnectionCandidate(NoteBase note, InputTmp input)
		{
			if (note is not LongNote and not FrictionLongNote and not FrictionHideLongNote)
			{
				return false;
			}

			float lane = ConvertLaneBy(ref note, ref input);
			if (lane < 0f || !note.IsJudgment(ref input.Touch, lane))
			{
				return false;
			}

			input.CandidateNotes.Add(note);
			return true;
		}

		private NoteBase ResolveJudgmentNote(NoteBase note, InputTmp input)
		{
			if (note?.NoteList == null || note.NoteList.Count == 1)
			{
				return note;
			}

			if (note.Result != NoteResult.None || input.Touch.phase != InputTouchPhase.Began)
			{
				return GetLastNote(note);
			}

			return GetFirstNote(note);
		}

		private NoteBase ResolveFastGenerationNote(NoteBase note, InputTmp input)
		{
			if (note?.NoteList == null || note.NoteList.Count == 1)
			{
				return note;
			}

			NoteBase resolved = note.Result != NoteResult.None || input.Touch.phase != InputTouchPhase.Began
				? GetLastNote(note)
				: GetFirstNote(note);
			float lane = ConvertLaneBy(ref resolved, ref input);
			if (lane < 0f || resolved == null || !resolved.IsJudgment(ref input.Touch, lane))
			{
				return note;
			}

			return resolved;
		}

		private static NoteBase GetFirstNote(NoteBase note)
		{
			return note?.NoteList != null && note.NoteList.Count > 0 ? note.NoteList[0] : note;
		}

		private static NoteBase GetLastNote(NoteBase note)
		{
			return note?.NoteList != null && note.NoteList.Count > 0 ? note.NoteList[note.NoteList.Count - 1] : note;
		}

		private static bool IsSameJudgmentTime(NoteBase a, NoteBase b)
		{
			return a != null && b != null && a.MusicScoreInfo.time.Equals(b.MusicScoreInfo.time);
		}

		private static bool IsEndedOrCanceled(InputTouchPhase phase)
		{
			return phase == InputTouchPhase.Ended || phase == InputTouchPhase.Canceled;
		}

		private void ExcuteEvent(EventBase eventBase)
		{
			scoreLogic?.ExcuteEvent(eventBase);
			skillLogic?.ExcuteEvent(eventBase);
		}

		private void OnSpawnNote(NoteBase note)
		{
			LiveViewExt.SpawnNote(liveViews, note);
		}

		private void OnUnSpawnNote(NoteBase note)
		{
			LiveViewExt.UnspawnNote(liveViews, note);
		}

		private void OnJudgmentNote(NoteBase note)
		{
			scoreLogic?.UpdateNoteResult(note);
			LiveViewExt.JudgmentNote(liveViews, note);
		}

		private void OnUpdateCombo(NoteBase note)
		{
			scoreLogic?.UpdateCombo(note);
			LiveViewExt.UpdateCombo(liveViews, scoreLogic?.score ?? default);
		}

		private void OnUpdateScore(NoteBase note)
		{
			int addScore = scoreLogic?.CalculateAddScore(note, skillLogic?.ScoreFactor(note) ?? 1f) ?? 0;
			LiveScore score = scoreLogic?.score ?? default;
			LiveViewExt.UpdateScore(liveViews, ref score, addScore);
		}

		private void OnDamage(NoteBase note)
		{
			scoreLogic?.Damage(note);
			LiveViewExt.UpdateLife(liveViews, scoreLogic?.score ?? default);
		}

		private (NoteResult result, NoteResultDescription description) OnUpdateResult((NoteResult result, NoteResultDescription description) result)
		{
			return skillLogic?.UpdateResult(result) ?? result;
		}

		/// <summary>
		/// Display window passed to <see cref="NoteBase.Excute"/>. Notes turn it into a progress with
		/// (now + offset - noteTime) / offset, so the window is chosen to make that progress equal
		/// 1 - distance / noteDisplayTime, where distance comes from the scroll timeline. Zero means the note
		/// is on the judge line (progress 1).
		/// </summary>
		private float CalcTimeOffset(NoteBase note)
		{
			if (note == null)
			{
				return 0f;
			}

			float now = currentFrameInfo.time;
			float noteTime = note.MusicScoreInfo.time;
			float untilHit = noteTime - now;
			if (untilHit <= 0f)
			{
				// Past its time a note keeps sliding down at the current speed, whatever its direction,
				// so a missed note leaves through the bottom of the screen.
				float speed = Mathf.Abs(scrollTimeline.SpeedAt(now) * note.speedRatio);
				return speed < MinScrollSpeed ? noteDisplayTimeOffset : noteDisplayTimeOffset / speed;
			}

			float distance = (float)((scrollTimeline.PositionAt(noteTime) - scrollTimeline.PositionAt(now)) * note.speedRatio);
			if (Mathf.Abs(distance) <= JudgeLineDistanceEpsilon)
			{
				return 0f;
			}

			if (distance < 0f)
			{
				// Scrolled past the judge line before its time: hidden until it comes back (progress -1).
				return untilHit * 0.5f;
			}

			return noteDisplayTimeOffset * untilHit / distance;
		}

		private void BindNoteCallbacks(NoteBase[] notes)
		{
			if (notes == null)
			{
				return;
			}

			HashSet<NoteBase> visited = new HashSet<NoteBase>();

			foreach (NoteBase note in notes)
			{
				BindNoteCallbacks(note, visited);
			}
		}

		private void BindNoteCallbacks(NoteBase note, HashSet<NoteBase> visited)
		{
			if (note == null || !visited.Add(note))
			{
				return;
			}

			note.OnSpawn = OnSpawnNote;
			note.OnUnspawn = OnUnSpawnNote;
			note.OnJudgment = OnJudgmentNote;
			note.OnUpdateCombo = OnUpdateCombo;
			note.OnUpdateScore = OnUpdateScore;
			note.OnDamage = OnDamage;
			note.OnUpdateResult = OnUpdateResult;
			note.CalcTimeOffset = CalcTimeOffset;
			note.ScrollTimeline = scrollTimeline;
			note.ScrollDisplayTime = noteDisplayTimeOffset;

			if (note.NoteList == null)
			{
				return;
			}

			foreach (NoteBase childNote in note.NoteList)
			{
				BindNoteCallbacks(childNote, visited);
			}
		}

		private static void BindEventCallbacks(EventBase[] events)
		{
		}

		private void UpdateSeVolume()
		{
			if (Mathf.Approximately(seScoreVolume, currentFrameInfo.seVolume))
			{
				return;
			}

			seScoreVolume = currentFrameInfo.seVolume;
			// TODO(original): route to SoundManager.SetVolumeSEPlayer once the CRI player pool is fully mirrored.
		}

		private static NoteBase[] SortNotes(NoteBase[] notes)
		{
			return notes == null
				? Array.Empty<NoteBase>()
				: notes.Where(note => note != null).OrderBy(note => note.MusicScoreInfo.time).ToArray();
		}

		private static EventBase[] SortEvents(EventBase[] events)
		{
			return events == null
				? Array.Empty<EventBase>()
				: events.Where(eventBase => eventBase != null).OrderBy(eventBase => eventBase.MusicScoreInfo.time).ToArray();
		}
	}

	internal static class ScoreLogicExtensions
	{
		public static bool HasValue(this ScoreLogic logic)
		{
			return logic != null;
		}
	}
}
