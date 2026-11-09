using Sekai.Live;
using Sekai.MusicScoreMaker.Common;
using Sekai.MusicScoreMaker.Ingame.Presenters;
using UnityEngine;

namespace Sekai.Core.Live
{
	public class SoloLiveController : BaseLiveController
	{
		[Header("Scene views (authored in Core.unity)")]
		[SerializeField]
		private LiveViewBase frontView;

		[SerializeField]
		private LiveViewBase background2DView;

		[SerializeField]
		private LiveViewBase background3DView;

		[Header("Prefabs instantiated on demand")]
		[SerializeField]
		[Tooltip("Pause/confirm dialogs; instantiated when a dialog opens and destroyed when it closes.")]
		private GameObject liveOutUIPrefab;

		private bool isTestPlayFinishedCalled;
		private Coroutine finishCoroutine;
		private Coroutine resumeCoroutine;
		private bool playHistoryRecorded;
		private LiveViewBase[] liveViews;
		private LiveLogic liveLogic;

		public bool IsBooted { get; private set; }

		/// <summary>
		/// A co-op live (協力ライブ, <see cref="Sekai.CoopLive.CoopLiveBridge"/>): no pause, retire or retry, the
		/// live keeps going at life 0, and it goes back to the co-op room. Set before <see cref="Boot"/>.
		/// </summary>
		public bool IsCoop { get; set; }

		/// <summary>Co-op: waits, once the music is ready, until every player can start.</summary>
		public System.Func<System.Collections.IEnumerator> StartGate { get; set; }

		/// <summary>Co-op: makes the score logic (FEVER scores more).</summary>
		public System.Func<LiveBundleBuildData, ScoreLogic> ScoreLogicFactory { get; set; }

		/// <summary>The score so far.</summary>
		public LiveScore CurrentScore => liveLogic?.Score ?? default;

		/// <summary>How many judgments fall between two music times (<see cref="BaseLiveController.MusicTimeMs"/>, end excluded).</summary>
		public int CountJudgments(long fromMusicMs, long toMusicMs)
		{
			float fillerSec = BootData?.MusicData?.Music?.fillerSec ?? 0f;
			return liveLogic?.CountJudgments(fromMusicMs / 1000f - fillerSec, toMusicMs / 1000f - fillerSec) ?? 0;
		}

		/// <summary>Raised once when the live ends (the last note, or leaving it), with the final score.</summary>
		public event System.Action<LiveScore> LiveEnded;

		private bool liveEndedRaised;

		private bool coopAborted;

		// The chart's best score before this live, read before the live's own record is written (null: not read yet).
		private int? previousBest;

		/// <summary>Starts the live described by <paramref name="bootData"/>. Called by <see cref="EntryPoint"/>.</summary>
		public void Boot(LiveBootDataBase bootData)
		{
			if (IsBooted)
			{
				Debug.LogWarning("SoloLiveController is already running.", this);
				return;
			}

			BootData = bootData;
			if (BootData == null)
			{
				Debug.LogWarning("No live boot data. Test play cannot start.", this);
				return;
			}

			IsBooted = true;
			currentMusicTimeMs = System.Math.Max(0L, BootData.MusicData?.StartMusicTimeMs ?? 0L);
			Setup();
			LoadSound();
			liveViews = SelectViews(BootData);
			LiveViewExt.Setup(liveViews, this);
			RetryProcess();
			liveOutUIController?.Initialize(BootData.LivePlayMode, BaseCamera, liveOutUIPrefab);
			LiveViewExt.OnLoad(liveViews);
			StartCoroutine(LiveStart(6f));
		}

		/// <summary>Enables the scene views this live uses and disables the others.</summary>
		private LiveViewBase[] SelectViews(LiveBootDataBase bootData)
		{
			bool use3DBackground = bootData.MusicCategory == MusicCategory.mv;
			LiveViewBase background = use3DBackground ? background3DView : background2DView;
			SetViewActive(background2DView, !use3DBackground);
			SetViewActive(background3DView, use3DBackground);
			SetViewActive(frontView, true);

			if (bootData.LivePlayMode != LivePlayMode.Free)
			{
				Debug.LogWarning($"Live play mode {bootData.LivePlayMode} has no dedicated front view in Core; using the solo view.", this);
			}

			var views = new System.Collections.Generic.List<LiveViewBase>(2);
			if (frontView != null)
			{
				views.Add(frontView);
			}
			else
			{
				Debug.LogError("SoloLiveController: frontView is not assigned in Core.unity.", this);
			}
			if (background != null)
			{
				views.Add(background);
			}
			return views.ToArray();
		}

