using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Sekai.Community;
using Sekai.CoopLive.Proto;
using UnityEngine;

namespace Sekai.CoopLive
{
	/// <summary>
	/// The player's connection to a 協力ライブ room. It outlives scenes: the room screen opens it, the Core scene
	/// keeps using it during the live, and the room screen finds it again afterwards. Every event is raised on
	/// the main thread.
	/// </summary>
	public static class CoopSession
	{
		public const int Protocol = 1;

		/// <summary>Development override of the co-op server, e.g. http://127.0.0.1:50051.</summary>
		public const string ServerEnvironmentVariable = "OPENSEKAI_MULTIPLAY_URL";

		private const int ReconnectAttempts = 3;
		private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);

		private static GrpcChannel _channel;
		private static AsyncDuplexStreamingCall<ClientMessage, ServerMessage> _call;
		private static CancellationTokenSource _cts;
		private static readonly Queue<ClientMessage> Outbox = new Queue<ClientMessage>();
		private static bool _writing;
		private static int _generation;
		private static bool _leaving;
		private static string _address;

		/// <summary>The room as the server last described it; null when not in a room.</summary>
		public static Room Room { get; private set; }

		public static string MyUserId { get; private set; } = string.Empty;

		public static bool IsConnected => _call != null;

		/// <summary>Server clock minus local clock, from the last room snapshot (ms).</summary>
		public static long ClockOffsetMs { get; private set; }

		/// <summary>The latest statuses of the players in the live.</summary>
		public static LiveStatuses Statuses { get; private set; }

		/// <summary>The round this player last started a live in (0: none).</summary>
		public static int LastPlayedRound { get; internal set; }

