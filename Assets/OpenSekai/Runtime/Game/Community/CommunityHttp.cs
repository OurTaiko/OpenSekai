using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace Sekai.Community
{
	/// <summary>JSON over UnityWebRequest, with the error shapes of the community site and OurTaiko SSO.</summary>
	internal static class CommunityHttp
	{
		private const int TimeoutSeconds = 20;
		// A large package over a slow line, plus the server's check of it.
		private const int UploadTimeoutSeconds = 600;

		private const int MaxResponseBytes = 4 * 1024 * 1024;

		public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			DateTimeZoneHandling = DateTimeZoneHandling.Utc,
			MissingMemberHandling = MissingMemberHandling.Ignore,
		};

		public static async UniTask<T> SendAsync<T>(string method, string url, string bearer, object body, CancellationToken ct)
		{
			string text = await SendRawAsync(method, url, bearer, body, ct);
			try
			{
				return string.IsNullOrEmpty(text) ? default : JsonConvert.DeserializeObject<T>(text, JsonSettings);
			}
			catch (JsonException exception)
			{
				throw new CommunityException("RESPONSE_INVALID", "服务器返回的数据无法识别。", 0, exception);
			}
		}

		public static async UniTask<string> SendRawAsync(string method, string url, string bearer, object body, CancellationToken ct)
		{
			using UnityWebRequest request = new UnityWebRequest(url, method);
			request.downloadHandler = new DownloadHandlerBuffer();
			request.timeout = TimeoutSeconds;
			request.SetRequestHeader("Accept", "application/json");
			if (body != null)
			{
				string json = body is string s ? s : JsonConvert.SerializeObject(body, JsonSettings);
				request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
				request.SetRequestHeader("Content-Type", "application/json");
			}
			if (!string.IsNullOrEmpty(bearer))
			{
				request.SetRequestHeader("Authorization", "Bearer " + bearer);
			}

			try
			{
				await request.SendWebRequest().WithCancellation(ct);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (UnityWebRequestException exception)
			{
				throw ToException(exception.ResponseCode, exception.Text, exception.Error);
			}

			if (request.result != UnityWebRequest.Result.Success)
			{
				throw ToException(request.responseCode, request.downloadHandler?.text, request.error);
			}
			if (request.downloadedBytes > MaxResponseBytes)
			{
				throw new CommunityException("RESPONSE_TOO_LARGE", "服务器返回的数据太大。");
			}
			return request.downloadHandler.text;
		}

		/// <summary>
		/// PUTs a file as the raw request body (the chart upload) and returns the response text. Progress is the
		/// share of the file sent; the server checks the package after that, so the last step can take a while.
		/// </summary>
		public static async UniTask<string> UploadFileAsync(string url, string bearer, string path, string contentType, IProgress<float> progress, CancellationToken ct)
		{
			using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT);
			request.uploadHandler = new UploadHandlerFile(path) { contentType = contentType };
			request.downloadHandler = new DownloadHandlerBuffer();
			request.timeout = UploadTimeoutSeconds;
			request.SetRequestHeader("Accept", "application/json");
			request.SetRequestHeader("Content-Type", contentType);
			if (!string.IsNullOrEmpty(bearer))
			{
				request.SetRequestHeader("Authorization", "Bearer " + bearer);
			}

			UnityWebRequestAsyncOperation operation = request.SendWebRequest();
			try
			{
				while (!operation.isDone)
				{
					progress?.Report(request.uploadProgress);
					await UniTask.Yield(PlayerLoopTiming.Update, ct);
				}
			}
			catch (OperationCanceledException)
			{
				request.Abort();
				throw;
			}

			if (request.result != UnityWebRequest.Result.Success)
			{
				throw ToException(request.responseCode, request.downloadHandler?.text, request.error);
			}
			progress?.Report(1f);
			return request.downloadHandler.text;
		}

		/// <summary>Turns a failed request into a CommunityException with the server's message when there is one.</summary>
		private static CommunityException ToException(long status, string body, string transportError)
		{
			if (status == 0)
			{
				return new CommunityException("NETWORK_ERROR", "无法连接到服务器，请检查网络。（" + transportError + "）");
			}

			string code = "HTTP_" + status;
			string message = null;
			try
			{
				JToken json = string.IsNullOrEmpty(body) ? null : JToken.Parse(body);
				// The site answers {error:{code,message}}; SSO answers {code,message}.
				JToken error = json?["error"] is JObject ? json["error"] : json;
				if (error is JObject)
				{
					code = error.Value<string>("code") ?? code;
					message = error.Value<string>("message");
				}
			}
			catch (JsonException)
			{
			}

			message ??= status switch
			{
				401 => "登录已失效，请重新登录。",
				403 => "没有权限。",
				404 => "找不到这份谱面。",
				429 => "操作太频繁，请稍后再试。",
				_ when status >= 500 => "服务器暂时不可用，请稍后再试。",
				_ => "请求失败（HTTP " + status + "）。",
			};
			return new CommunityException(code, message, status);
		}
	}
}
