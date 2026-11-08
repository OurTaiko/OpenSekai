using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>A <see cref="OptionRowKind.ButtonGrid"/>: capsule buttons two per line (ノーツのSE's タップ / フリック / ロング / トレース).</summary>
	public sealed class OptionButtonGridView : OptionRowView
	{
		[SerializeField]
		private Transform grid;

		[SerializeField]
		private Button buttonTemplate;

		private readonly List<Button> buttons = new List<Button>();

		protected override void OnBind(OptionRowStyle style)
		{
			foreach (Button old in buttons)
			{
				Destroy(old.gameObject);
			}
			buttons.Clear();
			for (int i = 0; i < Row.Options.Count; i++)
			{
				Button button = Instantiate(buttonTemplate, grid, false);
				button.gameObject.SetActive(true);
				button.name = "Button_" + Row.Actions[i];
				button.GetComponentInChildren<TMP_Text>(true).text = Row.Options[i];
				string action = Row.Actions[i];
				button.onClick.AddListener(() => Host.RunAction(action));
				buttons.Add(button);
			}
		}

		public override void Refresh(OptionSettings settings)
		{
		}
	}
}
