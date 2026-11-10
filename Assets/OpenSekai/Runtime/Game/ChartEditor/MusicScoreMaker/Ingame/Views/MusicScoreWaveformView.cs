using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.MusicScoreMaker.Ingame.Views
{
	/// <summary>
	/// The audio's waveform along the track, behind the lane and bar lines: each row's width is the loudest sample in
	/// the audio time that row covers (tick → chart time through the BPM changes, plus fillerSec). It lines up with the
	/// bar lines when fillerSec and the BPM are right.
	/// </summary>
	[RequireComponent(typeof(CanvasRenderer))]
	public class MusicScoreWaveformView : MaskableGraphic
	{
		private const float RowHeight = 3f;
		private const int MinRows = 32;
		private const int MaxRows = 720;

		/// <summary>Width of the loudest peak, as a fraction of the track's width.</summary>
		[SerializeField]
		private float _maxWidthRate = 0.92f;

		private float[] _rowY = new float[0];
		private float[] _rowWidth = new float[0];
		private int _rowCount;

		public void UpdateView(long startTicks, long endTicks, MusicScoreMakerData musicScore)
		{
			int previousCount = _rowCount;
			_rowCount = 0;
			if (musicScore != null && endTicks > startTicks && MusicScoreMakerSettingsManager.ShowWaveform && MusicScoreMakerWaveform.HasData)
			{
				Fill(startTicks, endTicks, musicScore);
			}
			if (_rowCount > 0 || previousCount > 0)
			{
				SetVerticesDirty();
			}
		}

		private void Fill(long startTicks, long endTicks, MusicScoreMakerData musicScore)
		{
			Rect rect = rectTransform.rect;
			if (rect.height <= 0f || rect.width <= 0f)
			{
				return;
			}
			int rows = Mathf.Clamp(Mathf.CeilToInt(rect.height / RowHeight), MinRows, MaxRows);
			if (_rowY.Length < rows + 1)
			{
				_rowY = new float[rows + 1];
				_rowWidth = new float[rows];
			}

			(long ticks, float bpm)[] timeline = MusicScoreMakerUtility.BuildBpmTimeline(musicScore.MusicScoreEventDataList);
			float fillerSec = MusicScoreMakerWaveform.FillerSec;
			long range = endTicks - startTicks;
			double previousTime = 0;
			for (int i = 0; i <= rows; i++)
			{
				long ticks = startTicks + range * i / rows;
				_rowY[i] = rect.yMin + MusicScoreMakerUtility.CalcNormalizedPositionFromTicks(startTicks, endTicks, ticks) * rect.height;
				double time = MusicScoreMakerUtility.GetTimeFromTicksOnTimeline(ticks, timeline) + fillerSec;
				if (i > 0)
				{
					_rowWidth[i - 1] = MusicScoreMakerWaveform.GetPeak(previousTime, time) * rect.width * _maxWidthRate;
				}
				previousTime = time;
			}
			_rowCount = rows;
		}

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();
			if (_rowCount <= 0)
			{
				return;
			}
			Rect rect = rectTransform.rect;
			float centerX = rect.center.x;
			Color32 c = color;
			for (int i = 0; i < _rowCount; i++)
			{
				float half = _rowWidth[i] * 0.5f;
				if (half < 0.5f)
				{
					continue;
				}
				float y0 = Mathf.Max(_rowY[i], rect.yMin);
				float y1 = Mathf.Min(_rowY[i + 1], rect.yMax);
				if (y1 <= y0)
				{
					continue;
				}
				int index = vh.currentVertCount;
				vh.AddVert(new Vector3(centerX - half, y0), c, Vector2.zero);
				vh.AddVert(new Vector3(centerX - half, y1), c, Vector2.zero);
				vh.AddVert(new Vector3(centerX + half, y1), c, Vector2.zero);
				vh.AddVert(new Vector3(centerX + half, y0), c, Vector2.zero);
				vh.AddTriangle(index, index + 1, index + 2);
				vh.AddTriangle(index + 2, index + 3, index);
			}
		}
	}
}