		private static void SetViewActive(LiveViewBase view, bool active)
		{
			if (view != null && view.gameObject.activeSelf != active)
			{
				view.gameObject.SetActive(active);
			}
		}

		protected override void OnMusicStart()
		{
			LiveTransitioner.SafeFinish(null, null);
			base.OnMusicStart();
			LiveViewExt.MusicStart(liveViews, currentAudioLatencyMusicTimeMs);
		}

		protected override void OnRhythmGameStart()
		{
			LiveViewExt.RhythmGameStart(liveViews);
			base.OnRhythmGameStart();
			liveLogic?.RefreshInput();
		}

		protected override void OnUpdate()
		{
			base.OnUpdate();
			if (state != LiveControllerState.Playing)
			{
				return;
			}

			UpdateMusicTime();
			float fillerSec = BootData?.MusicData?.Music?.fillerSec ?? 0f;
			liveLogic?.OnUpdate(currentAudioLatencyMusicTimeMs - fillerSec - judgeAdjust, currentGameTime);
			if (liveLogic?.result == 2 && liveLogic.IsNotesAllFinished)
			{
				OnFinished();
			}
			if (BootData?.IsAuto == true)
			{
				liveLogic?.OnAutoInput();
			}
			else
			{
				liveLogic?.OnInput();
			}
			LiveViewExt.OnUpdate(liveViews, currentAudioLatencyMusicTimeMs);
		}

		protected override void OnApplicationPause(bool pauseStatus)
		{
			base.OnApplicationPause(pauseStatus);
			// Going to the background mid-song opens the pause menu instead of letting the chart run on.
			if (pauseStatus)
			{
				OnPause();
			}
		}

		protected override void OnPause()
		{
			// Co-op lives cannot be paused: the others keep playing.
			if (IsCoop)
			{
				return;
			}
			if ((state == LiveControllerState.Playing || state == LiveControllerState.ResumeCountDown) && result == 0)
			{
				state = LiveControllerState.Pause;
				SoundManager.Instance.PauseIngame(currentMusicTimeMs);
				LiveViewExt.Pause(liveViews);
				ShowPauseDialog();
			}
		}

		public void ShowPauseDialog()
		{
			if (BootData?.MusicData?.IsTestPlay == true)
			{
				ScreenLayerMusicScoreMaker.BootArg bootArg = MusicScoreMakerEntryPoint.BootData?.bootData;
				if (bootArg != null && bootArg.IsFromFullComboCheck)
				{
					liveOutUIController?.ShowMusicScoreMakerFullComboCheckPauseDialog(OnResume, OnReturnToMusicScoreMaker, OnTestPlayRetryConfirm);
				}
				else
				{
					liveOutUIController?.ShowMusicScoreMakerTestPlayPauseDialog(OnResume, OnReturnToMusicScoreMaker, OnTestPlayRetryConfirm);
				}
				return;
			}

			liveOutUIController?.ShowPauseDialog(OnResume, OnRetireConfirm, OnRetryConfirm);
		}

		public void PauseLive()
		{
			if ((state != LiveControllerState.Playing && state != LiveControllerState.ResumeCountDown) || result != 0)
			{
				return;
			}

			state = LiveControllerState.Pause;
			SoundManager.Instance.PauseIngame(currentMusicTimeMs);
			LiveViewExt.Pause(liveViews);
		}

		protected override void OnResume()
		{
			if (state != LiveControllerState.Pause)
			{
				return;
			}

			state = LiveControllerState.ResumeCountDown;
			liveOutUIController?.Destroy();
			LiveViewExt.Countdown(liveViews);
			if (resumeCoroutine != null)
			{
				StopCoroutine(resumeCoroutine);
			}
			resumeCoroutine = StartCoroutine(ResumeCoroutine());
		}

