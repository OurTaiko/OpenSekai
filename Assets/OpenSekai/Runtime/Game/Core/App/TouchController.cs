using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Sekai
{
	public class TouchController : MonoBehaviour
	{
		public Action<Vector2> OnTouch;

		public Action<bool> OnHit;

		private GameObject hitTarget;

		private bool _isCheckChild;

		private void Update()
		{
			CheckTouch();
		}

		private void CheckTouch()
		{
			var touches = NativeInput.ActiveTouches;
			if (touches.Count >= 1)
			{
				Touch touch = touches[0];
				if (touch.phase == TouchPhase.Began)
				{
					OnTouch?.Invoke(touch.screenPosition);
					CheckHit(touch.screenPosition);
				}

				return;
			}

			if (NativeInput.GetMouseButtonDown(0))
			{
				Vector2 position = NativeInput.PointerPosition;
				OnTouch?.Invoke(position);
				CheckHit(position);
			}
		}

		public void SetHitTarget(GameObject target, bool isCheckChild = false)
		{
			hitTarget = target;
			_isCheckChild = isCheckChild;
		}

		private void CheckHit(Vector2 touchPoint)
		{
			if (EventSystem.current == null)
			{
				OnHit?.Invoke(false);
				return;
			}

			List<RaycastResult> raycastResults = new List<RaycastResult>();
			PointerEventData eventData = new PointerEventData(EventSystem.current)
			{
				position = touchPoint
			};
			EventSystem.current.RaycastAll(eventData, raycastResults);
			OnHit?.Invoke(IsHit(raycastResults));
		}

		private bool IsHit(List<RaycastResult> raycastResults)
		{
			if (hitTarget == null || raycastResults == null)
			{
				return false;
			}

			foreach (RaycastResult result in raycastResults)
			{
				GameObject target = result.gameObject;
				if (target == null)
				{
					continue;
				}

				if (_isCheckChild)
				{
					if (target.transform.IsChildOf(hitTarget.transform))
					{
						return true;
					}
				}
				else if (target == hitTarget)
				{
					return true;
				}
			}

			return false;
		}

		public TouchController()
		{
		}
	}
}
