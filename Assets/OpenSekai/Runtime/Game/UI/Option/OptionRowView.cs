using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>What a row view needs from the dialog that shows it.</summary>
	public interface IOptionRowHost
	{
		OptionSettings Settings { get; }

		/// <summary>A row wrote a new value into <see cref="Settings"/>.</summary>
		void OnRowChanged(OptionRow row);

		/// <summary>A button of a row was pressed (see the OptionCatalog.Action* ids).</summary>
		void RunAction(string action);

		OptionRow FindRow(string id);
	}

	/// <summary>
	/// The view of one <see cref="OptionRow"/>, instantiated from its prefab by <see cref="OptionPageView"/>. Views never
	/// hold a value of their own: <see cref="Refresh"/> reads it from the settings after every change.
	/// </summary>
	public abstract class OptionRowView : MonoBehaviour
	{
		[SerializeField]
		protected LayoutElement layout;

		public OptionRow Row { get; private set; }
		protected IOptionRowHost Host { get; private set; }

		public void Bind(OptionRow row, IOptionRowHost host, OptionRowStyle style)
		{
			Row = row;
			Host = host;
			name = GetType().Name.Replace("View", string.Empty) + "_" + row.Id;
			if (layout != null && style.Width > 0f)
			{
				layout.preferredWidth = style.Width;
				layout.minWidth = style.Width;
			}
			OnBind(style);
		}

		protected abstract void OnBind(OptionRowStyle style);

		public abstract void Refresh(OptionSettings settings);

		protected void Changed()
		{
			Host.OnRowChanged(Row);
		}
	}

	/// <summary>Sizes a row takes in the block that holds it.</summary>
	public struct OptionRowStyle
	{
		public float Width;
		public bool ShowLabel;
		public float LabelGap;
		public float ButtonWidth;
		public float ValueWidth;
		public float Spacing;
		public float Height;
		public TextAnchor Alignment;
	}
}