		public void ResumeNoCountDown()
		{
			if (state != LiveControllerState.Pause)
			{
				return;
			}

			state = LiveControllerState.ResumeCountDown;
			liveOutUIController?.Destroy();
			SoundManager.Instance.ResumeIngame(currentMusicTimeMs);
			state = LiveControllerState.Playing;
			LiveViewExt.Resume(liveViews, currentAudioLatencyMusicTimeMs);
			liveLogic?.RefreshInput();
		}

		public void ShowConsecutiveAutoLiveRetireDialog()
		{
			CallPreExit();
		}

		protected override void OnRetry()
		{
			liveOutUIController?.Destroy();
			SoundManager.Instance.StopIngame();
			isTestPlayFinishedCalled = false;
			playHistoryRecorded = false;
			result = 0;
			state = LiveControllerState.None;
			currentMusicTimeMs = System.Math.Max(0L, BootData?.MusicData?.StartMusicTimeMs ?? 0L);
			RetryProcess();
			LiveViewExt.Retry(liveViews);
			StartCoroutine(LiveStart(6f));
		}

		private void RetryProcess()
		{
			if (liveLogic != null)
			{
				liveLogic.OnFinished -= OnFinished;
				liveLogic.OnFailure -= OnFailure;
			}

			liveLogic = new LiveLogic(liveBundleBuildData);
			liveLogic.Setup(BootData, liveViews);
			liveLogic.SetSkillLogic(new SkillLogic());
			liveLogic.SetScoreLogic(ScoreLogicFactory?.Invoke(liveBundleBuildData) ?? new ScoreLogic(liveBundleBuildData));
			liveLogic.OnFinished += OnFinished;
			liveLogic.OnFailure += OnFailure;
		}

		protected override void OnRetire()
		{
			liveOutUIController?.Destroy();
			result = 2;
			PreExit(0f, 4f);
		}

		private void OnRetireByMySelf()
		{
			liveOutUIController?.Destroy();
			result = 1;
			PreExit(0f, 0f);
		}

		private void OnConfirmCancel()
		{
			liveOutUIController?.ShowPauseDialog(OnResume, OnRetireConfirm, OnRetryConfirm);
		}

		private void OnTestPlayConfirmCancel()
		{
			liveOutUIController?.ShowMusicScoreMakerTestPlayPauseDialog(OnResume, OnReturnToMusicScoreMaker, OnTestPlayRetryConfirm);
		}

		private void OnRetireConfirm()
		{
			liveOutUIController?.ShowConfirmRetireDialog(OnRetireByMySelf, OnConfirmCancel);
		}

		private void OnRetryConfirm()
		{
			liveOutUIController?.ShowConfirmRetryDialog(Retry, OnConfirmCancel);
		}

		private void OnTestPlayRetryConfirm()
		{
			liveOutUIController?.ShowConfirmRetryDialog(Retry, OnTestPlayConfirmCancel);
		}

		private System.Collections.IEnumerator ResumeCoroutine()
		{
			liveLogic?.RefreshInput();
			yield return new WaitForSeconds(3f);
			SoundManager.Instance.ResumeIngame(currentMusicTimeMs);
			state = LiveControllerState.Playing;
			LiveViewExt.Resume(liveViews, currentAudioLatencyMusicTimeMs);
			SoundManager.Instance.SetAudioSyncedUnityTimer(cueId);
			resumeCoroutine = null;
		}

		private void OnFailure()
		{
			currentMusicTimeMs = SoundManager.Instance.GetAudioSyncedUnityTimer();
		}

		protected override void OnFinished()
		{
			if (BootData?.MusicData?.IsTestPlay == true)
			{
				OnTestPlayFinished();
			}
			else
			{
				base.OnFinished();
			}
		}

		private void OnTestPlayFinished()
		{
			if (isTestPlayFinishedCalled || state == LiveControllerState.Finish)
			{
				return;
			}

			isTestPlayFinishedCalled = true;
			UpdateFullComboDataHashIfNeeded();
			state = LiveControllerState.None;
			ShowTestPlayFinishDialog();
		}

		private void ShowTestPlayFinishDialog()
		{
			ScreenLayerMusicScoreMaker.BootArg bootArg = MusicScoreMakerEntryPoint.BootData?.bootData;
			if (bootArg != null && bootArg.IsFromFullComboCheck)
			{
				if (IsFullCombo())
				{
					liveOutUIController?.ShowMusicScoreMakerFullComboSuccessDialog(OnReturnToMusicScoreMaker, Retry, OnProceedToPublish);
				}
				else
				{
					liveOutUIController?.ShowMusicScoreMakerFullComboFailedDialog(OnReturnToMusicScoreMaker, Retry);
				}
				return;
			}

			liveOutUIController?.ShowMusicScoreMakerTestPlayFinishDialog(Retry, CallPreExit);
		}

