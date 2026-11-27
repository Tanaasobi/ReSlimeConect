//====================================================
// GameUIView.cs
// 手番プレイヤー枠・残り時間・トラップ操作ボタン・交代画面・リザルトの UI を担当する
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
		[Header("プレイヤー枠")]
		[SerializeField] private GameObject m_PlayerFrameRoot;
		[SerializeField] private SpriteAnimator m_PlayerSlimeAnimator;

		[Header("残り時間")]
		[SerializeField] private GameObject m_TimeFrameRoot;
		[SerializeField] private SpriteNumberText m_TimeText;

		[Header("トラップ設置")]
		[SerializeField] private GameObject m_TrapControlRoot;
		[SerializeField] private Button m_TrapConfirmButton;
		[SerializeField] private Button m_TrapSkipButton;
		[SerializeField] private Color m_DisabledButtonColor = new Color(1f, 1f, 1f, 0.5f);

		[Header("交代画面")]
		[SerializeField] private GameObject m_HandoffPanel;
		[SerializeField] private Text m_HandoffText;
		[SerializeField] private Button m_HandoffButton;

		[Header("状況表示（任意。ネットワーク対戦で手番などを表示する）")]
		[SerializeField] private Text m_StatusText;

		[Header("リザルト")]
		[SerializeField] private GameObject m_ResultRoot;
		[SerializeField] private Text m_ResultText;
		[SerializeField] private Button m_ModeSelectButton;
		[SerializeField] private Button m_RematchButton;
		[SerializeField] private Button m_GameEndButton;

		public event Action TrapConfirmClicked;
		public event Action TrapSkipClicked;
		public event Action ModeSelectClicked;
		public event Action GameEndClicked;

		//----------------------------------------------------
		// ボタンのイベントを登録し、切り替え式の UI を非表示にする
		//----------------------------------------------------
		private void Awake()
		{
			m_TrapConfirmButton.onClick.AddListener(() => TrapConfirmClicked?.Invoke());
			m_TrapSkipButton.onClick.AddListener(() => TrapSkipClicked?.Invoke());
			m_ModeSelectButton.onClick.AddListener(() => ModeSelectClicked?.Invoke());
			m_GameEndButton.onClick.AddListener(() => GameEndClicked?.Invoke());

			ShowTrapControls(false);
			SetTimeVisible(false);
			m_HandoffPanel.SetActive(false);
			m_ResultRoot.SetActive(false);
			SetStatus(string.Empty);
		}

		//----------------------------------------------------
		// 状況表示の文言を設定する（空文字で消す。状況表示が無いシーンでは何もしない）
		//----------------------------------------------------
		public void SetStatus(string message)
		{
			if (m_StatusText != null)
			{
				m_StatusText.text = message;
			}
		}

		//----------------------------------------------------
		// リザルト表示を閉じる（相手の操作で次の対戦が始まったときなど）
		//----------------------------------------------------
		public void HideResult()
		{
			m_ResultRoot.SetActive(false);
		}

		//----------------------------------------------------
		// プレイヤー枠に表示するスライムのアニメーションを設定する（null で空にする）
		//----------------------------------------------------
		public void SetPlayerSlime(SpriteAnimationClip clip)
		{
			m_PlayerFrameRoot.SetActive(true);
			if (clip == null)
			{
				m_PlayerSlimeAnimator.Hide();
				return;
			}
			if (m_PlayerSlimeAnimator.CurrentClip != clip)
			{
				m_PlayerSlimeAnimator.Play(clip);
			}
		}

		//----------------------------------------------------
		// 残り時間枠の表示を切り替える
		//----------------------------------------------------
		public void SetTimeVisible(bool visible)
		{
			m_TimeFrameRoot.SetActive(visible);
		}

		//----------------------------------------------------
		// 残り時間（秒）を表示する
		//----------------------------------------------------
		public void SetTime(int seconds)
		{
			m_TimeText.SetText($"{seconds}s");
		}

		//----------------------------------------------------
		// トラップ設置用ボタンの表示を切り替える
		//----------------------------------------------------
		public void ShowTrapControls(bool visible)
		{
			m_TrapControlRoot.SetActive(visible);
		}

		//----------------------------------------------------
		// トラップ決定ボタンを押せるかどうかを設定する（押せない間は半透明）
		//----------------------------------------------------
		public void SetTrapConfirmInteractable(bool interactable)
		{
			m_TrapConfirmButton.interactable = interactable;
			m_TrapConfirmButton.image.color = interactable ? Color.white : m_DisabledButtonColor;
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
		// リザルトを表示し、再戦ボタンが押されるまで待つ
		//----------------------------------------------------
		public async Awaitable ShowResultAsync(string message, CancellationToken token)
		{
			m_ResultText.text = message;
			m_ResultRoot.SetActive(true);

			// キャンセル時はシーン破棄中で UI が消えている場合があるため、後片付けは正常終了時のみ行う
			await WaitForClickAsync(m_RematchButton, token);
			m_ResultRoot.SetActive(false);
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
