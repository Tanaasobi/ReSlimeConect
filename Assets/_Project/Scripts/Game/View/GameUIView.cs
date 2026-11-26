//====================================================
// GameUIView.cs
// フェーズ表示・トラップ操作ボタン・交代画面・リザルト画面の UI を担当する
//====================================================
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RESlimeConect.Game
{
	public sealed class GameUIView : MonoBehaviour
	{
		[Header("常時表示")]
		[SerializeField] private Text m_PhaseText;
		[SerializeField] private Text m_MessageText;

		[Header("トラップ設置")]
		[SerializeField] private GameObject m_TrapControlRoot;
		[SerializeField] private Button m_TrapConfirmButton;
		[SerializeField] private Button m_TrapSkipButton;

		[Header("交代画面")]
		[SerializeField] private GameObject m_HandoffPanel;
		[SerializeField] private Text m_HandoffText;
		[SerializeField] private Button m_HandoffButton;

		[Header("リザルト")]
		[SerializeField] private GameObject m_ResultPanel;
		[SerializeField] private Text m_ResultText;
		[SerializeField] private Button m_RetryButton;

		public event Action TrapConfirmClicked;
		public event Action TrapSkipClicked;

		//----------------------------------------------------
		// ボタンのイベントを登録し、パネルを非表示にする
		//----------------------------------------------------
		private void Awake()
		{
			m_TrapConfirmButton.onClick.AddListener(() => TrapConfirmClicked?.Invoke());
			m_TrapSkipButton.onClick.AddListener(() => TrapSkipClicked?.Invoke());
			ShowTrapControls(false);
			m_HandoffPanel.SetActive(false);
			m_ResultPanel.SetActive(false);
		}

		//----------------------------------------------------
		// 画面上部のフェーズ名を設定する
		//----------------------------------------------------
		public void SetPhase(string text)
		{
			m_PhaseText.text = text;
		}

		//----------------------------------------------------
		// フェーズ名の下に出す案内メッセージを設定する
		//----------------------------------------------------
		public void SetMessage(string text)
		{
			m_MessageText.text = text;
		}

		//----------------------------------------------------
		// トラップ設置用ボタンの表示を切り替える
		//----------------------------------------------------
		public void ShowTrapControls(bool visible)
		{
			m_TrapControlRoot.SetActive(visible);
		}

		//----------------------------------------------------
		// トラップ決定ボタンを押せるかどうかを設定する
		//----------------------------------------------------
		public void SetTrapConfirmInteractable(bool interactable)
		{
			m_TrapConfirmButton.interactable = interactable;
		}

		//----------------------------------------------------
		// 盤面を隠す交代画面を表示し、ボタンが押されるまで待つ
		//----------------------------------------------------
		public async Awaitable ShowHandoffAsync(string message, CancellationToken token)
		{
			m_HandoffText.text = message;
			m_HandoffPanel.SetActive(true);

			// キャンセル時はシーン破棄中で UI が消えている場合があるため、後片付けは正常終了時のみ行う
			await WaitForClickAsync(m_HandoffButton, token);
			m_HandoffPanel.SetActive(false);
		}

		//----------------------------------------------------
		// リザルトを表示し、もう一度ボタンが押されるまで待つ
		//----------------------------------------------------
		public async Awaitable ShowResultAsync(string message, CancellationToken token)
		{
			m_ResultText.text = message;
			m_ResultPanel.SetActive(true);

			// キャンセル時はシーン破棄中で UI が消えている場合があるため、後片付けは正常終了時のみ行う
			await WaitForClickAsync(m_RetryButton, token);
			m_ResultPanel.SetActive(false);
		}

		//----------------------------------------------------
		// 指定ボタンが一度押されるまで待つ
		//----------------------------------------------------
		private static async Awaitable WaitForClickAsync(Button button, CancellationToken token)
		{
			AwaitableCompletionSource source = new AwaitableCompletionSource();
			UnityAction onClick = () => source.TrySetResult();
			button.onClick.AddListener(onClick);
			try
			{
				using (token.Register(() => source.TrySetCanceled()))
				{
					await source.Awaitable;
				}
			}
			finally
			{
				// シーン破棄中はボタンが既に破棄されていることがある
				if (button != null)
				{
					button.onClick.RemoveListener(onClick);
				}
			}
		}
	}
}
