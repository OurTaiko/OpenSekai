using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.Community
{
	/// <summary>
	/// The Home scene (the first scene): the navigation hub. It leads to the community charts (在线谱面),
	/// the local chart manager (自制谱管理), the OurTaiko account page and the option dialog.
	/// Authored in <c>Prefabs/Community/ScreenLayerOpenSekaiHome</c>; this only binds it.
	/// </summary>
	public sealed class ScreenLayerOpenSekaiHome : ScreenLayer
	{
		[SerializeField]
		private Button communityButton;

		[SerializeField]
		private Button managerButton;

		[SerializeField]
		private Button accountButton;

		[SerializeField]
		private TextMeshProUGUI accountTitle;

		[SerializeField]
		private TextMeshProUGUI accountDetail;

		[SerializeField]
		private Button settingsButton;

		[SerializeField]
		private TextMeshProUGUI statusText;

		[SerializeField]
		private TextMeshProUGUI versionText;

		[SerializeField]
		private CommunityLoginPanel loginPanel;

		private bool _leaving;

		protected override void Awake()
		{
			base.Awake();
			communityButton.onClick.AddListener(() => Leave(SceneManager.Scene.CommunityCharts));
			managerButton.onClick.AddListener(() => Leave(SceneManager.Scene.CustomMusicScoreManager));
			accountButton.onClick.AddListener(() => loginPanel.Show());
			settingsButton.onClick.AddListener(() => ScreenManager.Instance?.ShowDialog<OptionDialog>(DialogType.OptionDialog));
			loginPanel.gameObject.SetActive(false);
			CommunityAccount.Changed += UpdateAccount;
			if (versionText != null)
			{
				versionText.text = "OpenSekai " + Application.version;
			}
			statusText.text = string.Empty;
		}

		private void OnDestroy()
		{
			CommunityAccount.Changed -= UpdateAccount;
		}

		protected override void OnScreenStart()
		{
			_leaving = false;
			UpdateAccount();
			CommunityAccount.RefreshAsync(this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow().Forget();
		}

		private void UpdateAccount()
		{
			if (CommunityAccount.IsSignedIn)
			{
				accountTitle.text = CommunityAccount.Nickname;
				accountDetail.text = "已登录 OurTaiko 账号：" + CommunityAccount.Nickname;
			}
			else
			{
				accountTitle.text = "登录";
				accountDetail.text = "登录 OurTaiko 账号后，可以在在线谱面里点喜欢、使用マイリスト。";
			}
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
