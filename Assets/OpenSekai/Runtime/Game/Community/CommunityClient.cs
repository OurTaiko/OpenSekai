using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Sekai.Community
{
	/// <summary>
	/// Calls the community chart site, SekaiFanmade (https://sekai.ourtaiko.org, repository SekaiFanmade).
	/// Reads are public; likes and the マイリスト need a signed-in <see cref="CommunityAccount"/>.
	/// </summary>
	public static class CommunityClient
	{
		public const string DefaultServer = "https://sekai.ourtaiko.org";

		/// <summary>Development override, e.g. http://127.0.0.1:3000 for a local SekaiFanmade.</summary>
		public const string ServerEnvironmentVariable = "OPENSEKAI_COMMUNITY_URL";

		private static string _server;

		private static CommunityMeta _meta;

		public static string Server => _server ??= ResolveServer();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			_server = null;
			_meta = null;
		}

		private static string ResolveServer()
		{
			string fromEnvironment = Environment.GetEnvironmentVariable(ServerEnvironmentVariable);
			return string.IsNullOrWhiteSpace(fromEnvironment) ? DefaultServer : fromEnvironment.Trim().TrimEnd('/');
		}

		/// <summary>The web page of a chart, for sharing or opening in a browser.</summary>
		public static string ChartPageUrl(string id) => Server + "/charts/" + Uri.EscapeDataString(id);


		public static async UniTask<CommunityMeta> GetMetaAsync(CancellationToken ct = default)
		{
			if (_meta != null)
			{
				return _meta;
			}
			CommunityMeta meta = await CommunityHttp.SendAsync<CommunityMeta>("GET", Server + "/api/v1/meta", null, null, ct);
			_meta = meta ?? new CommunityMeta();
			return _meta;
		}

		public static UniTask<CommunityChartPage> ListAsync(CommunityQuery query, CancellationToken ct = default)
		{
			return SendWithOptionalSessionAsync<CommunityChartPage>("GET", Server + "/api/v1/charts" + QueryString(query), ct);
		}

		/// <summary>The signed-in player's マイリスト.</summary>
		public static UniTask<CommunityChartPage> ListBookmarksAsync(CommunityQuery query, CancellationToken ct = default)
		{
			return SendWithSessionAsync<CommunityChartPage>("GET", Server + "/api/v1/me/bookmarks" + QueryString(query), null, ct);
		}

		/// <summary>One chart by its id or its number (譜面ID検索), or null when it does not exist.</summary>
		public static async UniTask<CommunityChart> GetAsync(string idOrNumber, CancellationToken ct = default)
		{
			try
			{
				return await SendWithOptionalSessionAsync<CommunityChart>("GET", Server + "/api/v1/charts/" + Uri.EscapeDataString(idOrNumber), ct);
			}
			catch (CommunityException exception) when (exception.Status == 404)
			{
				return null;
			}
		}

		public static async UniTask<List<CommunityTagCount>> ListTagsAsync(CancellationToken ct = default)
		{
			TagsReply reply = await CommunityHttp.SendAsync<TagsReply>("GET", Server + "/api/v1/tags", null, null, ct);
			return reply?.Tags ?? new List<CommunityTagCount>();
		}

		/// <summary>
		/// Uploads a chart package (a ZIP the manager exported) as the signed-in player. With
		/// <paramref name="replaceId"/> it becomes the new version of that chart, which must be the player's own.
		/// Returns the chart's id.
		/// </summary>
		public static async UniTask<string> UploadChartAsync(string zipPath, string replaceId, IProgress<float> progress, CancellationToken ct = default)
		{
			string url = Server + "/api/v1/uploads" + (string.IsNullOrEmpty(replaceId) ? string.Empty : "?chart=" + Uri.EscapeDataString(replaceId));
			string session = await CommunityAccount.GetSessionAsync(ct);
			if (session == null)
			{
				throw new CommunityException("AUTH_REQUIRED", L10n.Get("Community", "error.signInRequired"), 401);
			}
			string text;
			try
			{
				text = await CommunityHttp.UploadFileAsync(url, session, zipPath, "application/zip", progress, ct);
			}
			catch (CommunityException exception) when (exception.Status == 401)
			{
				CommunityAccount.DropSession();
				session = await CommunityAccount.GetSessionAsync(ct, forceNew: true);
				if (session == null)
				{
					throw;
				}
				text = await CommunityHttp.UploadFileAsync(url, session, zipPath, "application/zip", progress, ct);
			}
			UploadReply reply = null;
			try
			{
				reply = JsonConvert.DeserializeObject<UploadReply>(text, CommunityHttp.JsonSettings);
			}
			catch (JsonException)
			{
			}
			if (string.IsNullOrEmpty(reply?.Id))
			{
				throw new CommunityException("RESPONSE_INVALID", L10n.Get("Community", "error.responseInvalid"));
			}
			return reply.Id;
		}

		/// <summary>Votes on a chart: 1 up, -1 down, 0 takes the vote back.</summary>
		public static async UniTask<VoteReply> SetVoteAsync(string id, int value, CancellationToken ct = default)
		{
			return await SendWithSessionAsync<VoteReply>("PUT", Server + "/api/v1/charts/" + id + "/vote", new { value }, ct)
				?? throw new CommunityException("RESPONSE_INVALID", L10n.Get("Community", "error.responseInvalid"));
		}

		public static async UniTask SetBookmarkAsync(string id, bool saved, CancellationToken ct = default)
		{
			await SendWithSessionAsync<object>(saved ? "PUT" : "DELETE", Server + "/api/v1/charts/" + id + "/bookmark", null, ct);
		}

		/// <summary>Counts a download on the site (the game fetches the parts from the CDN itself).</summary>
		public static async UniTask ReportDownloadAsync(string id, CancellationToken ct = default)
		{
			await CommunityHttp.SendRawAsync("POST", Server + "/api/v1/charts/" + Uri.EscapeDataString(id) + "/downloads", null, "{}", ct);
		}

		/// <summary>Reports a finished play. Counted once per player (or address) per chart per hour.</summary>
		public static async UniTask ReportPlayAsync(string id, CancellationToken ct = default)
		{
			string session = null;
			try
			{
				session = await CommunityAccount.GetSessionAsync(ct);
			}
			catch (CommunityException)
			{
				// Still count it anonymously.
			}
			await CommunityHttp.SendRawAsync("POST", Server + "/api/v1/charts/" + id + "/plays", session, "{}", ct);
		}

		private static string QueryString(CommunityQuery query)
		{
			List<string> parts = new List<string>();
			void Add(string key, string value)
			{
				if (!string.IsNullOrWhiteSpace(value))
				{
					parts.Add(key + "=" + Uri.EscapeDataString(value.Trim()));
				}
			}
			Add("sort", query?.Sort);
			Add("difficulty", query?.Difficulty);
			Add("q", query?.Text);
			Add("tag", query?.Tag);
			Add("seed", query?.Seed);
			if (query != null && query.Page > 1)
			{
				parts.Add("page=" + query.Page);
			}
			return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
		}

		/// <summary>Sends with the session when signed in (so items carry liked / bookmarked), anonymously otherwise.</summary>
		private static async UniTask<T> SendWithOptionalSessionAsync<T>(string method, string url, CancellationToken ct)
		{
			string session = null;
			if (CommunityAccount.IsSignedIn)
			{
				try
				{
					session = await CommunityAccount.GetSessionAsync(ct);
				}
				catch (CommunityException exception)
				{
					Debug.LogWarning("[Community] Browsing without a session: " + exception.Message);
				}
			}
			try
			{
				return await CommunityHttp.SendAsync<T>(method, url, session, null, ct);
			}
			catch (CommunityException exception) when (exception.Status == 401 && session != null)
			{
				CommunityAccount.DropSession();
				return await CommunityHttp.SendAsync<T>(method, url, null, null, ct);
			}
		}

		/// <summary>Sends with the session; trades a new one once if the site rejects it.</summary>
		private static async UniTask<T> SendWithSessionAsync<T>(string method, string url, object body, CancellationToken ct)
		{
			string session = await CommunityAccount.GetSessionAsync(ct);
			if (session == null)
			{
				throw new CommunityException("AUTH_REQUIRED", L10n.Get("Community", "error.signInRequired"), 401);
			}
			try
			{
				return await CommunityHttp.SendAsync<T>(method, url, session, body, ct);
			}
			catch (CommunityException exception) when (exception.Status == 401)
			{
				CommunityAccount.DropSession();
				session = await CommunityAccount.GetSessionAsync(ct, forceNew: true);
				if (session == null)
				{
					throw;
				}
				return await CommunityHttp.SendAsync<T>(method, url, session, body, ct);
			}
		}

		private sealed class TagsReply
		{
			public List<CommunityTagCount> Tags;
		}

		private sealed class UploadReply
		{
			public string Id;
		}

		public sealed class VoteReply
		{
			public int Score;
			public int Upvotes;
			public int Downvotes;
			public int MyVote;
		}
	}
}
