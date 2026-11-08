using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UiEffect
{
	[AddComponentMenu("UI/Effects/Gradient Alpha")]
	[RequireComponent(typeof(Graphic))]
	public class GradientAlpha : BaseMeshEffect
	{
		private const int ONE_TEXT_VERTEX = 6;

		[SerializeField]
		[Range(0f, 1f)]
		private float m_alphaTop;

		[SerializeField]
		[Range(0f, 1f)]
		private float m_alphaBottom;

		[SerializeField]
		[Range(0f, 1f)]
		private float m_alphaLeft;

		[SerializeField]
		[Range(0f, 1f)]
		private float m_alphaRight;

		[SerializeField]
		[Range(-1f, 1f)]
		private float m_gradientOffsetVertical;

		[SerializeField]
		[Range(-1f, 1f)]
		private float m_gradientOffsetHorizontal;

		[SerializeField]
		private bool m_splitTextGradient;

		public float alphaTop
		{
			get
			{
				return m_alphaTop;
			}
			set
			{
				SetAlpha(ref m_alphaTop, value);
			}
		}

		public float alphaBottom
		{
			get
			{
				return m_alphaBottom;
			}
			set
			{
				SetAlpha(ref m_alphaBottom, value);
			}
		}

		public float alphaLeft
		{
			get
			{
				return m_alphaLeft;
			}
			set
			{
				SetAlpha(ref m_alphaLeft, value);
			}
		}

		public float alphaRight
		{
			get
			{
				return m_alphaRight;
			}
			set
			{
				SetAlpha(ref m_alphaRight, value);
			}
		}

		public float gradientOffsetVertical
		{
			get
			{
				return m_gradientOffsetVertical;
			}
			set
			{
				SetOffset(ref m_gradientOffsetVertical, value);
			}
		}

		public float gradientOffsetHorizontal
		{
			get
			{
				return m_gradientOffsetHorizontal;
			}
			set
			{
				SetOffset(ref m_gradientOffsetHorizontal, value);
			}
		}

		public bool splitTextGradient
		{
			get
			{
				return m_splitTextGradient;
			}
			set
			{
				if (m_splitTextGradient == value)
				{
					return;
				}

				m_splitTextGradient = value;
				Refresh();
			}
		}

		public override void ModifyMesh(VertexHelper vh)
		{
			if (!IsActive() || vh == null)
			{
				return;
			}

			List<UIVertex> vertices = UiEffectListPool<UIVertex>.Get();
			vh.GetUIVertexStream(vertices);
			ModifyVertices(vertices);
			vh.Clear();
			vh.AddUIVertexTriangleStream(vertices);
			UiEffectListPool<UIVertex>.Release(vertices);
		}

		private void ModifyVertices(List<UIVertex> vList)
		{
			if (!IsActive() || vList == null || vList.Count < 1)
			{
				return;
			}

			float minX = 0f;
			float minY = 0f;
			float width = 0f;
			float height = 0f;
			for (int i = 0; i < vList.Count; i++)
			{
				if (i == 0 || (m_splitTextGradient && i % ONE_TEXT_VERTEX == 0))
				{
					int endIndex = m_splitTextGradient ? i + ONE_TEXT_VERTEX : vList.Count;
					UIVertex first = vList[i];
					minX = first.position.x;
					minY = first.position.y;
					float maxX = first.position.x;
					float maxY = first.position.y;
					for (int j = i; j < endIndex && j < vList.Count; j++)
					{
						Vector3 position = vList[j].position;
						if (minX >= position.x)
						{
							minX = position.x;
						}
						if (minY >= position.y)
						{
							minY = position.y;
						}
						if (maxX <= position.x)
						{
							maxX = position.x;
						}
						if (maxY <= position.y)
						{
							maxY = position.y;
						}
					}

					width = maxX - minX;
					height = maxY - minY;
				}

				UIVertex vertex = vList[i];
				float verticalRate = 0f;
				if (height > 0f)
				{
					verticalRate = (vertex.position.y - minY) / height;
				}
				verticalRate = Mathf.Clamp01(verticalRate + m_gradientOffsetVertical);

				float horizontalRate = 0f;
				if (width > 0f)
				{
					horizontalRate = (vertex.position.x - minX) / width;
				}
				horizontalRate = Mathf.Clamp01(horizontalRate + m_gradientOffsetHorizontal);

				Color32 color = vertex.color;
				float sourceAlpha = color.a / 255f;
				float verticalAlpha = m_alphaBottom + (m_alphaTop - m_alphaBottom) * verticalRate;
				float horizontalAlpha = m_alphaLeft + (m_alphaRight - m_alphaLeft) * horizontalRate;
				color.a = (byte)(Mathf.Clamp01(sourceAlpha * verticalAlpha * horizontalAlpha) * 255f);
				vertex.color = color;
				vList[i] = vertex;
			}
		}

		private void Refresh()
		{
			if (graphic != null)
			{
				graphic.SetVerticesDirty();
			}
		}

		public GradientAlpha()
		{
			m_alphaTop = 1f;
			m_alphaBottom = 1f;
			m_alphaLeft = 1f;
			m_alphaRight = 1f;
		}

		private void SetAlpha(ref float field, float value)
		{
			if (field == value)
			{
				return;
			}

			field = Mathf.Clamp01(value);
			Refresh();
		}

		private void SetOffset(ref float field, float value)
		{
			if (field == value)
			{
				return;
			}

			field = Mathf.Clamp(value, -1f, 1f);
			Refresh();
		}
	}
}
