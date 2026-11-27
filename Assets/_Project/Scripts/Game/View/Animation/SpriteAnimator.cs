//====================================================
// SpriteAnimator.cs
// SpriteAnimationClip を SpriteRenderer または UI Image に再生するコンポーネント
//====================================================
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace RESlimeConect.Game
{
	public sealed class SpriteAnimator : MonoBehaviour
	{
		[Tooltip("どちらか一方を設定する")]
		[SerializeField] private SpriteRenderer m_SpriteRenderer;
		[SerializeField] private Image m_Image;

		private SpriteAnimationClip m_Clip;
		private float m_Time;
		private bool m_IsPlaying;
		private AwaitableCompletionSource m_Completion;

		public SpriteAnimationClip CurrentClip => m_Clip;

		//----------------------------------------------------
		// 再生中ならコマを進める
		//----------------------------------------------------
		private void Update()
		{
			if (!m_IsPlaying)
			{
				return;
			}

			m_Time += Time.deltaTime;
			int frame = Mathf.FloorToInt(m_Time * m_Clip.FrameRate);
			if (m_Clip.Loop)
			{
				frame %= m_Clip.FrameCount;
			}
			else if (frame >= m_Clip.FrameCount)
			{
				frame = m_Clip.FrameCount - 1;
				m_IsPlaying = false;
			}

			SetSprite(m_Clip.GetFrame(frame));
			if (!m_IsPlaying)
			{
				CompletePending();
			}
		}

		//----------------------------------------------------
		// 破棄時に待機中の処理を終わらせる
		//----------------------------------------------------
		private void OnDestroy()
		{
			CompletePending();
		}

		//----------------------------------------------------
		// アニメーションを最初から再生する（完了は待たない）
		//----------------------------------------------------
		public void Play(SpriteAnimationClip clip)
		{
			CompletePending();
			if (clip == null || !clip.IsValid)
			{
				Hide();
				return;
			}

			m_Clip = clip;
			m_Time = 0f;
			m_IsPlaying = true;
			SetSprite(clip.FirstFrame);
			SetVisible(true);
		}

		//----------------------------------------------------
		// アニメーションを再生し、最後のコマまで待つ（ループするものは開始直後に戻る）
		//----------------------------------------------------
		public async Awaitable PlayAsync(SpriteAnimationClip clip, CancellationToken token)
		{
			Play(clip);
			if (!m_IsPlaying || m_Clip.Loop)
			{
				return;
			}

			AwaitableCompletionSource completion = new AwaitableCompletionSource();
			m_Completion = completion;
			using (token.Register(() => completion.TrySetCanceled()))
			{
				await completion.Awaitable;
			}
		}

		//----------------------------------------------------
		// 再生を止めて非表示にする
		//----------------------------------------------------
		public void Hide()
		{
			CompletePending();
			m_IsPlaying = false;
			m_Clip = null;
			SetVisible(false);
		}

		//----------------------------------------------------
		// 表示先に画像を設定する
		//----------------------------------------------------
		private void SetSprite(Sprite sprite)
		{
			if (m_SpriteRenderer != null)
			{
				m_SpriteRenderer.sprite = sprite;
			}
			if (m_Image != null)
			{
				m_Image.sprite = sprite;
			}
		}

		//----------------------------------------------------
		// 表示先の表示・非表示を切り替える
		//----------------------------------------------------
		private void SetVisible(bool visible)
		{
			if (m_SpriteRenderer != null)
			{
				m_SpriteRenderer.enabled = visible;
			}
			if (m_Image != null)
			{
				m_Image.enabled = visible;
			}
		}

		//----------------------------------------------------
		// 完了待ちの処理があれば完了させる（別の再生で中断された場合も含む）
		//----------------------------------------------------
		private void CompletePending()
		{
			AwaitableCompletionSource completion = m_Completion;
			m_Completion = null;
			completion?.TrySetResult();
		}
	}
}
