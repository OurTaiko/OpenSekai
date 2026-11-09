using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Sekai.CoopLive
{
	/// <summary>
	/// Small uGUI builders for the 協力ライブ screens, which are put together in code: the room screen changes
	/// its whole layout with the room's phase, and the live HUD sits over the Core scene's own views.
	/// Colors follow the menu screens (light panels, the teal accent).
	/// </summary>
	public static class CoopUi
	{
		public static readonly Color Accent = new Color32(0x33, 0xCC, 0xBB, 0xFF);
		public static readonly Color AccentDark = new Color32(0x1E, 0x9E, 0x92, 0xFF);
		public static readonly Color Ink = new Color32(0x44, 0x44, 0x66, 0xFF);
		public static readonly Color SubInk = new Color32(0x88, 0x88, 0xA0, 0xFF);
		public static readonly Color Panel = new Color32(0xFF, 0xFF, 0xFF, 0xF2);
		public static readonly Color PanelSoft = new Color32(0xF1, 0xF3, 0xF8, 0xFF);
		public static readonly Color Danger = new Color32(0xEE, 0x55, 0x77, 0xFF);
		public static readonly Color Gold = new Color32(0xFF, 0xBB, 0x33, 0xFF);

		/// <summary>One color per room slot, as the original's player frames.</summary>
		public static readonly Color[] SlotColors =
		{
			new Color32(0xFF, 0x77, 0x99, 0xFF),
			new Color32(0x44, 0xBB, 0xFF, 0xFF),
			new Color32(0x66, 0xDD, 0x88, 0xFF),
			new Color32(0xFF, 0xAA, 0x44, 0xFF),
			new Color32(0xAA, 0x88, 0xFF, 0xFF),
		};

		public static Color SlotColor(int slot) => SlotColors[((slot % SlotColors.Length) + SlotColors.Length) % SlotColors.Length];

		private const string FontEbPath = "font/FOT-RodinNTLGPro-EB SDF_Base";
		private const string FontDbPath = "font/FOT-RodinNTLGPro-DB SDF_Base";
		private static TMP_FontAsset _bold;
		private static TMP_FontAsset _regular;

		public static TMP_FontAsset Bold => _bold != null ? _bold : (_bold = Resources.Load<TMP_FontAsset>(FontEbPath) ?? TMP_Settings.defaultFontAsset);
		public static TMP_FontAsset Regular => _regular != null ? _regular : (_regular = Resources.Load<TMP_FontAsset>(FontDbPath) ?? TMP_Settings.defaultFontAsset);

		/// <summary>Rounded sprite for panels and buttons; set by the screen from its serialized references.</summary>
		public static Sprite RoundSprite { get; set; }

		public static RectTransform Rect(string name, Transform parent)
		{
			GameObject go = new GameObject(name, typeof(RectTransform));
			go.layer = parent != null ? parent.gameObject.layer : 5;
			RectTransform rt = (RectTransform)go.transform;
			rt.SetParent(parent, false);
			return rt;
		}

		public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
		{
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = new Vector2(left, bottom);
			rt.offsetMax = new Vector2(-right, -top);
			return rt;
		}

		/// <summary>Anchors at one point with a fixed size.</summary>
		public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
		{
			rt.anchorMin = anchor;
			rt.anchorMax = anchor;
			rt.pivot = pivot ?? anchor;
			rt.anchoredPosition = position;
			rt.sizeDelta = size;
			return rt;
		}

		public static Image Image(Transform parent, string name, Color color, bool rounded = true)
		{
			RectTransform rt = Rect(name, parent);
			Image image = rt.gameObject.AddComponent<Image>();
			image.color = color;
			if (rounded && RoundSprite != null)
			{
				image.sprite = RoundSprite;
				image.type = UnityEngine.UI.Image.Type.Sliced;
				image.pixelsPerUnitMultiplier = 1.5f;
			}
			return image;
		}

		public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, bool bold = false,
			TextAlignmentOptions alignment = TextAlignmentOptions.Left)
		{
			RectTransform rt = Rect(name, parent);
			TextMeshProUGUI label = rt.gameObject.AddComponent<TextMeshProUGUI>();
			label.font = bold ? Bold : Regular;
			label.text = text;
			label.fontSize = size;
			label.color = color;
			label.alignment = alignment;
			label.raycastTarget = false;
			label.textWrappingMode = TextWrappingModes.NoWrap;
			label.overflowMode = TextOverflowModes.Ellipsis;
			return label;
		}

		public static Button Button(Transform parent, string name, string text, UnityAction onClick, Color? background = null, Color? textColor = null, float fontSize = 30)
		{
			Image image = Image(parent, name, background ?? Accent);
			Button button = image.gameObject.AddComponent<Button>();
			ColorBlock colors = button.colors;
			colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
			colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
			colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
			button.colors = colors;
			button.targetGraphic = image;
			if (onClick != null)
			{
				button.onClick.AddListener(onClick);
			}
			TextMeshProUGUI label = Text(image.transform, "Label", text, fontSize, textColor ?? Color.white, true, TextAlignmentOptions.Center);
			label.rectTransform.Stretch(12, 0, 12, 0);
			return button;
		}

		public static void SetLabel(this Button button, string text)
		{
			TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
			if (label != null)
			{
				label.text = text;
			}
		}

		public static VerticalLayoutGroup Vertical(GameObject go, float spacing, RectOffset padding = null, bool expandWidth = true)
		{
			VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
			layout.spacing = spacing;
			layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
			layout.childControlWidth = true;
			layout.childControlHeight = true;
			layout.childForceExpandWidth = expandWidth;
			layout.childForceExpandHeight = false;
			return layout;
		}

		public static HorizontalLayoutGroup Horizontal(GameObject go, float spacing, RectOffset padding = null)
		{
			HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = spacing;
			layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
			layout.childControlWidth = true;
			layout.childControlHeight = true;
			layout.childForceExpandWidth = false;
			layout.childForceExpandHeight = true;
			layout.childAlignment = TextAnchor.MiddleLeft;
			return layout;
		}

		public static LayoutElement Size(Component c, float width = -1, float height = -1, float flexibleWidth = -1)
		{
			LayoutElement element = c.gameObject.GetComponent<LayoutElement>();
			if (element == null)
			{
				element = c.gameObject.AddComponent<LayoutElement>();
			}
			if (width >= 0)
			{
				element.preferredWidth = width;
				element.minWidth = width;
			}
			if (height >= 0)
			{
				element.preferredHeight = height;
				element.minHeight = height;
			}
			if (flexibleWidth >= 0)
			{
				element.flexibleWidth = flexibleWidth;
			}
			return element;
		}

		/// <summary>A vertical scroll list; children go under the returned content.</summary>
		public static RectTransform ScrollList(Transform parent, string name, float spacing, out ScrollRect scroll)
		{
			RectTransform viewport = Rect(name, parent);
			Image mask = viewport.gameObject.AddComponent<Image>();
			mask.color = new Color(1, 1, 1, 0.01f);
			viewport.gameObject.AddComponent<RectMask2D>();
			scroll = viewport.gameObject.AddComponent<ScrollRect>();
			scroll.horizontal = false;
			scroll.movementType = ScrollRect.MovementType.Clamped;
			scroll.scrollSensitivity = 40f;
			RectTransform content = Rect("Content", viewport);
			content.anchorMin = new Vector2(0, 1);
			content.anchorMax = new Vector2(1, 1);
			content.pivot = new Vector2(0.5f, 1);
			content.offsetMin = Vector2.zero;
			content.offsetMax = Vector2.zero;
			Vertical(content.gameObject, spacing);
			ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			scroll.viewport = viewport;
			scroll.content = content;
			return content;
		}

		public static TMP_InputField Input(Transform parent, string name, string placeholder, TMP_InputField.ContentType contentType, int limit)
		{
			Image background = Image(parent, name, PanelSoft);
			TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();
			RectTransform area = Rect("Text Area", background.transform).Stretch(20, 6, 20, 6);
			area.gameObject.AddComponent<RectMask2D>();
			TextMeshProUGUI hint = Text(area, "Placeholder", placeholder, 28, SubInk);
			hint.rectTransform.Stretch();
			hint.alignment = TextAlignmentOptions.MidlineLeft;
			TextMeshProUGUI text = Text(area, "Text", string.Empty, 30, Ink, true);
			text.rectTransform.Stretch();
			text.alignment = TextAlignmentOptions.MidlineLeft;
			input.textViewport = area;
			input.textComponent = text;
			input.placeholder = hint;
			input.contentType = contentType;
			input.characterLimit = limit;
			input.fontAsset = Bold;
			return input;
		}

		/// <summary>The co-op screens' frame: Home's background with its tint, and the header band with a back button.</summary>
		public static RectTransform Frame(Transform parent, Sprite backgroundSprite, UnityAction onBack, out TextMeshProUGUI title, out TextMeshProUGUI subtitle)
		{
			RectTransform root = Rect("CoopRoot", parent).Stretch();
			Image background = Image(root, "Background", Color.white, rounded: false);
			background.rectTransform.Stretch();
			background.sprite = backgroundSprite;
			background.color = backgroundSprite != null ? Color.white : new Color32(0x3A, 0x33, 0x6E, 0xFF);
			background.raycastTarget = true;
			if (backgroundSprite != null)
			{
				AspectRatioFitter fitter = background.gameObject.AddComponent<AspectRatioFitter>();
				fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
				fitter.aspectRatio = backgroundSprite.rect.width / Mathf.Max(1f, backgroundSprite.rect.height);
			}
			Image tint = Image(root, "Tint", new Color32(0x22, 0x1C, 0x48, 0xA8), rounded: false);
			tint.rectTransform.Stretch();

			Image header = Image(root, "Header", new Color32(0x34, 0x2E, 0x5C, 0x96), rounded: false);
			header.rectTransform.anchorMin = new Vector2(0, 1);
			header.rectTransform.anchorMax = new Vector2(1, 1);
			header.rectTransform.pivot = new Vector2(0.5f, 1);
			header.rectTransform.offsetMin = new Vector2(0, -132);
			header.rectTransform.offsetMax = Vector2.zero;
			Button back = Button(header.transform, "Back", "‹ 返回", onBack, Color.white, Ink, 28);
			back.GetComponent<RectTransform>().Place(new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(170, 72));
			title = Text(header.transform, "Title", "協力ライブ", 40, Color.white, true);
			title.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(234, 0), new Vector2(900, 70));
			subtitle = Text(header.transform, "RoomCode", string.Empty, 32, new Color32(0x67, 0xDD, 0xCD, 0xFF), true, TextAlignmentOptions.Right);
			subtitle.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-34, 0), new Vector2(800, 70));
			return root;
		}

		/// <summary>The row of スタンプ buttons along the bottom.</summary>
		public static RectTransform StampBar(Transform parent)
		{
			RectTransform bar = Rect("Stamps", parent);
			bar.anchorMin = new Vector2(0, 0);
			bar.anchorMax = new Vector2(1, 0);
			bar.pivot = new Vector2(0.5f, 0);
			bar.offsetMin = new Vector2(24, 20);
			bar.offsetMax = new Vector2(-24, 100);
			HorizontalLayoutGroup stamps = Horizontal(bar.gameObject, 12);
			stamps.childForceExpandWidth = true;
			for (int i = 0; i < CoopStamps.Texts.Length; i++)
			{
				int id = i;
				Button stamp = Button(bar, "Stamp" + i, CoopStamps.Texts[i], () => CoopSession.SendStamp(id), Panel, Ink, 24);
				Size(stamp, height: 80);
			}
			return bar;
		}

		/// <summary>A message box at the top; hide it again with SetActive(false) on the returned label's parent.</summary>
		public static TextMeshProUGUI Toast(Transform parent)
		{
			Image back = Image(parent, "ToastBack", new Color(0.15f, 0.13f, 0.28f, 0.92f));
			back.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(1000, 70));
			TextMeshProUGUI label = Text(back.transform, "Toast", string.Empty, 30, Color.white, true, TextAlignmentOptions.Center);
			label.rectTransform.Stretch(20, 0, 20, 0);
			back.gameObject.SetActive(false);
			return label;
		}

		public static void Clear(Transform t)
		{
			for (int i = t.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
			}
		}

		public static string FormatSeconds(long ms) => ms < 0 ? string.Empty : Math.Ceiling(ms / 1000.0).ToString("0");

		public static string DifficultyLabel(string difficulty)
		{
			switch (difficulty)
			{
				case "easy": return "EASY";
				case "normal": return "NORMAL";
				case "hard": return "HARD";
				case "expert": return "EXPERT";
				case "master": return "MASTER";
				case "append": return "APPEND";
				default: return (difficulty ?? string.Empty).ToUpperInvariant();
			}
		}

		public static Color DifficultyColor(string difficulty)
		{
			switch (difficulty)
			{
				case "easy": return new Color32(0x66, 0xDD, 0x11, 0xFF);
				case "normal": return new Color32(0x33, 0xBB, 0xEE, 0xFF);
				case "hard": return new Color32(0xFF, 0xAA, 0x00, 0xFF);
				case "expert": return new Color32(0xEE, 0x44, 0x66, 0xFF);
				case "master": return new Color32(0xBB, 0x33, 0xEE, 0xFF);
				case "append": return new Color32(0xEE, 0x77, 0xCC, 0xFF);
				default: return SubInk;
			}
		}
	}
}
