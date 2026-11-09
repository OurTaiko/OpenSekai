using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Sekai.CoopLive
{
	/// <summary>Where the live is in the 協力ライブ's FEVER CHANCE / FEVER (see <see cref="CoopLiveBridge"/>).</summary>
	public enum CoopFeverPhase
	{
		None,
		Chance,
		Fever,
		SuperFever,
	}

	/// <summary>
	/// FEVER CHANCE, FEVER and SUPER FEVER, from the original's マルチライブ UI (<c>FrontMultiUIView</c>'s <c>FeverView</c>).
	/// <list type="bullet">
	/// <item>FEVER CHANCE: everything outside the lane is greyed (<see cref="feverChanceFrames"/>, two big rectangles
	/// along the lane's sides), a gauge rises on both sides of the lane with the team's PERFECT and GREAT, and the FEVER and
	/// SUPER FEVER lines cross it with their labels. The gauge is yellow below the FEVER line and rainbow above it.</item>
	/// <item>FEVER / SUPER FEVER: a rainbow border runs round the screen, and the lane shows FEVER! or a rainbow
	/// SUPER FEVER!!.</item>
	/// </list>
	/// The serialized fields up to <see cref="feverChanceFrames"/> keep the original's names (the two "lane texts" are the
	/// line labels). The gauge and border are meshes built here, and the labels and lane marks are TextMeshPro
	/// placeholders for the original's sprites, which the project does not have.
	/// </summary>
	public sealed class MultiFeverView : MonoBehaviour
	{
		[SerializeField]
		private Transform feverChanceRoot;

		[SerializeField]
		private SpriteRenderer feverLaneText;

		[SerializeField]
		private SpriteRenderer superFeverLaneText;

		[SerializeField]
		private SpriteRenderer[] feverChanceFrames;

		[SerializeField]
		private GameObject[] feverLines;

		[SerializeField]
		private GameObject[] superFeverLines;

		[SerializeField]
		private TextMeshPro feverMark;

		[SerializeField]
		private TextMeshPro superFeverMark;

		[Header("Lane geometry (this view's space)")]
		[SerializeField]
		[Tooltip("The lane's apex; its edges run from here to the bottom corners.")]
		private Vector2 laneApex = new Vector2(0f, 5.01f);

		[SerializeField]
		[Tooltip("The lane's bottom right corner; the left one is mirrored.")]
		private Vector2 laneBottomRight = new Vector2(10.02f, -6.56f);

		[SerializeField]
		private float gaugeBottomY = -2.92f;

		[SerializeField]
		private float feverLineY = 2.94f;

		[SerializeField]
		private float superFeverLineY = 4.10f;

		[SerializeField]
		[Tooltip("Horizontal gap between the lane edge and the gauge, and the gauge's width.")]
		private Vector2 gaugeInsetAndWidth = new Vector2(0.28f, 0.42f);

		[SerializeField]
		[Range(0f, 1f)]
		[Tooltip("The share of the team's notes, hit PERFECT or GREAT, that the FEVER line stands for (the server's FEVER rate).")]
		private float feverRate = 0.7f;

		[Header("Colors")]
		[SerializeField]
		private Color coverColor = new Color(0.6f, 0.6f, 0.64f, 0.65f);

		[SerializeField]
		private Color gaugeColor = new Color32(0xFF, 0xD8, 0x4A, 0xFF);

		[SerializeField]
		private Color gaugeTrackColor = new Color(1f, 1f, 1f, 0.18f);

		[SerializeField]
		private float borderWidth = 0.18f;

		private const int GaugeSegments = 24;
		private const int BorderSegments = 160;

		private CoopFeverPhase _phase = (CoopFeverPhase)(-1);
		private float _gauge;
		private Camera _camera;
		private Mesh _gaugeMesh;
		private Mesh _borderMesh;
		private GameObject _gaugeObject;
		private GameObject _borderObject;
		private readonly List<Vector3> _vertices = new List<Vector3>();
		private readonly List<Color> _colors = new List<Color>();
		private readonly List<int> _triangles = new List<int>();

		private void Awake()
		{
			Material material = feverChanceFrames != null && feverChanceFrames.Length > 0 && feverChanceFrames[0] != null
				? feverChanceFrames[0].sharedMaterial
				: new Material(Shader.Find("Sprites/Default"));
			_gaugeObject = CreateMeshObject("FeverGauge", feverChanceRoot != null ? feverChanceRoot : transform, material, -150, out _gaugeMesh);
			_borderObject = CreateMeshObject("FeverBorder", transform, material, 300, out _borderMesh);
			Show(CoopFeverPhase.None);
		}

		private void OnDestroy()
		{
			Destroy(_gaugeMesh);
			Destroy(_borderMesh);
		}

		public void Show(CoopFeverPhase phase)
		{
			if (phase == _phase)
			{
				return;
			}
			_phase = phase;
			bool chance = phase == CoopFeverPhase.Chance;
			bool fever = phase == CoopFeverPhase.Fever || phase == CoopFeverPhase.SuperFever;
			if (feverChanceRoot != null)
			{
				feverChanceRoot.gameObject.SetActive(chance);
			}
			SetActive(feverLaneText != null ? feverLaneText.gameObject : null, chance);
			SetActive(superFeverLaneText != null ? superFeverLaneText.gameObject : null, chance);
			foreach (GameObject line in Lines())
			{
				SetActive(line, chance);
			}
			foreach (SpriteRenderer frame in feverChanceFrames ?? System.Array.Empty<SpriteRenderer>())
			{
				if (frame != null)
				{
					frame.color = chance ? coverColor : Color.clear;
				}
			}
			SetActive(feverMark != null ? feverMark.gameObject : null, phase == CoopFeverPhase.Fever);
			SetActive(superFeverMark != null ? superFeverMark.gameObject : null, phase == CoopFeverPhase.SuperFever);
			SetActive(_borderObject, fever);
			if (chance)
			{
				_gauge = 0f;
			}
		}

		/// <summary>The team's PERFECT + GREAT in FEVER CHANCE as a share of its notes there (0..1).</summary>
		public void SetGauge(float value)
		{
			_gauge = Mathf.Clamp01(float.IsNaN(value) ? 0f : value);
		}

		private void Update()
		{
			if (_phase == CoopFeverPhase.Chance)
			{
				BuildGauge();
			}
			else if (_phase == CoopFeverPhase.Fever || _phase == CoopFeverPhase.SuperFever)
			{
				BuildBorder();
			}
		}

		// TextMeshPro rebuilds its mesh when it changes; bend it onto the lane after that, every frame it shows.
		private void LateUpdate()
		{
			if (_phase == CoopFeverPhase.Fever)
			{
				LayOnLane(feverMark);
			}
			else if (_phase == CoopFeverPhase.SuperFever)
			{
				LayOnLane(superFeverMark);
			}
		}

		/// <summary>
		/// Draws a lane mark as if it lay on the lane: every point is pulled towards the lane's centre line by how much
		/// narrower the lane is at its height, so the letters' strokes run towards the lane's apex like its edges.
		/// </summary>
		private void LayOnLane(TextMeshPro mark)
		{
			if (mark == null || !mark.gameObject.activeInHierarchy)
			{
				return;
			}
			mark.ForceMeshUpdate();
			Vector3 origin = mark.transform.localPosition;
			float reference = laneApex.y - origin.y;
			if (reference <= 0f)
			{
				return;
			}
			TMP_TextInfo info = mark.textInfo;
			for (int m = 0; m < info.meshInfo.Length; m++)
			{
				Vector3[] vertices = info.meshInfo[m].vertices;
				if (vertices == null)
				{
					continue;
				}
				for (int i = 0; i < vertices.Length; i++)
				{
					Vector3 v = vertices[i];
					float y = origin.y + v.y;
					float x = origin.x + v.x;
					float scale = Mathf.Max(0f, laneApex.y - y) / reference;
					v.x = laneApex.x + (x - laneApex.x) * scale - origin.x;
					vertices[i] = v;
				}
			}
			mark.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
		}

		/* ---------- gauge ---------- */

		private void BuildGauge()
		{
			Begin();
			float top = superFeverLineY;
			float fill = _gauge <= feverRate
				? gaugeBottomY + (feverRate > 0f ? _gauge / feverRate : 1f) * (feverLineY - gaugeBottomY)
				: feverLineY + (_gauge - feverRate) / Mathf.Max(0.0001f, 1f - feverRate) * (superFeverLineY - feverLineY);
			bool rainbow = _gauge >= feverRate;
			for (int side = -1; side <= 1; side += 2)
			{
				Strip(side, gaugeBottomY, top, _ => gaugeTrackColor);
				Strip(side, gaugeBottomY, fill, y => rainbow ? Rainbow((y - gaugeBottomY) / (top - gaugeBottomY) * 0.8f - Time.unscaledTime * 0.35f) : gaugeColor);
			}
			End(_gaugeMesh);
		}

		// The lane edge's x at height y on the right (side 1) or left (side -1).
		private float EdgeX(float y, int side)
		{
			float slope = (laneBottomRight.x - laneApex.x) / (laneApex.y - laneBottomRight.y);
			return side * (laneApex.y - y) * slope;
		}

		private void Strip(int side, float from, float to, System.Func<float, Color> color)
		{
			if (to <= from)
			{
				return;
			}
			int start = _vertices.Count;
			for (int i = 0; i <= GaugeSegments; i++)
			{
				float y = Mathf.Lerp(from, to, i / (float)GaugeSegments);
				float edge = EdgeX(y, side);
				Color c = color(y);
				_vertices.Add(new Vector3(edge + side * gaugeInsetAndWidth.x, y, 0f));
				_vertices.Add(new Vector3(edge + side * (gaugeInsetAndWidth.x + gaugeInsetAndWidth.y), y, 0f));
				_colors.Add(c);
				_colors.Add(c);
				if (i > 0)
				{
					int a = start + (i - 1) * 2;
					Quad(a, a + 1, a + 3, a + 2);
				}
			}
		}

		/* ---------- border ---------- */

		private void BuildBorder()
		{
			if (_camera == null)
			{
				Sekai.Live.LiveFrontView view = GetComponentInParent<Sekai.Live.LiveFrontView>();
				Transform front = view != null ? view.transform.Find("FrontCamera") : null;
				_camera = front != null ? front.GetComponent<Camera>() : Camera.main;
				if (_camera == null)
				{
					return;
				}
			}
			float halfHeight = _camera.orthographicSize;
			float halfWidth = halfHeight * _camera.aspect;
			Vector3 Corner(float x, float y) => transform.InverseTransformPoint(_camera.transform.TransformPoint(new Vector3(x, y, 0f)));
			Vector3[] outer = { Corner(-halfWidth, -halfHeight), Corner(halfWidth, -halfHeight), Corner(halfWidth, halfHeight), Corner(-halfWidth, halfHeight) };
			float w = borderWidth;
			Vector3[] inner =
			{
				Corner(-halfWidth + w, -halfHeight + w), Corner(halfWidth - w, -halfHeight + w),
				Corner(halfWidth - w, halfHeight - w), Corner(-halfWidth + w, halfHeight - w),
			};

			Begin();
			float perimeter = 4f * (halfWidth + halfHeight);
			float walked = 0f;
			int perSide = BorderSegments / 4;
			for (int side = 0; side < 4; side++)
			{
				int next = (side + 1) % 4;
				float length = Vector3.Distance(outer[side], outer[next]);
				int start = _vertices.Count;
				for (int i = 0; i <= perSide; i++)
				{
					float t = i / (float)perSide;
					Color c = Rainbow((walked + t * length) / perimeter * 2f + Time.unscaledTime * 0.4f);
					_vertices.Add(Vector3.Lerp(outer[side], outer[next], t));
					_vertices.Add(Vector3.Lerp(inner[side], inner[next], t));
					_colors.Add(c);
					_colors.Add(new Color(c.r, c.g, c.b, 0.15f));
					if (i > 0)
					{
						int a = start + (i - 1) * 2;
						Quad(a, a + 2, a + 3, a + 1);
					}
				}
				walked += length;
			}
			End(_borderMesh);
		}

		/* ---------- helpers ---------- */

		private static Color Rainbow(float t)
		{
			Color color = Color.HSVToRGB(Mathf.Repeat(t, 1f), 0.55f, 1f);
			color.a = 0.95f;
			return color;
		}

		private IEnumerable<GameObject> Lines()
		{
			foreach (GameObject line in feverLines ?? System.Array.Empty<GameObject>())
			{
				yield return line;
			}
			foreach (GameObject line in superFeverLines ?? System.Array.Empty<GameObject>())
			{
				yield return line;
			}
		}

		private void Begin()
		{
			_vertices.Clear();
			_colors.Clear();
			_triangles.Clear();
		}

		private void Quad(int a, int b, int c, int d)
		{
			_triangles.Add(a);
			_triangles.Add(b);
			_triangles.Add(c);
			_triangles.Add(a);
			_triangles.Add(c);
			_triangles.Add(d);
		}

		private void End(Mesh mesh)
		{
			mesh.Clear();
			mesh.SetVertices(_vertices);
			mesh.SetColors(_colors);
			mesh.SetTriangles(_triangles, 0);
			mesh.RecalculateBounds();
		}

		private static GameObject CreateMeshObject(string name, Transform parent, Material material, int sortingOrder, out Mesh mesh)
		{
			GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
			go.transform.SetParent(parent, false);
			mesh = new Mesh { name = name };
			mesh.MarkDynamic();
			go.GetComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer renderer = go.GetComponent<MeshRenderer>();
			renderer.sharedMaterial = material;
			renderer.sortingOrder = sortingOrder;
			return go;
		}

		private static void SetActive(GameObject target, bool active)
		{
			if (target != null && target.activeSelf != active)
			{
				target.SetActive(active);
			}
		}
	}
}
