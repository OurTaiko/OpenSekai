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
	/// FEVER CHANCE, FEVER and SUPER FEVER on the lane, from the original's マルチライブ UI (<c>FrontMultiUIView</c>'s
	/// <c>FeverView</c>): the frames at both sides of the screen light up, and the FEVER or SUPER FEVER mark and lines
	/// show while it lasts. The serialized fields up to <see cref="feverChanceFrames"/> keep the original's names; the
	/// rest are OpenSekai's, and the texts are placeholders for the original's FEVER sprites, which the project does
	/// not have.
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
		private TextMeshPro chanceText;

		[SerializeField]
		private Color chanceColor = new Color32(0xFF, 0xEE, 0x88, 0xFF);

		[SerializeField]
		private Color feverColor = new Color32(0xFF, 0xCC, 0x44, 0xFF);

		[SerializeField]
		private Color superFeverColor = new Color32(0xFF, 0x88, 0xDD, 0xFF);

		[SerializeField]
		[Range(0f, 1f)]
		private float frameAlpha = 0.35f;

		private CoopFeverPhase _phase = (CoopFeverPhase)(-1);

		private void Awake()
		{
			Show(CoopFeverPhase.None);
		}

		public void Show(CoopFeverPhase phase)
		{
			if (phase == _phase)
			{
				return;
			}
			_phase = phase;
			bool on = phase != CoopFeverPhase.None;
			if (feverChanceRoot != null)
			{
				feverChanceRoot.gameObject.SetActive(on);
			}
			SetActive(chanceText != null ? chanceText.gameObject : null, phase == CoopFeverPhase.Chance);
			SetActive(feverLaneText != null ? feverLaneText.gameObject : null, phase == CoopFeverPhase.Fever);
			SetActive(superFeverLaneText != null ? superFeverLaneText.gameObject : null, phase == CoopFeverPhase.SuperFever);
			foreach (GameObject line in feverLines ?? System.Array.Empty<GameObject>())
			{
				SetActive(line, phase == CoopFeverPhase.Fever);
			}
			foreach (GameObject line in superFeverLines ?? System.Array.Empty<GameObject>())
			{
				SetActive(line, phase == CoopFeverPhase.SuperFever);
			}
			UpdateFrames();
		}

		private void Update()
		{
			if (_phase == CoopFeverPhase.Chance)
			{
				UpdateFrames();
				if (chanceText != null)
				{
					float pulse = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 8f);
					chanceText.transform.localScale = new Vector3(pulse, pulse, 1f);
				}
			}
		}

		private void UpdateFrames()
		{
			Color color = _phase == CoopFeverPhase.SuperFever ? superFeverColor
				: _phase == CoopFeverPhase.Fever ? feverColor
				: chanceColor;
			float alpha = _phase == CoopFeverPhase.None ? 0f : frameAlpha;
			if (_phase == CoopFeverPhase.Chance)
			{
				alpha *= 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
			}
			color.a = alpha;
			foreach (SpriteRenderer frame in feverChanceFrames ?? System.Array.Empty<SpriteRenderer>())
			{
				if (frame != null)
				{
					frame.color = color;
				}
			}
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
