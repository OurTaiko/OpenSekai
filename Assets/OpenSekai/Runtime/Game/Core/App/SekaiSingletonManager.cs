using CP;
using UnityEngine;

namespace Sekai
{
	public class SekaiSingletonManager : MonoBehaviour
	{
		private static SekaiSingletonManager instance;
		private static float deltaK = 1f;

		public static float DeltaTimeK => deltaK;

		private void Awake()
		{
			if (instance != null && instance != this)
			{
				Destroy(gameObject);
				return;
			}

			instance = this;
			DontDestroyOnLoad(gameObject);
			OnInitialize();
		}

		protected virtual void OnInitialize()
		{
			FramerateUtility.SetFrameRate();
			TextMeshProUtility.SetupBuiltinFontAsset();
			GameBootstrap.Initialize();
		}

		private void Update()
		{
			// An unlimited live sets targetFrameRate to -1; measure against the default rate then.
			int targetFrameRate = Application.targetFrameRate > 0 ? Application.targetFrameRate : FramerateUtility.DefaultFrameRate;
			deltaK = Time.timeScale * Time.deltaTime * targetFrameRate;
		}

		public virtual void OnFinalize()
		{
		}

		private void OnDestroy()
		{
			if (instance != this)
			{
				return;
			}

			OnFinalize();
			instance = null;
		}

		private void OnApplicationQuit()
		{
			instance = null;
		}

		private void OnApplicationPause(bool pauseStatus)
		{
			if (!pauseStatus)
			{
				System.GC.Collect();
			}
		}
	}
}
