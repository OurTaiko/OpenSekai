using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>The gray rounded label above (or beside) a setting. Rows nest it; inline blocks use it on its own.</summary>
	public sealed class OptionLabelView : MonoBehaviour
	{
		[SerializeField]
		private TMP_Text text;

		[SerializeField]
		private LayoutElement layout;

		public void Setup(string label, float width, float height = -1f)
		{
			if (text != null)
			{
				text.text = label;
			}
			if (layout != null)
			{
				if (width > 0f)
				{
					layout.preferredWidth = width;
					layout.minWidth = width;
				}
				if (height > 0f)
				{
					layout.preferredHeight = height;
					layout.minHeight = height;
				}
			}
		}
	}
}
