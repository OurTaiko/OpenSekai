namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
	using TouchPhase = UnityEngine.InputSystem.TouchPhase;

	public static class MusicScoreMakerInputUtility
	{
		public static bool IsInputStarted()
		{
			if (NativeInput.GetMouseButtonDown(0) || NativeInput.GetMouseButtonDown(1))
			{
				return true;
			}
			var touches = NativeInput.ActiveTouches;
			for (int i = 0; i < touches.Count; i++)
			{
				if (touches[i].phase == TouchPhase.Began)
				{
					return true;
				}
			}
			return false;
		}

		public static bool IsInputEnded()
		{
			if (NativeInput.GetMouseButtonUp(0) || NativeInput.GetMouseButtonUp(1))
			{
				return true;
			}
			var touches = NativeInput.ActiveTouches;
			if (touches.Count == 0)
			{
				return true;
			}
			for (int i = 0; i < touches.Count; i++)
			{
				TouchPhase phase = touches[i].phase;
				if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
				{
					return true;
				}
			}
			return false;
		}

		public static bool IsMultipleInputStarted()
		{
			if ((NativeInput.GetMouseButtonDown(0) && NativeInput.GetMouseButton(1))
				|| (NativeInput.GetMouseButtonDown(1) && NativeInput.GetMouseButton(0)))
			{
				return true;
			}
			var touches = NativeInput.ActiveTouches;
			if (!NativeInput.TouchSupported || touches.Count < 2)
			{
				return false;
			}
			for (int i = 0; i < touches.Count; i++)
			{
				if (touches[i].phase == TouchPhase.Began)
				{
					return true;
				}
			}
			return false;
		}

		public static bool IsMultipleInputActive()
		{
			if (NativeInput.GetMouseButton(0) && NativeInput.GetMouseButton(1))
			{
				return true;
			}
			return NativeInput.TouchSupported && NativeInput.TouchCount > 1;
		}
	}
}
