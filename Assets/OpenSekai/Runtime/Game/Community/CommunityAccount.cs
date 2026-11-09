// The account store and the account-center calls follow OurTaikoPlay (Runtime/Online/OurTaikoAccounts.cs, GPLv3).
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenSekai.Platform;
using UnityEngine;

namespace Sekai.Community
{
	/// <summary>
	/// The player's OurTaiko account on this device. The password only ever goes to the account center
	/// (OurTaiko SSO, /api/v1/game/account/login); it answers with an account token that slides forward on
	/// each use and expires after 30 days without use. That token is traded at the account center for a
	/// game session of the community site (/api/v1/game/tokens, 7 days), which the site accepts as a Bearer token.
	/// Both are kept in <c>community_account.dat</c> under <see cref="GamePaths.Root"/>, encrypted with a key
	/// of this device, so a copy taken to another device cannot be read.
	/// </summary>
	public static class CommunityAccount
	{
		public const string FileName = "community_account.dat";

		private sealed class Stored
		{
			public string AccountToken = string.Empty;
			public string UserId = string.Empty;
			public string Username = string.Empty;
			public string Nickname = string.Empty;
			public string AvatarUrl = string.Empty;
			public string SessionToken = string.Empty;
			public DateTime SessionExpiresAt;
			public string SessionService = string.Empty;
			/// <summary>The community server it was signed in on (empty in files written before this field).</summary>
			public string Server = string.Empty;
		}

		private sealed class AccountReply
		{
			public string Token;
			public DateTime ExpiresAt;
			public CommunityUser User;
			public string AccessToken;
			public int ExpiresIn;
		}

		private static Stored _stored;

		/// <summary>What a different server sees: signed out. Never saved.</summary>
		private static readonly Stored SignedOut = new Stored();

		/// <summary>Raised on the main thread when the player signs in or out.</summary>
		public static event Action Changed;

		public static bool IsSignedIn => Current().AccountToken.Length > 0;

		public static string Nickname => Current().Nickname;

		public static string Username => Current().Username;

		public static string UserId => Current().UserId;

		/// <summary>
		/// The remembered account if it belongs to the server the game talks to now. Pointing the game at another
		/// server (a local SekaiFanmade in development) shows it as signed out but leaves the file alone.
		/// </summary>
		private static Stored Current()
		{
			Stored stored = Load();
			string server = string.IsNullOrEmpty(stored.Server) ? CommunityClient.DefaultServer : stored.Server;
			return stored.AccountToken.Length > 0 && string.Equals(server.TrimEnd('/'), CommunityClient.Server, StringComparison.OrdinalIgnoreCase)
				? stored
				: SignedOut;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			_stored = null;
			Changed = null;
		}

		/// <summary>Signs in with an OurTaiko username and password.</summary>
		public static async UniTask LoginAsync(string username, string password, CancellationToken ct = default)
		{
			CommunityMeta meta = await CommunityClient.GetMetaAsync(ct);
			object body = new { username = username ?? string.Empty, password = password ?? string.Empty, deviceName = DeviceName() };
			AccountReply reply;
			try
			{
				reply = await CommunityHttp.SendAsync<AccountReply>("POST", Issuer(meta) + "/api/v1/game/account/login", null, body, ct);
			}
			catch (CommunityException exception) when (exception.Code == "LOGIN_INVALID" || exception.Status == 401)
			{
				throw new CommunityException("LOGIN_INVALID", "用户名或密码错误，或账号还没有激活。", exception.Status);
			}
			catch (CommunityException exception) when (exception.Code == "RATE_LIMITED" || exception.Status == 429)
			{
				throw new CommunityException("RATE_LIMITED", "登录尝试太多次，请过一会儿再试。", exception.Status);
			}
			if (!IsHex(reply?.Token, 64) || reply.User == null || !IsHex(reply.User.Id, 32))
			{
				throw new CommunityException("RESPONSE_INVALID", "账号中心返回的数据无法识别。");
			}

			_stored = new Stored
			{
				AccountToken = reply.Token,
				UserId = reply.User.Id,
				Nickname = reply.User.Nickname ?? string.Empty,
				AvatarUrl = reply.User.AvatarUrl ?? string.Empty,
				Server = CommunityClient.Server,
			};
			_stored.Username = username?.Trim() ?? string.Empty;
			Save();
			Changed?.Invoke();
			// Trade right away so a later like or マイリスト call does not wait for it.
			try
			{
				await GetSessionAsync(ct);
			}
			catch (CommunityException exception)
			{
				Debug.LogWarning("[Community] Could not open a site session after login: " + exception.Message);
			}
		}