		private void OnProceedToPublish()
		{
			ScreenLayerMusicScoreMaker.BootArg bootArg = MusicScoreMakerEntryPoint.BootData?.bootData;
			if (bootArg != null)
			{
				bootArg.ShouldProceedToPublish = true;
			}
			OnReturnToMusicScoreMaker();
		}

		private void OnReturnToMusicScoreMaker()
		{
			liveOutUIController?.Destroy();
			state = LiveControllerState.Finish;
			result = 0;
			SetFinish(0f, 0f, BootData?.MusicData?.IsTestPlay == true ? 0.1f : 2f);
		}

		protected override void PreExit(float delay, float waitTime)
		{
			if (state == LiveControllerState.Finish)
			{
				return;
			}

			if (result == 0)
			{
				result = 3;
			}

			TryAppendPlayHistory();
			RaiseLiveEnded();
			state = LiveControllerState.Finish;
			SetFinish(delay, waitTime, BootData?.MusicData?.IsTestPlay == true ? 0.1f : 2f);
		}

		private void RaiseLiveEnded()
		{
			if (liveEndedRaised)
			{
				return;
			}
			liveEndedRaised = true;
			LiveEnded?.Invoke(liveLogic?.Score ?? default);
		}

		/// <summary>
		/// Co-op: leaves the live without a result (the round started without this player, or the room is gone)
		/// and goes back to the co-op room.
		/// </summary>
		public void AbortToMenu()
		{
			if (state == LiveControllerState.Finish)
			{
				return;
			}
			RaiseLiveEnded();
			coopAborted = true;
			state = LiveControllerState.Finish;
			result = 0;
			Exit();
		}

		protected override System.Collections.IEnumerator WaitBeforeMusicStart()
		{
			if (StartGate != null)
			{
				yield return StartGate();
			}
		}

		private void TryAppendPlayHistory()
		{
			if (playHistoryRecorded || result != 3 || BootData?.IsAuto == true || BootData?.MusicData?.IsTestPlay == true)
			{
				return;
			}

			playHistoryRecorded = true;
			if (BootData is FreeLiveBootData freeLiveBootData)
			{
				CapturePreviousBest();
				CustomMusicScorePlayHistoryStorage.AppendLiveResult(freeLiveBootData, liveLogic?.Score ?? default, currentMusicTimeMs, musicLength);
			}
		}

		protected override void OnExit()
		{
			if (!IsBooted)
			{
				return;
			}

			LiveViewExt.Finish3D(liveViews);
			LiveViewExt.OnUnload(liveViews);
			FreeLiveBootData freeLiveBootData = BootData as FreeLiveBootData;
			MenuScreenType? returnScreenType = freeLiveBootData?.ReturnScreenType;
			LiveResultData resultData = CreateResultData(freeLiveBootData);
			base.OnExit();
			if (returnScreenType.HasValue)
			{
				if (returnScreenType.Value == MenuScreenType.MusicScoreMakerTop || returnScreenType.Value == MenuScreenType.CommunityCharts)
				{
					// A finished or failed live shows its result first; its 次へ goes back to the list.
					MusicScoreMakerEntryPoint.BootData = null;
					LiveResultData.Last = resultData;
					SceneManager.Instance.RequestScene(resultData != null ? SceneManager.Scene.LiveResult
						: returnScreenType.Value == MenuScreenType.CommunityCharts ? SceneManager.Scene.CommunityCharts
						: SceneManager.Scene.CustomMusicScoreManager);
				}
				else if (returnScreenType.Value == MenuScreenType.CoopLive)
				{
					// A finished co-op live goes to its result; one left before it started goes back to the room.
					MusicScoreMakerEntryPoint.BootData = null;
					if (resultData != null)
					{
						LiveResultData.Last = resultData;
					}
					SceneManager.Instance.RequestScene(coopAborted ? SceneManager.Scene.CoopLive : SceneManager.Scene.LiveResult);
				}
				else
				{
					ReturnToChartEditor();
				}
			}
			else if (BootData?.MusicData?.IsTestPlay == true)
			{
				ReturnToChartEditor();
			}
		}

