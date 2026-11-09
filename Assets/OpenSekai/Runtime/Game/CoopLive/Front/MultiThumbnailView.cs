using System.Threading;
using Cysharp.Threading.Tasks;
using Sekai.Community;
using Sekai.Live;
using Sekai.CoopLive.Proto;
using TMPro;
using UnityEngine;

namespace Sekai.CoopLive
{
	/// <summary>
	/// One player's thumbnail in the 協力ライブ live UI, from the original's マルチライブ UI (<c>FrontMultiUIView</c>):
	/// frame, picture, dying / dead, FEVER OK, the player's combo and the YOU mark. The picture is the player's SSO
	/// avatar, or the slot color with the nickname's first letter. The serialized fields keep the original's names;
	/// the ones after <see cref="spriteRenderers"/> are OpenSekai's placeholders for sprites the project does not have.
	/// </summary>
	public sealed class MultiThumbnailView : MonoBehaviour
	{
		private const float DyingRate = 0.3f;

		[SerializeField]
		private Transform thumbnailRoot;

		[SerializeField]
		private Transform thumbnailTurnRoot;

		[SerializeField]
		private SpriteRenderer death;

		[SerializeField]
		private SpriteRenderer dying;

		[SerializeField]
		private SpriteRenderer frame;

		[SerializeField]
		private SpriteRenderer thumbnail;

		[SerializeField]
		private SpriteRenderer youIcon;

		[SerializeField]
		private GameObject feverReady;

		[SerializeField]
		private ComboView comboView;

		[SerializeField]
		private SpriteRenderer[] spriteRenderers;

		[SerializeField]
		[Tooltip("The nickname's first letter, shown when the player has no avatar.")]
		private TextMeshPro initial;

		[SerializeField]
		[Tooltip("World size of the picture inside the frame.")]
		private float thumbnailSize = 0.81f;

		private CancellationTokenSource _avatarCts;
		private Color _dyingColor;
		private bool _dying;

		public string UserId { get; private set; }

		private void Awake()
		{
			if (dying != null)
			{
				_dyingColor = dying.color;
			}
		}

		/// <summary>Shows <paramref name="member"/> in this place.</summary>
		public void Setup(Member member, bool isMe, bool useAllPerfectEffect)
		{
			gameObject.SetActive(true);
			UserId = member.UserId;
			Color slot = CoopUi.SlotColor(member.Slot);
			if (frame != null)
			{
				frame.color = slot;
			}
			if (youIcon != null)
			{
				youIcon.gameObject.SetActive(isMe);
			}
			comboView?.Setup(useAllPerfectEffect);
			SetPicture(null, slot);
			if (initial != null)
			{
				string name = member.Nickname ?? string.Empty;
				initial.text = name.Length > 0 ? char.ToUpperInvariant(name[0]).ToString() : "?";
			}
			UpdateStatus(0, false, LiveConfig.Life, true);
			SetFeverReady(false);
			LoadAvatar(member.AvatarUrl, slot);
		}

		public void Hide()
		{
			UserId = null;
			_avatarCts?.Cancel();
			gameObject.SetActive(false);
		}

		/// <summary>Combo, all-perfect so far, life and whether the player is still connected.</summary>
		public void UpdateStatus(int combo, bool allPerfect, int life, bool connected)
		{
			comboView?.Excute(combo, allPerfect);
			bool dead = life <= 0;
			_dying = !dead && life <= LiveConfig.Life * DyingRate;
			if (death != null)
			{
				death.gameObject.SetActive(dead || !connected);
			}
			if (dying != null)
			{
				dying.gameObject.SetActive(_dying);
			}
		}

		public void SetFeverReady(bool ready)
		{
			if (feverReady != null && feverReady.activeSelf != ready)
			{
				feverReady.SetActive(ready);
			}
		}

		private void Update()
		{
			if (_dying && dying != null)
			{
				Color color = _dyingColor;
				color.a *= 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
				dying.color = color;
			}
		}

		private void OnDestroy()
		{
			_avatarCts?.Cancel();
			_avatarCts?.Dispose();
		}

		private void LoadAvatar(string url, Color slot)
		{
			_avatarCts?.Cancel();
			_avatarCts?.Dispose();
			_avatarCts = null;
			if (string.IsNullOrEmpty(url))
			{
				return;
			}
			_avatarCts = new CancellationTokenSource();
			LoadAvatarAsync(url, slot, _avatarCts.Token).Forget();
		}

		private async UniTaskVoid LoadAvatarAsync(string url, Color slot, CancellationToken ct)
		{
			Sprite sprite = await CommunityImageCache.LoadAsync(url, ct);
			if (!ct.IsCancellationRequested && this != null && sprite != null)
			{
				SetPicture(sprite, slot);
			}
		}

		// The avatar fills the picture square; without one, the slot color with the initial.
		private void SetPicture(Sprite sprite, Color slot)
		{
			if (initial != null)
			{
				initial.gameObject.SetActive(sprite == null);
			}
			if (thumbnail == null)
			{
				return;
			}
			// Keep the alpha: the live UI fades every sprite in when the live starts.
			float alpha = thumbnail.color.a;
			if (sprite == null)
			{
				thumbnail.sprite = frame != null ? frame.sprite : null;
				thumbnail.drawMode = SpriteDrawMode.Sliced;
				thumbnail.size = new Vector2(thumbnailSize, thumbnailSize);
				thumbnail.transform.localScale = Vector3.one;
				Color fill = Color.Lerp(slot, Color.black, 0.35f);
				fill.a = alpha;
				thumbnail.color = fill;
				return;
			}
			thumbnail.drawMode = SpriteDrawMode.Simple;
			thumbnail.sprite = sprite;
			thumbnail.color = new Color(1f, 1f, 1f, alpha);
			Vector2 size = sprite.bounds.size;
			float scale = size.x > 0f && size.y > 0f ? thumbnailSize / Mathf.Max(size.x, size.y) : 1f;
			thumbnail.transform.localScale = new Vector3(scale, scale, 1f);
		}
	}
}