		/// <summary>Signs out here and at the account center (best effort).</summary>
		public static async UniTask LogoutAsync(CancellationToken ct = default)
		{
			Stored stored = Current();
			string token = stored.AccountToken;
			if (string.IsNullOrEmpty(token))
			{
				return;
			}
			_stored = new Stored();
			Save();
			Changed?.Invoke();
			if (string.IsNullOrEmpty(token))
			{
				return;
			}
			try
			{
				CommunityMeta meta = await CommunityClient.GetMetaAsync(ct);
				await CommunityHttp.SendRawAsync("POST", Issuer(meta) + "/api/v1/game/account/logout", token, "{}", ct);
			}
			catch (CommunityException exception)
			{
				Debug.LogWarning("[Community] Logout at the account center failed: " + exception.Message);
			}
		}

		/// <summary>Checks the remembered account and refreshes the nickname. Signs out locally if the center forgot it.</summary>
		public static async UniTask RefreshAsync(CancellationToken ct = default)
		{
			Stored stored = Current();
			if (stored.AccountToken.Length == 0)
			{
				return;
			}
			try
			{
				CommunityMeta meta = await CommunityClient.GetMetaAsync(ct);
				AccountReply reply = await CommunityHttp.SendAsync<AccountReply>("GET", Issuer(meta) + "/api/v1/game/account/session", stored.AccountToken, null, ct);
				if (reply?.User != null)
				{
					stored.Nickname = reply.User.Nickname ?? stored.Nickname;
					stored.AvatarUrl = reply.User.AvatarUrl ?? stored.AvatarUrl;
					Save();
					Changed?.Invoke();
				}
			}
			catch (CommunityException exception) when (exception.Status == 401)
			{
				ForgetLocally();
			}
		}

		/// <summary>
		/// A game session of the community site, trading for a new one when there is none or it is about to
		/// expire. Null when signed out. Throws CommunityException when the account center cannot be reached.
		/// </summary>
		public static async UniTask<string> GetSessionAsync(CancellationToken ct = default, bool forceNew = false)
		{
			Stored stored = Current();
			if (stored.AccountToken.Length == 0)
			{
				return null;
			}
			CommunityMeta meta = await CommunityClient.GetMetaAsync(ct);
			bool usable = !forceNew
				&& IsHex(stored.SessionToken, 64)
				&& stored.SessionService == meta.Sso.Service
				&& stored.SessionExpiresAt > DateTime.UtcNow.AddMinutes(10);
			if (usable)
			{
				return stored.SessionToken;
			}

			AccountReply reply;
			try
			{
				reply = await CommunityHttp.SendAsync<AccountReply>(
					"POST", Issuer(meta) + "/api/v1/game/tokens", stored.AccountToken, new { service = meta.Sso.Service }, ct);
			}
			catch (CommunityException exception) when (exception.Status == 401)
			{
				ForgetLocally();
				throw new CommunityException("AUTH_REQUIRED", "登录已过期，请重新登录。", 401);
			}
			if (!IsHex(reply?.AccessToken, 64))
			{
				throw new CommunityException("RESPONSE_INVALID", "账号中心返回的数据无法识别。");
			}
			stored.SessionToken = reply.AccessToken;
			stored.SessionService = meta.Sso.Service;
			stored.SessionExpiresAt = DateTime.UtcNow.AddSeconds(reply.ExpiresIn > 0 ? reply.ExpiresIn : 3600);
			if (reply.User != null)
			{
				stored.Nickname = reply.User.Nickname ?? stored.Nickname;
			}
			Save();
			return stored.SessionToken;
		}

		/// <summary>The site said the session is no longer valid: drop it so the next call trades again.</summary>
		internal static void DropSession()
		{
			Stored stored = Current();
			if (stored.AccountToken.Length == 0)
			{
				return;
			}
			stored.SessionToken = string.Empty;
			stored.SessionExpiresAt = default;
			Save();
		}