		/// <summary>The Result scene's data for a live that ended (finished, failed, or a co-op live); null when it has none.</summary>
		private LiveResultData CreateResultData(FreeLiveBootData bootData)
		{
			if (bootData == null || bootData.MusicData?.IsTestPlay == true)
			{
				return null;
			}
			bool coop = bootData.ReturnScreenType == MenuScreenType.CoopLive;
			if (coop ? coopAborted : result < 2)
			{
				return null;
			}
			CapturePreviousBest();
			LiveScore score = liveLogic?.Score ?? default;
			LiveResultData data = LiveResultData.Create(bootData, score, previousBest ?? -1, result == 2 || score.life <= 0);
			if (coop)
			{
				data.IsCoop = true;
				data.CoopRound = Sekai.CoopLive.CoopSession.LastPlayedRound;
			}
			return data;
		}

		private void CapturePreviousBest()
		{
			if (!previousBest.HasValue)
			{
				previousBest = LiveResultData.LoadBest(BootData as FreeLiveBootData);
			}
		}

		/// <summary>Reopens the chart editor with the state it saved before the test play.</summary>
		private static void ReturnToChartEditor()
		{
			MusicScoreMakerSceneArgs.Prepare(MusicScoreMakerEntryPoint.BootData?.bootData);
			SceneManager.Instance.RequestScene(SceneManager.Scene.MusicScoreMaker);
		}

		private void SetFinish(float delay, float waitTime, float finishWaitSeconds)
		{
			if (finishCoroutine != null)
			{
				StopCoroutine(finishCoroutine);
			}
			if (liveLogic != null)
			{
				liveLogic.OnFinished -= OnFinished;
				liveLogic.OnFailure -= OnFailure;
			}
			finishCoroutine = StartCoroutine(SetFinishCoroutine(delay, waitTime, finishWaitSeconds));
		}

		private System.Collections.IEnumerator SetFinishCoroutine(float delay, float waitTime, float finishWaitSeconds)
		{
			if (delay > 0f)
			{
				yield return new WaitForSeconds(delay);
			}
			yield return ResultAnim(waitTime, finishWaitSeconds);
		}

		private System.Collections.IEnumerator ResultAnim(float waitTime, float finishWaitSeconds)
		{
			LiveViewExt.Result(liveViews, (int)GetLiveResultAnimationType());
			if (waitTime > 0f)
			{
				yield return new WaitForSeconds(waitTime);
			}
			LiveViewExt.Finish(liveViews, finishWaitSeconds);
			if (finishWaitSeconds > 0f)
			{
				yield return new WaitForSeconds(finishWaitSeconds);
			}
			yield return null;
			Exit();
		}

		private LiveResultAnimationType GetLiveResultAnimationType()
		{
			if (BootData?.MusicData?.IsTestPlay == true || result < 2)
			{
				return LiveResultAnimationType.None;
			}

			if (result == 2)
			{
				return LiveResultAnimationType.Failure;
			}

			if (result != 3)
			{
				Debug.LogErrorFormat("Unsupported live result: {0}", result);
				return LiveResultAnimationType.None;
			}

			LiveScore score = liveLogic?.Score ?? default;
			if (score.totalComboCount == score.perfectCount)
			{
				return LiveResultAnimationType.AllPerfect;
			}
			if (score.totalComboCount == score.maxCombo)
			{
				return LiveResultAnimationType.FullCombo;
			}
			return score.life > 0 ? LiveResultAnimationType.Clear : LiveResultAnimationType.LifeZero;
		}

		private bool IsFullCombo()
		{
			LiveScore score = liveLogic?.Score ?? default;
			return score.totalComboCount > 0 && score.maxCombo >= score.totalComboCount;
		}

		private void UpdateFullComboDataHashIfNeeded()
		{
			ScreenLayerMusicScoreMaker.BootArg bootArg = MusicScoreMakerEntryPoint.BootData?.bootData;
			if (bootArg == null || !bootArg.IsFromFullComboCheck || !IsFullCombo())
			{
				return;
			}

			bootArg.FullComboDataHash = bootArg.MusicScoreDataHashAtTestPlay;
		}
	}
}
