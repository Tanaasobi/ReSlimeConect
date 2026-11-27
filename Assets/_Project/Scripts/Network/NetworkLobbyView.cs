//====================================================
// NetworkLobbyView.cs
// ネットワーク対戦の受付画面（部屋を作る・コードで参加・ランダムマッチ・待機中の表示）を担当する
//====================================================
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RESlimeConect.Network
{
	public sealed class NetworkLobbyView : MonoBehaviour
	{
		[SerializeField] private GameObject m_Root;

		[Header("メニュー")]
		[SerializeField] private GameObject m_MenuRoot;
		[SerializeField] private Text m_ErrorText;
		[SerializeField] private Button m_CreateRoomButton;
		[SerializeField] private Button m_QuickMatchButton;
		[SerializeField] private InputField m_CodeInput;
		[SerializeField] private Button m_JoinButton;
		[SerializeField] private Button m_BackButton;

		[Header("待機中")]
		[SerializeField] private GameObject m_WaitingRoot;
		[SerializeField] private Text m_WaitingText;
		[SerializeField] private Text m_CodeText;
		[SerializeField] private Button m_CopyCodeButton;
		[SerializeField] private Button m_CancelButton;

		public string EnteredCode => m_CodeInput.text.Trim().ToUpperInvariant();

		public event Action CancelClicked;

		//----------------------------------------------------
		// ボタンのイベントを登録する
		//----------------------------------------------------
		private void Awake()
		{
			m_CodeInput.onValueChanged.AddListener(_ => UpdateJoinButton());
			m_CopyCodeButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = m_CodeText.text);
			m_CancelButton.onClick.AddListener(() => CancelClicked?.Invoke());
		}

		//----------------------------------------------------
		// メニューを表示する（error があれば併せて表示する）
		//----------------------------------------------------
		public void ShowMenu(string error)
		{
			m_Root.SetActive(true);
			m_MenuRoot.SetActive(true);
			m_WaitingRoot.SetActive(false);
			m_ErrorText.text = error ?? string.Empty;
			UpdateJoinButton();
		}

		//----------------------------------------------------
		// 待機中の表示にする（code があればルームコードとコピーボタンを出す）
		//----------------------------------------------------
		public void ShowWaiting(string message, string code, bool canCancel)
		{
			m_Root.SetActive(true);
			m_MenuRoot.SetActive(false);
			m_WaitingRoot.SetActive(true);
			m_WaitingText.text = message;

			bool hasCode = !string.IsNullOrEmpty(code);
			m_CodeText.text = hasCode ? code : string.Empty;
			m_CopyCodeButton.gameObject.SetActive(hasCode);
			m_CancelButton.gameObject.SetActive(canCancel);
		}

		//----------------------------------------------------
		// 受付画面を閉じる
		//----------------------------------------------------
		public void Hide()
		{
			m_Root.SetActive(false);
		}

		//----------------------------------------------------
		// メニューのいずれかのボタンが押されるまで待つ
		//----------------------------------------------------
		public async Awaitable<ELobbyAction> WaitForActionAsync(CancellationToken token)
		{
			AwaitableCompletionSource<ELobbyAction> source = new AwaitableCompletionSource<ELobbyAction>();
			UnityAction onCreate = () => source.TrySetResult(ELobbyAction.CreateRoom);
			UnityAction onJoin = () => source.TrySetResult(ELobbyAction.JoinRoom);
			UnityAction onQuick = () => source.TrySetResult(ELobbyAction.QuickMatch);
			UnityAction onBack = () => source.TrySetResult(ELobbyAction.Back);

			m_CreateRoomButton.onClick.AddListener(onCreate);
			m_JoinButton.onClick.AddListener(onJoin);
			m_QuickMatchButton.onClick.AddListener(onQuick);
			m_BackButton.onClick.AddListener(onBack);
			try
			{
				using (token.Register(() => source.TrySetCanceled()))
				{
					return await source.Awaitable;
				}
			}
			finally
			{
				// シーン破棄中はボタンが既に破棄されていることがある
				if (this != null)
				{
					m_CreateRoomButton.onClick.RemoveListener(onCreate);
					m_JoinButton.onClick.RemoveListener(onJoin);
					m_QuickMatchButton.onClick.RemoveListener(onQuick);
					m_BackButton.onClick.RemoveListener(onBack);
				}
			}
		}

		//----------------------------------------------------
		// コードが入力されているときだけ参加ボタンを押せるようにする
		//----------------------------------------------------
		private void UpdateJoinButton()
		{
			m_JoinButton.interactable = EnteredCode.Length > 0;
		}
	}
}
