using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Sekai.Community
{
	/// <summary>Jacket images from the CDN as sprites, kept in memory for the last few dozen charts.</summary>
	public static class CommunityImageCache
	{
		private const int Capacity = 64;

		private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

		private static readonly LinkedList<string> Order = new LinkedList<string>();

		private static readonly HashSet<string> Pending = new HashSet<string>();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Sprites.Clear();
			Order.Clear();
			Pending.Clear();
		}

		/// <summary>The jacket as a sprite, or null if it has none or it cannot be loaded.</summary>
		public static async UniTask<Sprite> LoadAsync(string url, CancellationToken ct)
		{
			if (string.IsNullOrEmpty(url))
			{
				return null;
			}
			if (Sprites.TryGetValue(url, out Sprite cached) && cached != null)
			{
				Order.Remove(url);
				Order.AddFirst(url);
				return cached;
			}
			if (Pending.Contains(url))
			{
				// Another caller is fetching it; wait for that rather than awaiting its task twice.
				await UniTask.WaitWhile(() => Pending.Contains(url), cancellationToken: ct);
				return Sprites.TryGetValue(url, out Sprite loaded) ? loaded : null;
			}
			Pending.Add(url);
			return await FetchAsync(url).AttachExternalCancellation(ct);
		}

		private static async UniTask<Sprite> FetchAsync(string url)
		{
			try
			{
				using UnityWebRequest request = UnityWebRequest.Get(url);
				request.timeout = 30;
				await request.SendWebRequest();
				if (request.result != UnityWebRequest.Result.Success)
				{
					return null;
				}
				Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
				if (!texture.LoadImage(request.downloadHandler.data, true))
				{
					UnityEngine.Object.Destroy(texture);
					Debug.LogWarning("[Community] Jacket is not PNG or JPEG: " + url);
					return null;
				}
				Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
				Store(url, sprite);
				return sprite;
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Community] Jacket failed to load: " + exception.Message);
				return null;
			}
			finally
			{
				Pending.Remove(url);
			}
		}

		private static void Store(string url, Sprite sprite)
		{
			Sprites[url] = sprite;
			Order.AddFirst(url);
			while (Order.Count > Capacity)
			{
				string oldest = Order.Last.Value;
				Order.RemoveLast();
				if (Sprites.TryGetValue(oldest, out Sprite old) && old != null)
				{
					UnityEngine.Object.Destroy(old.texture);
					UnityEngine.Object.Destroy(old);
				}
				Sprites.Remove(oldest);
			}
		}
	}
}
