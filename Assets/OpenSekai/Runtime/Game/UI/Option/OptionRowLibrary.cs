using UnityEngine;

namespace Sekai
{
	/// <summary>The prefabs <see cref="OptionPageView"/> builds pages from, one per row kind. Shared by both option dialogs.</summary>
	[CreateAssetMenu(fileName = "OptionRowLibrary", menuName = "OpenSekai/Option Row Library")]
	public sealed class OptionRowLibrary : ScriptableObject
	{
		public OptionSectionHeaderView sectionHeader;
		public OptionLabelView label;
		public OptionStepperRowView stepper;
		public OptionRadioRowView radio;
		public OptionVolumeRowView volume;
		public OptionSelectorRowView selector;
		public OptionSkinRowView skin;
		public OptionButtonRowView button;
		public OptionButtonGridView buttonGrid;
		public OptionNotePreviewView notePreview;
		public OptionLanePreviewView lanePreview;
		public OptionTextRowView text;

		public OptionRowView Prefab(OptionRowKind kind)
		{
			return kind switch
			{
				OptionRowKind.Stepper => stepper,
				OptionRowKind.Radio => radio,
				OptionRowKind.Volume => volume,
				OptionRowKind.Selector => selector,
				OptionRowKind.Skin => skin,
				OptionRowKind.Button => button,
				OptionRowKind.ButtonGrid => buttonGrid,
				OptionRowKind.NotePreview => notePreview,
				OptionRowKind.LanePreview => lanePreview,
				OptionRowKind.Text => text,
				_ => null,
			};
		}
	}
}
