using System.Collections.Generic;
using Sekai.CoopLive.Proto;
using UnityEngine;

namespace Sekai.CoopLive
{
	/// <summary>
	/// The five player thumbnails of the 協力ライブ live UI (the original's <c>MultiThumbnailViewGroup</c>), in slot
	/// order. Places without a player are hidden.
	/// </summary>
	public sealed class MultiThumbnailViewGroup : MonoBehaviour
	{
		[SerializeField]
		private MultiThumbnailView[] multiThumbnailViewArray;

		private readonly Dictionary<string, MultiThumbnailView> _byUser = new Dictionary<string, MultiThumbnailView>();

		/// <summary>Shows <paramref name="players"/> (slot order) in the first places and hides the rest.</summary>
		public void Setup(IReadOnlyList<Member> players, string myUserId, bool useAllPerfectEffect)
		{
			_byUser.Clear();
			for (int i = 0; i < multiThumbnailViewArray.Length; i++)
			{
				MultiThumbnailView view = multiThumbnailViewArray[i];
				if (view == null)
				{
					continue;
				}
				if (i < players.Count)
				{
					view.Setup(players[i], players[i].UserId == myUserId, useAllPerfectEffect);
					_byUser[players[i].UserId] = view;
				}
				else
				{
					view.Hide();
				}
			}
		}

		public MultiThumbnailView Find(string userId)
		{
			return userId != null && _byUser.TryGetValue(userId, out MultiThumbnailView view) ? view : null;
		}

		public void SetFeverReady(bool ready)
		{
			foreach (MultiThumbnailView view in _byUser.Values)
			{
				view.SetFeverReady(ready);
			}
		}
	}
}
