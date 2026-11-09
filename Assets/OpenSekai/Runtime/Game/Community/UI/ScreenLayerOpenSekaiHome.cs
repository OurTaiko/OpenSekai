using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.Community
{
	/// <summary>
	/// The Home scene (the first scene): the navigation hub. It leads to the community charts (在线谱面),
	/// the local chart manager (自制谱管理) and the option dialog (☰), whose Additionals tab holds the OurTaiko sign-in.
	/// Authored in <c>Prefabs/Community/ScreenLayerOpenSekaiHome</c>; this only binds it.
	/// </summary>
	public sealed class ScreenLayerOpenSekaiHome : ScreenLayer
	{
		[SerializeField]
		private Button communityButton;

		[SerializeField]
		private Button managerButton;

		[SerializeField]
		private Button settingsButton;

		[SerializeField]
		[Tooltip("協力ライブ (co-op live).")]
		private Button coopButton;

		[SerializeField]
		private TextMeshProUGUI statusText;

		[SerializeField]
		private TextMeshProUGUI versionText;

		private bool _leaving;

		protected override void Awake()
		{
			base.Awake();
			communityButton.onClick.AddListener(() => Leave(SceneManager.Scene.CommunityCharts));
			managerButton.onClick.AddListener(() => Leave(SceneManager.Scene.CustomMusicScoreManager));
			if (coopButton != null)
			{
				coopButton.onClick.AddListener(() => Leave(SceneManager.Scene.CoopLive));
			}
			settingsButton.onClick.AddListener(() => ScreenManager.Instance?.ShowDialog<OptionDialog>(DialogType.OptionDialog));
			if (versionText != null)
			{
				versionText.text = "OpenSekai " + Application.version;
			}
			statusText.text = string.Empty;
		}

		protected override void OnScreenStart()
		{
			_leaving = false;
			// Checks the saved sign-in with the SSO (and refreshes the nickname) before the online charts need it.
			CommunityAccount.RefreshAsync(this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow().Forget();
		}

		private void Leave(SceneManager.Scene scene)
		{
			if (_leaving)
			{
				return;
			}
			_leaving = true;
			SceneManager.Instance.RequestScene(scene);
		}
	}
}
