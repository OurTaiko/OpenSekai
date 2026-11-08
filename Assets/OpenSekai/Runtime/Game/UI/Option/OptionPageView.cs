using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	/// <summary>
	/// Lays out an <see cref="OptionPage"/> under a page's content: a header per section, then its blocks, each row made
	/// from the <see cref="OptionRowLibrary"/> prefab of its kind. Sizes and gaps are canvas units measured from a recording
	/// of the original dialog.
	/// </summary>
	public static class OptionPageView
	{
		private const float ContentWidth = 1040f;
		private const float SplitLeft = 593f;
		private const float SplitRight = 400f;
		private const float SplitGap = 47f;
		private const float ColumnWidth = 320f;
		private const float ColumnGap = 40f;
		private const float HalfWidth = 500f;
		private const float RadioHeight = 54f;
		private const float StepHeight = 80f;

		public static List<OptionRowView> Build(OptionPage page, RectTransform content, OptionRowLibrary library, IOptionRowHost host)
		{
			var views = new List<OptionRowView>();
			for (int s = 0; s < page.Sections.Count; s++)
			{
				OptionSection section = page.Sections[s];
				if (s > 0)
				{
					Spacer(content, BeforeHeader(page.Sections[s - 1]));
				}
				Object.Instantiate(library.sectionHeader, content, false).Setup(section.Title);
				Spacer(content, AfterHeader(section.Blocks[0]));
				for (int b = 0; b < section.Blocks.Count; b++)
				{
					if (b > 0)
					{
						Spacer(content, BlockGap(section.Blocks[b - 1], section.Blocks[b]));
					}
					BuildBlock(section.Blocks[b], content, library, host, views);
				}
			}
			return views;
		}

		private static void BuildBlock(OptionBlock block, RectTransform content, OptionRowLibrary library, IOptionRowHost host, List<OptionRowView> views)
		{
			switch (block.Layout)
			{
				case OptionBlockLayout.Split:
					BuildSplit(block, content, library, host, views);
					break;
				case OptionBlockLayout.Full:
					foreach (OptionRow row in block.Rows)
					{
						views.Add(Add(row, content, library, host, new OptionRowStyle { Width = ContentWidth, ShowLabel = true }));
					}
					break;
				case OptionBlockLayout.Columns:
					BuildGrid(block, content, library, host, views, ColumnWidth, 3);
					break;
				case OptionBlockLayout.Halves:
					BuildGrid(block, content, library, host, views, HalfWidth, 2);
					break;
				case OptionBlockLayout.Inline:
					for (int i = 0; i < block.Rows.Count; i++)
					{
						if (i > 0)
						{
							Spacer(content, 24f);
						}
						RectTransform line = Row(content, "Inline_" + block.Rows[i].Id, 24f, TextAnchor.MiddleLeft);
						Object.Instantiate(library.label, line, false).Setup(block.Rows[i].Label, 241f, 46f);
						views.Add(Add(block.Rows[i], line, library, host,
							new OptionRowStyle { Width = ContentWidth - 265f, ShowLabel = false, Height = RadioHeight }));
					}
					break;
				case OptionBlockLayout.Volume:
					for (int i = 0; i < block.Rows.Count; i++)
					{
						if (i > 0)
						{
							Spacer(content, 14f);
						}
						views.Add(Add(block.Rows[i], content, library, host, new OptionRowStyle { Width = ContentWidth, ShowLabel = true }));
					}
					break;
				case OptionBlockLayout.Centered:
					for (int i = 0; i < block.Rows.Count; i++)
					{
						OptionRow row = block.Rows[i];
						if (i > 0)
						{
							Spacer(content, row.Kind == OptionRowKind.Text ? 27f : 30f);
						}
						views.Add(Add(row, content, library, host, new OptionRowStyle
						{
							Width = ContentWidth, ShowLabel = false, ButtonWidth = 89f, ValueWidth = 142f, Spacing = 22f, Height = 81f,
							Alignment = TextAnchor.MiddleCenter,
						}));
					}
					break;
			}
		}

		// Rows in a 593-wide left column (33 apart), side rows in the right column; both top-aligned.
		private static void BuildSplit(OptionBlock block, RectTransform content, OptionRowLibrary library, IOptionRowHost host, List<OptionRowView> views)
		{
			bool wideSide = false;
			foreach (OptionRow row in block.Rows)
			{
				wideSide |= row.IsSide && row.Kind == OptionRowKind.ButtonGrid;
			}
			RectTransform line = Row(content, "Split", wideSide ? SplitGap - 16f : SplitGap, TextAnchor.UpperLeft);
			RectTransform left = Column(line, "Left", SplitLeft);
			RectTransform right = Column(line, "Right", wideSide ? 416f : SplitRight);
			bool firstLeft = true;
			foreach (OptionRow row in block.Rows)
			{
				if (row.IsSide)
				{
					views.Add(Add(row, right, library, host, new OptionRowStyle { Width = wideSide ? 416f : SplitRight }));
					continue;
				}
				if (!firstLeft)
				{
					Spacer(left, 33f);
				}
				firstLeft = false;
				bool percent = row.Suffix == "%";
				views.Add(Add(row, left, library, host, new OptionRowStyle
				{
					Width = SplitLeft, ShowLabel = true, LabelGap = percent ? 26f : 25f, Height = StepHeight,
					ButtonWidth = percent ? 80f : 88f, ValueWidth = percent ? 145f : 143f, Spacing = percent ? 24f : 23f,
					Alignment = percent ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft,
				}));
			}
		}

		// Cells of `cellWidth`, `perLine` to a line (a wide row takes two cells), lines 33 or 34 apart.
		private static void BuildGrid(OptionBlock block, RectTransform content, OptionRowLibrary library, IOptionRowHost host, List<OptionRowView> views,
			float cellWidth, int perLine)
		{
			var lines = new List<List<OptionRow>>();
			int used = perLine;
			foreach (OptionRow row in block.Rows)
			{
				if (row.IsHidden)
				{
					continue;
				}
				int span = row.IsWide ? 2 : 1;
				if (used + span > perLine)
				{
					lines.Add(new List<OptionRow>());
					used = 0;
				}
				lines[lines.Count - 1].Add(row);
				used += span;
			}

			for (int l = 0; l < lines.Count; l++)
			{
				bool hasStepper = lines[l].Exists(r => r.Kind == OptionRowKind.Stepper);
				if (l > 0)
				{
					bool previousStepper = lines[l - 1].Exists(r => r.Kind == OptionRowKind.Stepper);
					Spacer(content, previousStepper ? 33f : 34f);
				}
				RectTransform line = Row(content, "Line" + l, ColumnGap, TextAnchor.UpperLeft);
				foreach (OptionRow row in lines[l])
				{
					float width = row.IsWide ? cellWidth * 2f + ColumnGap : cellWidth;
					var style = new OptionRowStyle
					{
						Width = width, ShowLabel = true, LabelGap = 25f, Height = hasStepper ? StepHeight : RadioHeight,
						ButtonWidth = 81f, ValueWidth = 111f, Spacing = 24f, Alignment = TextAnchor.MiddleLeft,
					};
					if (row.Kind == OptionRowKind.Button)
					{
						style.LabelGap = 27f;
						style.Height = 58f;
					}
					views.Add(Add(row, line, library, host, style));
				}
			}
		}

		private static OptionRowView Add(OptionRow row, Transform parent, OptionRowLibrary library, IOptionRowHost host, OptionRowStyle style)
		{
			OptionRowView prefab = library.Prefab(row.Kind);
			OptionRowView view = Object.Instantiate(prefab, parent, false);
			view.Bind(row, host, style);
			return view;
		}

		private static float AfterHeader(OptionBlock first)
		{
			if (first.Layout == OptionBlockLayout.Volume)
			{
				return 20f;
			}
			return first.Layout == OptionBlockLayout.Centered && first.Rows[0].Kind == OptionRowKind.Text ? 24f : 28f;
		}

		private static float BeforeHeader(OptionSection previous)
		{
			OptionBlock last = previous.Blocks[previous.Blocks.Count - 1];
			return last.Layout switch
			{
				OptionBlockLayout.Volume => 29.5f,
				OptionBlockLayout.Centered => 69.5f,
				_ => 32.5f,
			};
		}

		private static float BlockGap(OptionBlock previous, OptionBlock next)
		{
			if (previous.Layout == OptionBlockLayout.Split && next.Layout == OptionBlockLayout.Full)
			{
				return 34f;
			}
			if (previous.Layout == OptionBlockLayout.Split && next.Layout == OptionBlockLayout.Split && IsPercentSplit(previous) && IsPercentSplit(next))
			{
				return 30f;
			}
			return 33f;
		}

		private static bool IsPercentSplit(OptionBlock block)
		{
			foreach (OptionRow row in block.Rows)
			{
				if (!row.IsSide && row.Kind == OptionRowKind.Stepper && row.Suffix == "%")
				{
					return true;
				}
			}
			return false;
		}

		private static RectTransform NewRT(string name, Transform parent)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.layer = parent.gameObject.layer;
			go.transform.SetParent(parent, false);
			return (RectTransform)go.transform;
		}

		private static void Spacer(Transform parent, float height)
		{
			var layout = NewRT("Space", parent).gameObject.AddComponent<LayoutElement>();
			layout.preferredHeight = layout.minHeight = height;
		}

		private static RectTransform Row(Transform parent, string name, float spacing, TextAnchor alignment)
		{
			RectTransform rt = NewRT(name, parent);
			var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
			h.spacing = spacing;
			h.childAlignment = alignment;
			h.childControlWidth = true;
			h.childControlHeight = true;
			h.childForceExpandWidth = false;
			h.childForceExpandHeight = false;
			return rt;
		}

		private static RectTransform Column(Transform parent, string name, float width)
		{
			RectTransform rt = NewRT(name, parent);
			var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
			v.childAlignment = TextAnchor.UpperLeft;
			v.childControlWidth = true;
			v.childControlHeight = true;
			v.childForceExpandWidth = false;
			v.childForceExpandHeight = false;
			var layout = rt.gameObject.AddComponent<LayoutElement>();
			layout.preferredWidth = layout.minWidth = width;
			return rt;
		}
	}
}
