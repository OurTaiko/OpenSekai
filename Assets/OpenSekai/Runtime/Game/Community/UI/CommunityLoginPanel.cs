using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.Community
{
	/// <summary>
	/// The OurTaiko account page (prefab <c>Prefabs/Community/CommunityLoginPanel</c>), nested in the Home and
	/// community chart screens. Signed out it asks for a username and password, which go only to the
	/// account center; signed in it shows the account and 退出登录.
	/// </summary>
	public sealed class CommunityLoginPanel : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI titleText;

		[SerializeField]
		private GameObject signInGroup;

		[SerializeField]
		private TMP_InputField usernameInput;

		[SerializeField]
		private TMP_InputField passwordInput;

		[SerializeField]
		private Button loginButton;

		[SerializeField]
		private TextMeshProUGUI loginLabel;

		[SerializeField]
		private Button registerButton;

		[SerializeField]
		private Button forgotButton;

		[SerializeField]
		private GameObject signedInGroup;

		[SerializeField]
		private TextMeshProUGUI accountText;

		[SerializeField]
		private Button accountCenterButton;

		[SerializeField]
		private Button logoutButton;

		[SerializeField]
		private Button closeButton;

		[SerializeField]
		private TextMeshProUGUI statusText;

		private Action _onSignedIn;

		private CancellationTokenSource _busy;

		public bool IsOpen => gameObject.activeSelf;

		private void Awake()
		{
			loginButton.onClick.AddListener(() => LoginAsync().Forget());
			logoutButton.onClick.AddListener(() => LogoutAsync().Forget());
			closeButton.onClick.AddListener(Hide);
			registerButton.onClick.AddListener(() => OpenAccountPageAsync(CommunityAccount.RegisterUrl).Forget());
			forgotButton.onClick.AddListener(() => OpenAccountPageAsync(meta => (meta?.Sso?.Issuer ?? "https://sso.ourtaiko.org").TrimEnd('/') + "/password/reset/").Forget());
			accountCenterButton.onClick.AddListener(() => OpenAccountPageAsync(CommunityAccount.AccountCenterUrl).Forget());
			passwordInput.contentType = TMP_InputField.ContentType.Password;
			passwordInput.onSubmit.AddListener(_ => LoginAsync().Forget());
			usernameInput.onSubmit.AddListener(_ => passwordInput.ActivateInputField());
		}

		private void OnDestroy()
		{
			_busy?.Cancel();
			_busy?.Dispose();
		}

		/// <summary>Opens the page. <paramref name="onSignedIn"/> runs after a successful sign-in.</summary>
		public void Show(string reason = null, Action onSignedIn = null)
		{
			_onSignedIn = onSignedIn;
			gameObject.SetActive(true);
			transform.SetAsLastSibling();
			passwordInput.text = string.Empty;
			Refresh(reason);
			if (!CommunityAccount.IsSignedIn)
			{
				usernameInput.ActivateInputField();
			}
		}

		public void Hide()
		{
			_busy?.Cancel();
			_onSignedIn = null;
			gameObject.SetActive(false);
		}

		private void Refresh(string message = null)
		{
			bool signedIn = CommunityAccount.IsSignedIn;
			titleText.text = signedIn ? "OurTaiko 账号" : "登录 OurTaiko 账号";
			signInGroup.SetActive(!signedIn);
			signedInGroup.SetActive(signedIn);
			if (signedIn)
			{
				string username = CommunityAccount.Username;
				accountText.text = string.IsNullOrEmpty(username)
					? CommunityAccount.Nickname
					: CommunityAccount.Nickname + "\n<size=70%><color=#9C98C8>@" + username + "</color></size>";
			}
			SetStatus(message ?? (signedIn ? string.Empty : "登录后可以给谱面点喜欢，并把谱面加入マイリスト。"));
			SetBusy(false);
		}

		private async UniTaskVoid LoginAsync()
		{
			if (_busy != null || CommunityAccount.IsSignedIn)
			{
				return;
			}
			string username = usernameInput.text.Trim();
			if (username.Length == 0 || passwordInput.text.Length == 0)
			{
				SetStatus("请输入用户名和密码。", true);
				return;
			}

			SetBusy(true);
			SetStatus("正在登录…");
			try
			{
				await CommunityAccount.LoginAsync(username, passwordInput.text, _busy.Token);
				passwordInput.text = string.Empty;
				Action next = _onSignedIn;
				SetBusy(false);
				Hide();
				next?.Invoke();
			}
			catch (OperationCanceledException)
			{
			}
			catch (CommunityException exception)
			{
				SetStatus(exception.Message, true);
			}
			finally
			{
				SetBusy(false);
			}
		}

		private async UniTaskVoid LogoutAsync()
		{
			SetBusy(true);
			try
			{
				await CommunityAccount.LogoutAsync(_busy.Token);
			}
			catch (OperationCanceledException)
			{
			}
			finally
			{
				SetBusy(false);
			}
			Refresh("已退出登录。");
		}

		private async UniTaskVoid OpenAccountPageAsync(Func<CommunityMeta, string> url)
		{
			try
			{
				CommunityMeta meta = await CommunityClient.GetMetaAsync(this.GetCancellationTokenOnDestroy());
				Application.OpenURL(url(meta));
			}
			catch (CommunityException exception)
			{
				SetStatus(exception.Message, true);
			}
		}

		private void SetBusy(bool busy)
		{
			if (busy)
			{
				_busy = new CancellationTokenSource();
			}
			else if (_busy != null)
			{
				_busy.Dispose();
				_busy = null;
			}
			loginButton.interactable = !busy;
			logoutButton.interactable = !busy;
			usernameInput.interactable = !busy;
			passwordInput.interactable = !busy;
			if (loginLabel != null)
			{
				loginLabel.text = busy ? "登录中…" : "登录";
			}
			foreach (Button button in new[] { loginButton, logoutButton })
			{
				if (button != null && button.TryGetComponent(out CanvasGroup group))
				{
					group.alpha = button.interactable ? 1f : 0.4f;
				}
			}
		}

		private void SetStatus(string message, bool error = false)
		{
			statusText.text = message ?? string.Empty;
			statusText.color = error ? new Color32(255, 140, 160, 255) : new Color32(200, 196, 232, 255);
		}
	}
}
