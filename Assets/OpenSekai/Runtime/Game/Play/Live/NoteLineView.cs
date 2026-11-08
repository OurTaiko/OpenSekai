using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class NoteLineView : MonoBehaviour
	{
		private struct TwoPoint
		{
			public Vector2 Left;

			public Vector2 Right;
		}

		private struct Setting
		{
			public float CurrentViewProgress;

			public TwoPoint LanePosition;

			public float LineProgress { get; set; }
		}

		private struct Trapezoid
		{
			public TwoPoint Top;

			public TwoPoint Bottom;
		}

		private const int TCount = 3;
		private const int DefaultSplitMeshCount = 100;
		private const int DefaultPoolMultiplier = 10;
		private const int TrapezoidVertexCount = 8;
		private const int TrapezoidIndexCount = 18;
		private const int DefaultListCapacity = 64;

		private static readonly Color PressColor = Color.white;
		private static readonly Color ReleaseColor = new Color(0.60000002f, 0.70664f, 0.65332f, 1f);
		private static readonly Vector2[] offsetValue =
		{
			new Vector2(0f, 0.125f),
			new Vector2(0.875f, 1f)
		};

		private int SplitMeshCount;
		private int PoolCount;
		private Renderer renderer;
		private MeshFilter filter;
		private Mesh mesh;
		private Vector3[] vertices;
		private Vector2[] uv;
		private int[] triangles;
		private List<NoteBase> longNotes;
		private Vector2[] leftPoint;
		private Vector2[] rightPoint;
		private Vector2[] uvOffsetLeft;
		private Vector2[] uvOffsetRight;
		private Color[] colors;
		private Vector2 statusValue;
		private Setting[] meshSettingBuffer;
		private int meshCount;
		private Trapezoid[] trapezoidVertexInfo;
		private LiveBundleBuildData bundleBuildData;

		public Material Material { get; private set; }

		private static int GetSplitMeshCount(bool isCustomMusicScore)
		{
			return DefaultSplitMeshCount;
		}

		private static int GetPoolMultiplier(bool isCustomMusicScore)
		{
			return DefaultPoolMultiplier;
		}

		public void Setup(LiveBundleBuildData bundleBuildData, string textureName, float alpha, bool isCustomMusicScore = false)
		{
			SplitMeshCount = Mathf.Max(1, GetSplitMeshCount(isCustomMusicScore));
			PoolCount = Mathf.Max(1, SplitMeshCount * GetPoolMultiplier(isCustomMusicScore));
			this.bundleBuildData = bundleBuildData;

			filter = gameObject.AddComponent<MeshFilter>();
			renderer = gameObject.AddComponent<MeshRenderer>();

			Material source = Resources.Load<Material>("Materials/Ingame/LongNoteLine")
				?? Resources.Load<Material>("materials/ingame/LongNoteLine");
			Material = source != null ? new Material(source) : new Material(Shader.Find("Sprites/Default"));
			Texture texture = AssetBundleUtility.LoadAsset<Texture>(LiveConfig.NoteBundleName, textureName, false);
			if (texture != null)
			{
				Material.mainTexture = texture;
			}

			Material.color = new Color(1f, 1f, 1f, alpha);
			renderer.material = Material;
			Material = renderer.material;

			longNotes = new List<NoteBase>(DefaultListCapacity);
			SetupVertexArray();

			mesh = new Mesh { name = name + "Mesh" };
			mesh.vertices = vertices;
			mesh.colors = colors;
			mesh.uv = uv;
			mesh.triangles = triangles;
			mesh.MarkDynamic();
			filter.mesh = mesh;
			meshSettingBuffer = new Setting[SplitMeshCount + 10];
			trapezoidVertexInfo = new Trapezoid[2];
			meshCount = 0;
		}

		private void SetupVertexArray()
		{
			int vertexCount = PoolCount * TrapezoidVertexCount;
			vertices = new Vector3[vertexCount];
			leftPoint = new Vector2[vertexCount];
			rightPoint = new Vector2[vertexCount];
			uvOffsetLeft = new Vector2[vertexCount];
			uvOffsetRight = new Vector2[vertexCount];
			uv = new Vector2[vertexCount];
			colors = new Color[vertexCount];
			triangles = new int[PoolCount * TrapezoidIndexCount];

			ClearVertex(0, PoolCount);
			for (int meshIndex = 0; meshIndex < PoolCount; meshIndex++)
			{
				int vertexBase = TrapezoidVertexCount * meshIndex;
				for (int i = 0; i < TrapezoidVertexCount; i++)
				{
					uv[vertexBase + i] = new Vector2(0f, i & 1);
					colors[vertexBase + i] = Color.white;
				}

				int triangleBase = TrapezoidIndexCount * meshIndex;
				for (int t = 0; t < TCount; t++)
				{
					int quadVertex = vertexBase + t * 2;
					int quadIndex = triangleBase + t * 6;
					triangles[quadIndex] = quadVertex;
					triangles[quadIndex + 1] = quadVertex + 1;
					triangles[quadIndex + 2] = quadVertex + 2;
					triangles[quadIndex + 3] = quadVertex + 1;
					triangles[quadIndex + 4] = quadVertex + 3;
					triangles[quadIndex + 5] = quadVertex + 2;
				}
			}
		}

		public void Add(LongNote note)
		{
			if (note != null)
			{
				longNotes.Add(note);
			}
		}

		public void Remove(LongNote note)
		{
			longNotes?.Remove(note);
		}

		public void Add(GuideNote note)
		{
			if (note != null)
			{
				longNotes.Add(note);
			}
		}

		public void Remove(GuideNote note)
		{
			longNotes?.Remove(note);
		}

		public void Clear()
		{
			longNotes?.Clear();
			ClearVertex(0, PoolCount);
			ApplyMesh();
		}

		public void Excute(float time)
		{
			if (mesh == null || longNotes == null)
			{
				return;
			}

			int previousMeshCount = meshCount;
			meshCount = 0;
			for (int i = 0; i < longNotes.Count; i++)
			{
				NextExecute(longNotes[i], time, ref meshCount);
			}

			if (previousMeshCount > meshCount)
			{
				ClearVertex(meshCount, previousMeshCount - meshCount);
			}

			ApplyMesh();
		}

		private void NextExecute(INote baseNote, float time, ref int meshCount)
		{
			List<INote> viewNotes = baseNote?.ViewNoteList;
			if (viewNotes == null || viewNotes.Count < 2)
			{
				return;
			}

			INote startNote = viewNotes[0];
			for (int i = 1; i < viewNotes.Count; i++)
			{
				INote endNote = viewNotes[i];
				if (endNote == null)
				{
					continue;
				}

				if (endNote.IsSkip)
				{
					continue;
				}

				if (meshCount >= PoolCount)
				{
					return;
				}

				DrawSegment(time, baseNote, startNote, endNote, ref meshCount);
				startNote = endNote;
			}
		}

		private void MeshCreate(ref int meshCount, float time, INote baseNote, int meshDataCount)
		{
			if (meshDataCount < 2)
			{
				return;
			}

			SetStatusValue(time, baseNote);
			Color color = baseNote != null && baseNote.State == NoteState.Release ? ReleaseColor : PressColor;
			for (int i = 0; i < meshDataCount - 1 && meshCount < PoolCount; i++)
			{
				int vertexIndex = meshCount * TrapezoidVertexCount;
				CalculateTrapezoidVertex(meshSettingBuffer[i], meshSettingBuffer[i + 1]);
				UpdateVertex(vertexIndex, color);
				UpdateUV(vertexIndex, i);
				meshCount++;
			}
		}

		private void UpdateUV(int vertexIndex, int meshDataIndex)
		{
			float startLineProgress = meshSettingBuffer[meshDataIndex].LineProgress;
			float endLineProgress = meshSettingBuffer[meshDataIndex + 1].LineProgress;
			for (int offset = 0; offset <= 4; offset += 4)
			{
				uv[vertexIndex + offset + 3].y = endLineProgress;
				uv[vertexIndex + offset + 1].y = endLineProgress;
				uv[vertexIndex + offset + 2].y = startLineProgress;
				uv[vertexIndex + offset].y = startLineProgress;
			}
		}

		private void ClearVertex(int startIndex, int length)
		{
			if (vertices == null)
			{
				return;
			}

			int start = Mathf.Max(0, startIndex * TrapezoidVertexCount);
			int end = Mathf.Min(vertices.Length, start + Mathf.Max(0, length) * TrapezoidVertexCount);
			for (int i = start; i < end; i++)
			{
				vertices[i] = Vector3.zero;
				colors[i] = Color.clear;
			}
		}

		private void SetStatusValue(float time, INote baseNote)
		{
			float animationRate = 0f;
			if (baseNote != null && baseNote.State > NoteState.InputBegan)
			{
				float phase = (time - baseNote.MusicScoreInfo.time) * Mathf.PI * 2f;
				animationRate = 0.5f - Mathf.Cos(phase * 2f) * 0.5f;
			}

			statusValue.x = animationRate;
			statusValue.y = baseNote != null && baseNote.Type == NoteType.Critical ? 0.025f : 0.525f;
		}

		private void CalculateTrapezoidVertex(Setting startSetting, Setting endSetting)
		{
			trapezoidVertexInfo[0].Top.Right = endSetting.LanePosition.Left;
			trapezoidVertexInfo[0].Top.Left = endSetting.LanePosition.Left;
			trapezoidVertexInfo[0].Bottom.Right = startSetting.LanePosition.Left;
			trapezoidVertexInfo[0].Bottom.Left = startSetting.LanePosition.Left;
			trapezoidVertexInfo[0].Top.Left.x -= endSetting.CurrentViewProgress * 0.4f;
			trapezoidVertexInfo[0].Bottom.Left.x -= startSetting.CurrentViewProgress * 0.4f;

			trapezoidVertexInfo[1].Top.Right = endSetting.LanePosition.Right;
			trapezoidVertexInfo[1].Top.Left = endSetting.LanePosition.Right;
			trapezoidVertexInfo[1].Bottom.Right = startSetting.LanePosition.Right;
			trapezoidVertexInfo[1].Bottom.Left = startSetting.LanePosition.Right;
			trapezoidVertexInfo[1].Top.Right.x += endSetting.CurrentViewProgress * 0.4f;
			trapezoidVertexInfo[1].Bottom.Right.x += startSetting.CurrentViewProgress * 0.4f;
		}

		private void UpdateVertex(int vertexIndex, Color color)
		{
			for (int t = 0; t < 2; t++)
			{
				int offset = vertexIndex + t * 4;
				Trapezoid trapezoid = trapezoidVertexInfo[t];
				vertices[offset + 3] = ToVector3(trapezoid.Top.Right);
				vertices[offset + 1] = ToVector3(trapezoid.Top.Left);
				vertices[offset + 2] = ToVector3(trapezoid.Bottom.Right);
				vertices[offset] = ToVector3(trapezoid.Bottom.Left);

				leftPoint[offset + 3].y = trapezoid.Top.Left.x;
				leftPoint[offset + 1].y = trapezoid.Top.Left.x;
				leftPoint[offset + 2].y = trapezoid.Bottom.Left.x;
				leftPoint[offset].y = trapezoid.Bottom.Left.x;

				rightPoint[offset + 3].y = trapezoid.Top.Right.x;
				rightPoint[offset + 1].y = trapezoid.Top.Right.x;
				rightPoint[offset + 2].y = trapezoid.Bottom.Right.x;
				rightPoint[offset].y = trapezoid.Bottom.Right.x;

				Vector2 offsetRange = offsetValue[t];
				for (int i = 0; i < 4; i++)
				{
					int index = offset + i;
					uvOffsetLeft[index] = new Vector2(statusValue.x, offsetRange.x);
					uvOffsetRight[index] = new Vector2(statusValue.y, offsetRange.y);
					colors[index] = color;
				}
			}
		}

		/// <summary>
		/// Draws the body between two consecutive notes of a hold or guide. Every point of the body reaches
		/// the judge line at its own score time, so its screen position comes from the scroll timeline: with a
		/// hi-speed change inside the segment the body bends, and with negative hi-speed it can fold back or
		/// leave the screen and return, giving several separate visible runs. The timeline is linear between
		/// hi-speed changes, so each such piece is clipped to the screen exactly and then sampled.
		/// </summary>
		private void DrawSegment(float time, INote baseNote, INote startNote, INote endNote, ref int meshCount)
		{
			if (startNote is not NoteBase startNoteBase || endNote is not NoteBase endNoteBase)
			{
				return;
			}

			NoteScrollTimeline timeline = startNoteBase.ScrollTimeline;
			float startTime = startNote.MusicScoreInfo.time;
			float endTime = endNote.MusicScoreInfo.time;
			float duration = endTime - startTime;
			if (timeline == null || duration <= 0f)
			{
				return;
			}

			// The part that already reached the judge line has been consumed.
			float pieceStart = Mathf.Max(startTime, time);
			if (pieceStart >= endTime)
			{
				return;
			}

			var segment = new SegmentContext
			{
				Timeline = timeline,
				DisplayTime = startNoteBase.ScrollDisplayTime,
				Now = time,
				StartNote = startNote,
				StartTime = startTime,
				Duration = duration,
				StartSpeedRatio = startNoteBase.speedRatio,
				EndSpeedRatio = endNoteBase.speedRatio,
				StartCenterLane = (startNote.DefaultLeftLane + startNote.DefaultRightLane) * 0.5f,
				EndCenterLane = (endNote.DefaultLeftLane + endNote.DefaultRightLane) * 0.5f,
				StartWidth = startNote.DefaultRightLane - startNote.DefaultLeftLane,
				EndWidth = endNote.DefaultRightLane - endNote.DefaultLeftLane,
			};
			List<INote> viewNotes = startNote.ParentNote?.ViewNoteList ?? startNote.ViewNoteList;
			segment.ViewIndex = viewNotes != null ? Mathf.Max(0, viewNotes.IndexOf(startNote)) : 0;
			segment.ViewLastIndex = Mathf.Max(1, (viewNotes?.Count ?? 2) - 1);

			int sampleCount = 0;
			while (pieceStart < endTime)
			{
				float pieceEnd = Mathf.Min(timeline.NextChangeAfter(pieceStart), endTime);
				float startProgress = segment.ProgressAt(pieceStart);
				float endProgress = segment.ProgressAt(pieceEnd);

				if (!TryClipToScreen(startProgress, endProgress, out float enter, out float exit))
				{
					FlushRun(ref sampleCount, ref meshCount, time, baseNote);
				}
				else
				{
					if (enter > 0f)
					{
						// Re-entering the screen inside this piece: not connected to the previous run.
						FlushRun(ref sampleCount, ref meshCount, time, baseNote);
					}

					float runStart = Mathf.Lerp(pieceStart, pieceEnd, enter);
					float runEnd = Mathf.Lerp(pieceStart, pieceEnd, exit);
					float progressSpan = Mathf.Abs(Mathf.Lerp(startProgress, endProgress, exit) - Mathf.Lerp(startProgress, endProgress, enter));
					int steps = Mathf.Max(1, Mathf.CeilToInt(progressSpan * SplitMeshCount));
					for (int step = sampleCount == 0 ? 0 : 1; step <= steps; step++)
					{
						if (sampleCount == meshSettingBuffer.Length)
						{
							// Buffer full: draw what we have and continue from its last sample.
							Setting last = meshSettingBuffer[sampleCount - 1];
							FlushRun(ref sampleCount, ref meshCount, time, baseNote);
							meshSettingBuffer[0] = last;
							sampleCount = 1;
						}

						float sampleTime = Mathf.Lerp(runStart, runEnd, (float)step / steps);
						meshSettingBuffer[sampleCount++] = segment.CreateSetting(sampleTime);
					}

					if (exit < 1f)
					{
						// Leaves the screen inside this piece.
						FlushRun(ref sampleCount, ref meshCount, time, baseNote);
					}
				}

				if (meshCount >= PoolCount)
				{
					return;
				}

				pieceStart = pieceEnd;
			}

			FlushRun(ref sampleCount, ref meshCount, time, baseNote);
		}

		private void FlushRun(ref int sampleCount, ref int meshCount, float time, INote baseNote)
		{
			if (sampleCount >= 2)
			{
				MeshCreate(ref meshCount, time, baseNote, sampleCount);
			}
			sampleCount = 0;
		}

		// Part of a straight piece (progress going linearly from start to end) that is on screen, as fractions
		// of the piece. False when none of it is.
		private static bool TryClipToScreen(float startProgress, float endProgress, out float enter, out float exit)
		{
			float delta = endProgress - startProgress;
			if (Mathf.Abs(delta) < 1e-6f)
			{
				enter = 0f;
				exit = 1f;
				return startProgress >= 0f && startProgress <= 1f;
			}

			float atSpawnLine = -startProgress / delta;
			float atJudgeLine = (1f - startProgress) / delta;
			enter = Mathf.Max(Mathf.Min(atSpawnLine, atJudgeLine), 0f);
			exit = Mathf.Min(Mathf.Max(atSpawnLine, atJudgeLine), 1f);
			return enter < exit;
		}

		private struct SegmentContext
		{
			public NoteScrollTimeline Timeline;
			public float DisplayTime;
			public float Now;
			public INote StartNote;
			public float StartTime;
			public float Duration;
			public float StartSpeedRatio;
			public float EndSpeedRatio;
			public float StartCenterLane;
			public float EndCenterLane;
			public float StartWidth;
			public float EndWidth;
			public int ViewIndex;
			public int ViewLastIndex;

			public float ProgressAt(float scoreTime)
			{
				float speedRatio = Mathf.Lerp(StartSpeedRatio, EndSpeedRatio, (scoreTime - StartTime) / Duration);
				return Timeline.ProgressAt(Now, scoreTime, speedRatio, DisplayTime);
			}

			public Setting CreateSetting(float scoreTime)
			{
				float lineProgress = Mathf.Clamp01((scoreTime - StartTime) / Duration);
				float centerProgress = LiveConfig.GetNoteLineCenterProgress(lineProgress, StartNote);
				float viewProgress = Mathf.Min(LiveConfig.GetNoteViewProgress(ProgressAt(scoreTime)), 1f);
				float centerLane = Mathf.Lerp(StartCenterLane, EndCenterLane, centerProgress);
				float width = Mathf.Lerp(StartWidth, EndWidth, centerProgress);
				Vector2 lanePosition = GetLanePosition(centerLane);
				Vector2 viewPosition = LiveUtility.EarlyVec2Lerp(LiveConfig.SpawnPosition, lanePosition, viewProgress);
				float halfWidth = viewProgress * (((width + 1f) * LiveConfig.widthX) - 0.1f) * 0.5f;

				return new Setting
				{
					CurrentViewProgress = viewProgress,
					LanePosition = new TwoPoint
					{
						Left = new Vector2(viewPosition.x - halfWidth, viewPosition.y),
						Right = new Vector2(viewPosition.x + halfWidth, viewPosition.y)
					},
					LineProgress = (lineProgress + ViewIndex) / ViewLastIndex
				};
			}
		}

		private static Vector2 GetLanePosition(float lane)
		{
			Vector2[] positions = LiveConfig.JudgmentPositions;
			if (positions == null || positions.Length == 0)
			{
				return Vector2.zero;
			}

			float clamped = Mathf.Clamp(lane, 0f, positions.Length - 1);
			int left = Mathf.Clamp(Mathf.FloorToInt(clamped), 0, positions.Length - 1);
			int right = Mathf.Clamp(Mathf.CeilToInt(clamped), 0, positions.Length - 1);
			return LiveUtility.EarlyVec2Lerp(positions[left], positions[right], clamped - left);
		}

		private static Vector3 ToVector3(Vector2 value)
		{
			return new Vector3(value.x, value.y, 0f);
		}

		private void ApplyMesh()
		{
			if (mesh == null)
			{
				return;
			}

			mesh.vertices = vertices;
			mesh.colors = colors;
			mesh.uv = uv;
			mesh.uv2 = leftPoint;
			mesh.uv3 = rightPoint;
			mesh.uv4 = uvOffsetLeft;
			mesh.uv5 = uvOffsetRight;
			filter.mesh = mesh;
		}
	}
}