		public static event Action<Room> RoomChanged;
		public static event Action<int> LiveStarted;
		public static event Action<LiveStatuses> StatusesChanged;
		public static event Action<FeverKind> FeverDecided;
		public static event Action<StampSent> StampReceived;
		public static event Action<Notice> NoticeReceived;
		/// <summary>The session ended: left, kicked, the room closed, or the connection was lost for good.</summary>
		public static event Action<string, string> Closed;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			_channel = null;
			_call = null;
			_cts = null;
			Outbox.Clear();
			_writing = false;
			_leaving = false;
			Room = null;
			Statuses = null;
			MyUserId = string.Empty;
			LastPlayedRound = 0;
			RoomChanged = null;
			LiveStarted = null;
			StatusesChanged = null;
			FeverDecided = null;
			StampReceived = null;
			NoticeReceived = null;
			Closed = null;
		}

		public static Member Me => Find(MyUserId);

		public static Member Find(string userId)
		{
			if (Room == null || string.IsNullOrEmpty(userId))
			{
				return null;
			}
			foreach (Member member in Room.Members)
			{
				if (member.UserId == userId)
				{
					return member;
				}
			}
			return null;
		}

		public static bool IsHost => Room != null && Room.HostId == MyUserId;

		/// <summary>Milliseconds left in the current phase, or -1 when it has no deadline.</summary>
		public static long RemainingMs
		{
			get
			{
				if (Room == null || Room.DeadlineMs <= 0)
				{
					return -1;
				}
				long serverNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ClockOffsetMs;
				return Math.Max(0, Room.DeadlineMs - serverNow);
			}
		}

		/// <summary>Where the co-op server is: the environment override, else the site's /api/v1/meta.</summary>
		public static async UniTask<string> ResolveAddressAsync(CancellationToken ct)
		{
			string fromEnvironment = Environment.GetEnvironmentVariable(ServerEnvironmentVariable);
			if (!string.IsNullOrWhiteSpace(fromEnvironment))
			{
				return fromEnvironment.Trim().TrimEnd('/');
			}
			CommunityMeta meta = await CommunityClient.GetMetaAsync(ct);
			if (meta?.Multiplay == null || string.IsNullOrWhiteSpace(meta.Multiplay.Url))
			{
				throw new CoopException("NOT_AVAILABLE", "这个社区服务器没有开放协力ライブ。");
			}
			if (meta.Multiplay.Protocol > Protocol)
			{
				throw new CoopException("UPDATE_REQUIRED", "协力ライブ已更新，请更新游戏。");
			}
			return meta.Multiplay.Url.Trim().TrimEnd('/');
		}

		/// <summary>フリールーム: a waiting public room, or a new one.</summary>
		public static UniTask QuickMatchAsync(CancellationToken ct) =>
			ConnectAsync(new Join { QuickMatch = new QuickMatch(), Protocol = Protocol }, ct);

		/// <summary>A new private room (5-digit ID).</summary>
		public static UniTask CreatePrivateAsync(CancellationToken ct) =>
			ConnectAsync(new Join { CreatePrivate = new CreatePrivate(), Protocol = Protocol }, ct);

		public static UniTask JoinAsync(string roomCode, CancellationToken ct) =>
			ConnectAsync(new Join { RoomCode = roomCode, Protocol = Protocol }, ct);

		/// <summary>Opens the stream and joins; returns once the server has put the player in a room.</summary>
		public static async UniTask ConnectAsync(Join join, CancellationToken ct)
		{
			if (IsConnected)
			{
				throw new CoopException("ALREADY_IN_ROOM", "已经在房间里了。");
			}
			if (!CommunityAccount.IsSignedIn)
			{
				throw new CoopException("AUTH_REQUIRED", "请先在设置 › Additionals 里登录 OurTaiko 账号。");
			}
			_address ??= await ResolveAddressAsync(ct);
			_leaving = false;
			try
			{
				await OpenAsync(join, forceNewSession: false, ct);
			}
			catch (RpcException exception) when (exception.StatusCode == StatusCode.Unauthenticated)
			{
				// The saved game session may have been revoked: trade for a new one once.
				await OpenAsync(join, forceNewSession: true, ct);
			}
		}

		private static async UniTask OpenAsync(Join join, bool forceNewSession, CancellationToken ct)
		{
			string session = await CommunityAccount.GetSessionAsync(ct, forceNewSession);
			if (string.IsNullOrEmpty(session))
			{
				throw new CoopException("AUTH_REQUIRED", "请先登录 OurTaiko 账号。");
			}

			CloseTransport();
			int generation = ++_generation;
			_cts = new CancellationTokenSource();
			_channel = CoopTransport.CreateChannel(_address);
			_call = CoopTransport.Connect(_channel, session, _cts.Token);
			Outbox.Clear();
			Send(new ClientMessage { Join = join });

			// The first answers are welcome and the room (or closed / an error status).
			var joined = new UniTaskCompletionSource();
			ReadLoopAsync(generation, joined).Forget();
			using (ct.Register(() => joined.TrySetCanceled()))
			{
				try
				{
					await joined.Task.Timeout(TimeSpan.FromSeconds(20));
				}
				catch (TimeoutException)
				{
					CloseTransport();
					throw new CoopException("TIMEOUT", "连接协力ライブ服务器超时。");
				}
				catch
				{
					CloseTransport();
					throw;
				}
			}
			PingLoopAsync(generation).Forget();
		}

		private static async UniTaskVoid ReadLoopAsync(int generation, UniTaskCompletionSource joined)
		{
			AsyncDuplexStreamingCall<ClientMessage, ServerMessage> call = _call;
			CancellationToken token = _cts.Token;
			Exception failure = null;
			try
			{
				while (await call.ResponseStream.MoveNext(token))
				{
					await UniTask.SwitchToMainThread();
					if (generation != _generation)
					{
						return;
					}
					ServerMessage message = call.ResponseStream.Current;
					if (message.KindCase == ServerMessage.KindOneofCase.Closed)
					{
						joined.TrySetException(new CoopException(message.Closed.Code, message.Closed.Message));
						EndSession(message.Closed.Code, message.Closed.Message);
						return;
					}
					Dispatch(message);
					if (message.KindCase == ServerMessage.KindOneofCase.Room)
					{
						joined.TrySetResult();
					}
				}
			}
			catch (Exception exception)
			{
				failure = exception;
			}

			await UniTask.SwitchToMainThread();
			if (generation != _generation)
			{
				return;
			}
			if (joined.TrySetException(failure ?? new CoopException("CLOSED", "服务器关闭了连接。")))
			{
				// Failed while joining; ConnectAsync reports it.
				return;
			}
			if (_leaving)
			{
				EndSession("LEFT", "已离开房间。");
				return;
			}
			Debug.LogWarning($"Co-op live connection lost: {failure?.Message ?? "stream ended"}");
			await ReconnectAsync();
		}

		/// <summary>The stream dropped: the server keeps the player's place for 10 s, so join the room again.</summary>
		private static async UniTask ReconnectAsync()
		{
			string code = Room?.Code;
			CloseTransport();
			for (int attempt = 1; attempt <= ReconnectAttempts && !string.IsNullOrEmpty(code) && !_leaving; attempt++)
			{
				await UniTask.Delay(TimeSpan.FromSeconds(attempt == 1 ? 0.5 : 2), ignoreTimeScale: true);
				try
				{
					await OpenAsync(new Join { RoomCode = code, Protocol = Protocol }, forceNewSession: false, CancellationToken.None);
					NoticeReceived?.Invoke(new Notice { Code = "RECONNECTED", Message = "已重新连接。" });
					return;
				}
				catch (CoopException exception) when (exception.Code == "ROOM_NOT_FOUND" || exception.Code == "ROOM_BUSY" || exception.Code == "ROOM_CLOSED")
				{
					break;
				}
				catch (Exception exception)
				{
					Debug.LogWarning($"Co-op live reconnect {attempt} failed: {exception.Message}");
				}
			}
			EndSession("CONNECTION_LOST", "与协力ライブ服务器的连接已断开。");
		}

		private static async UniTaskVoid PingLoopAsync(int generation)
		{
			while (generation == _generation && IsConnected)
			{
				await UniTask.Delay(PingInterval, ignoreTimeScale: true);
				if (generation != _generation || !IsConnected)
				{
					return;
				}
				Send(new ClientMessage { Ping = new Proto.Ping { ClientTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() } });
			}
		}

		private static void Dispatch(ServerMessage message)
		{
			switch (message.KindCase)
			{
				case ServerMessage.KindOneofCase.Welcome:
					MyUserId = message.Welcome.UserId;
					break;
				case ServerMessage.KindOneofCase.Room:
					Room = message.Room;
					ClockOffsetMs = Room.ServerTimeMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
					RoomChanged?.Invoke(Room);
					break;
				case ServerMessage.KindOneofCase.StartLive:
					LiveStarted?.Invoke(message.StartLive.Round);
					break;
				case ServerMessage.KindOneofCase.Statuses:
					Statuses = message.Statuses;
					StatusesChanged?.Invoke(Statuses);
					break;
				case ServerMessage.KindOneofCase.Fever:
					FeverDecided?.Invoke(message.Fever.Kind);
					break;
				case ServerMessage.KindOneofCase.Stamp:
					StampReceived?.Invoke(message.Stamp);
					break;
				case ServerMessage.KindOneofCase.Notice:
					NoticeReceived?.Invoke(message.Notice);
					break;
			}
		}

		/// <summary>Queues a message; they are written one at a time, in order.</summary>
		public static void Send(ClientMessage message)
		{
			if (_call == null)
			{
				return;
			}
			Outbox.Enqueue(message);
			if (!_writing)
			{
				WriteLoopAsync(_generation).Forget();
			}
		}

		private static async UniTaskVoid WriteLoopAsync(int generation)
		{
			_writing = true;
			try
			{
				while (Outbox.Count > 0 && generation == _generation && _call != null)
				{
					ClientMessage next = Outbox.Dequeue();
					await _call.RequestStream.WriteAsync(next);
				}
			}
			catch (Exception exception)
			{
				// The read loop notices the broken stream and reconnects.
				Debug.LogWarning($"Co-op live send failed: {exception.Message}");
				Outbox.Clear();
			}
			finally
			{
				if (generation == _generation)
				{
					_writing = false;
				}
			}
		}

		public static void LockMembers() => Send(new ClientMessage { LockMembers = new LockMembers() });
		public static void OpenRoom() => Send(new ClientMessage { OpenRoom = new OpenRoom() });
		public static void Kick(string userId) => Send(new ClientMessage { Kick = new Kick { UserId = userId } });
		public static void PickSong(string chartIdOrEmpty) => Send(new ClientMessage { PickSong = new PickSong { Chart = chartIdOrEmpty ?? string.Empty } });
		public static void PickDifficulty(string chartId) => Send(new ClientMessage { PickDifficulty = new PickDifficulty { ChartId = chartId } });
		public static void SetReady(bool ready) => Send(new ClientMessage { SetReady = new SetReady { Ready = ready } });
		public static void Loaded(bool ok, string error = "") => Send(new ClientMessage { Loaded = new Loaded { Ok = ok, Error = error ?? string.Empty } });
		public static void SendStatus(PlayerStatus status) => Send(new ClientMessage { Status = status });
		public static void SendFeverReport(FeverReport report) => Send(new ClientMessage { FeverReport = report });
		public static void Finish(PlayerStatus final, bool failed) => Send(new ClientMessage { Finish = new FinishLive { Final = final, Failed = failed } });
		public static void SendStamp(int stampId) => Send(new ClientMessage { Stamp = new SendStamp { StampId = stampId } });

		/// <summary>Leaves the room and closes the connection.</summary>
		public static async UniTask LeaveAsync()
		{
			if (!IsConnected)
			{
				EndSession("LEFT", "已离开房间。");
				return;
			}
			_leaving = true;
			Send(new ClientMessage { Leave = new Leave() });
			int generation = _generation;
			// The server answers with closed; do not wait for long.
			await UniTask.WaitUntil(() => generation != _generation || !IsConnected).TimeoutWithoutException(TimeSpan.FromSeconds(2));
			if (generation == _generation)
			{
				EndSession("LEFT", "已离开房间。");
			}
		}

		private static void EndSession(string code, string message)
		{
			_generation++;
			CloseTransport();
			bool hadRoom = Room != null;
			Room = null;
			Statuses = null;
			if (hadRoom || code != "LEFT")
			{
				Closed?.Invoke(code, message);
			}
		}

		private static void CloseTransport()
		{
			try
			{
				_cts?.Cancel();
			}
			catch (ObjectDisposedException)
			{
			}
			try
			{
				_call?.Dispose();
				_channel?.Dispose();
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"Co-op live close: {exception.Message}");
			}
			_cts?.Dispose();
			_cts = null;
			_call = null;
			_channel = null;
			Outbox.Clear();
			_writing = false;
		}
	}
}