		public static string RegisterUrl(CommunityMeta meta) => Issuer(meta) + "/register/";

		public static string AccountCenterUrl(CommunityMeta meta) => Issuer(meta) + "/profile/";

		private static void ForgetLocally()
		{
			if (!IsSignedIn)
			{
				return;
			}
			_stored = new Stored();
			Save();
			Changed?.Invoke();
		}

		private static string Issuer(CommunityMeta meta)
		{
			return (meta?.Sso?.Issuer ?? "https://sso.ourtaiko.org").TrimEnd('/');
		}

		private static string DeviceName()
		{
			string name = SystemInfo.deviceName;
			if (string.IsNullOrWhiteSpace(name) || name == SystemInfo.unsupportedIdentifier)
			{
				name = SystemInfo.deviceModel;
			}
			return ("OpenSekai · " + name).Substring(0, Math.Min(80, ("OpenSekai · " + name).Length));
		}

		private static bool IsHex(string value, int length)
		{
			return value != null && value.Length == length && value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
		}

		/* ---------- encrypted file ---------- */

		private static string FilePath => Path.Combine(GamePaths.Root, FileName);

		private static Stored Load()
		{
			if (_stored != null)
			{
				return _stored;
			}
			_stored = new Stored();
			try
			{
				if (File.Exists(FilePath))
				{
					_stored = JsonConvert.DeserializeObject<Stored>(Decrypt(File.ReadAllBytes(FilePath))) ?? new Stored();
				}
			}
			catch (Exception exception)
			{
				// A foreign or damaged file is no account: the player signs in again.
				Debug.LogWarning("[Community] Ignoring unreadable account file: " + exception.Message);
				_stored = new Stored();
			}
			return _stored;
		}

		private static void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				if (_stored == null || _stored.AccountToken.Length == 0)
				{
					if (File.Exists(FilePath))
					{
						File.Delete(FilePath);
					}
					return;
				}
				string temporary = FilePath + ".tmp";
				File.WriteAllBytes(temporary, Encrypt(JsonConvert.SerializeObject(_stored)));
				if (File.Exists(FilePath))
				{
					File.Delete(FilePath);
				}
				File.Move(temporary, FilePath);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Community] Could not save the account file: " + exception.Message);
			}
		}

		private static string DeviceSecret() => SystemInfo.deviceUniqueIdentifier + "\n" + Application.identifier;

		private static byte[] Key(string purpose)
		{
			using SHA256 sha = SHA256.Create();
			return sha.ComputeHash(Encoding.UTF8.GetBytes("OpenSekai community account " + purpose + "\n" + DeviceSecret()));
		}

		private static byte[] Encrypt(string text)
		{
			using Aes aes = Aes.Create();
			aes.Key = Key("encryption");
			aes.GenerateIV();
			using ICryptoTransform encryptor = aes.CreateEncryptor();
			byte[] plain = Encoding.UTF8.GetBytes(text);
			byte[] body = aes.IV.Concat(encryptor.TransformFinalBlock(plain, 0, plain.Length)).ToArray();
			using HMACSHA256 hmac = new HMACSHA256(Key("authentication"));
			return body.Concat(hmac.ComputeHash(body)).ToArray();
		}

		private static string Decrypt(byte[] data)
		{
			if (data.Length < 16 + 16 + 32)
			{
				throw new InvalidDataException("too short");
			}
			byte[] body = data.Take(data.Length - 32).ToArray();
			byte[] mac = data.Skip(data.Length - 32).ToArray();
			using HMACSHA256 hmac = new HMACSHA256(Key("authentication"));
			byte[] expected = hmac.ComputeHash(body);
			int difference = 0;
			for (int i = 0; i < 32; i++)
			{
				difference |= expected[i] ^ mac[i];
			}
			if (difference != 0)
			{
				throw new InvalidDataException("authentication failed");
			}
			using Aes aes = Aes.Create();
			aes.Key = Key("encryption");
			aes.IV = body.Take(16).ToArray();
			using ICryptoTransform decryptor = aes.CreateDecryptor();
			return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(body, 16, body.Length - 16));
		}
	}
}
