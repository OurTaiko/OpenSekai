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
			deltaK = Time.timeScale * Time.deltaTime * Application.targetFrameRate;
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
