using System;
using DG.Tweening;
using UnityEngine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Sekai
{
	public class LiveSpriteButton : MonoBehaviour
	{
		private struct ButtonBounds
		{
			public float left;

			public float bottom;

			public float right;

			public float top;
		}

		private static readonly int DisableId;

		[SerializeField]
		private SpriteRenderer spriteRenderer;

		[SerializeField]
		protected float pressScale;

		[SerializeField]
		protected float raycastPadding;

		private float scaleTime;

		private Action onClick;

		private bool isEnable;

		private int touchId;

		private ButtonBounds bounds;

		private Vector3 baseScale;

		private Tweener pressTween;

		public void Setup(Action onClick)
		{
			this.onClick = onClick;
			touchId = DisableId;
			baseScale = transform.localScale;
		}

		public void CalculateBounds(Camera camera)
		{
			isEnable = true;
			if (spriteRenderer == null)
			{
				spriteRenderer = GetComponent<SpriteRenderer>();
			}
			if (spriteRenderer == null || camera == null)
			{
				isEnable = false;
				return;
			}
			Bounds rendererBounds = spriteRenderer.bounds;
			Vector3 min = camera.WorldToScreenPoint(rendererBounds.min);
			Vector3 max = camera.WorldToScreenPoint(rendererBounds.max);
			bounds.left = Mathf.Min(min.x, max.x) - raycastPadding;
			bounds.right = Mathf.Max(min.x, max.x) + raycastPadding;
			bounds.bottom = Mathf.Min(min.y, max.y) - raycastPadding;
			bounds.top = Mathf.Max(min.y, max.y) + raycastPadding;
		}

		private void UpdateMouse()
		{
			if (!isEnable)
			{
				return;
			}
			if (NativeInput.GetMouseButtonDown(0) && touchId == DisableId && Contains(NativeInput.PointerPosition))
			{
				ScaleDown();
				touchId = 0;
			}
			else if (NativeInput.GetMouseButtonUp(0) && touchId == 0)
			{
				if (Contains(NativeInput.PointerPosition))
				{
					OnClick();
					ScaleUp();
				}
				else
				{
					ScaleUp();
				}
				touchId = DisableId;
			}
		}

		private void UpdateTouches()
		{
			if (!isEnable)
			{
				return;
			}
			var touches = NativeInput.ActiveTouches;
			for (int i = 0; i < touches.Count; i++)
			{
				Touch touch = touches[i];
				Vector2 position = touch.screenPosition;
				if (touch.phase == TouchPhase.Began)
				{
					if (touchId == DisableId && Contains(position))
					{
						ScaleDown();
						touchId = touch.finger.index;
						return;
					}
				}
				else if (touch.phase == TouchPhase.Ended)
				{
					if (touchId == touch.finger.index)
					{
						if (Contains(position))
						{
							OnClick();
						}
						ScaleUp();
						touchId = DisableId;
						return;
					}
				}
			}
		}

		private void ScaleDown()
		{
			KillPressTween();
			pressTween = transform.DOScale(baseScale * pressScale, scaleTime);
		}

		private void ScaleUp()
		{
			KillPressTween();
			pressTween = transform.DOScale(baseScale, scaleTime);
		}

		private void KillPressTween()
		{
			if (pressTween != null && pressTween.IsActive())
			{
				pressTween.Kill();
			}
		}

		private void LateUpdate()
		{
			UpdateTouches();
			if (NativeInput.TouchCount == 0)
			{
				UpdateMouse();
			}
		}

		public void OnClick()
		{
			onClick?.Invoke();
		}

		public LiveSpriteButton()
		{
			scaleTime = 0.125f;
			pressScale = 0.95f;
			raycastPadding = 24f;
			touchId = DisableId;
		}

		static LiveSpriteButton()
		{
			DisableId = int.MinValue;
		}

		private bool Contains(Vector2 position)
		{
			return bounds.left <= position.x
				&& bounds.right >= position.x
				&& bounds.bottom <= position.y
				&& bounds.top >= position.y;
		}
	}
}
