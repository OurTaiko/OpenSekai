using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// A <see cref="OptionRowKind.LanePreview"/>: the dark perspective lane with a long-note band whose opacity follows the
	/// row named by <see cref="OptionRow.BoundRowId"/> (a percentage).
	/// </summary>
	public sealed class OptionLanePreviewView : OptionRowView
	{
		[SerializeField]
		private Image band;

		[SerializeField]
		private Sprite[] bandSprites;

		private OptionRow bound;

		protected override void OnBind(OptionRowStyle style)
		{
			bound = Host.FindRow(Row.BoundRowId);
			foreach (Sprite sprite in bandSprites)
			{
				if (sprite != null && sprite.name == Row.Art)
				{
					band.sprite = sprite;
				}
			}
		}

		public override void Refresh(OptionSettings settings)
		{
			if (bound == null)
			{
				return;
			}
			Color color = band.color;
			color.a = Mathf.Clamp01(bound.GetNumber(settings) / 100f);
			band.color = color;
		}
	}
}
