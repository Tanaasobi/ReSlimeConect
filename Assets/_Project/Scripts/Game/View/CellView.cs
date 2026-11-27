//====================================================
// CellView.cs
// 1マス分の見た目（スライム・トラップ・ビックリマーク・ホバー時のプレビュー）と演出を担当する
//====================================================
using System.Threading;
using UnityEngine;

namespace RESlimeConect.Game
{
	[RequireComponent(typeof(Collider2D))]
	public sealed class CellView : MonoBehaviour
	{
		[SerializeField] private int m_Index;

		[Header("表示レイヤー")]
		[SerializeField] private SpriteAnimator m_TrapAnimator;
		[SerializeField] private SpriteAnimator m_SlimeAnimator;
		[SerializeField] private SpriteAnimator m_ExclamationAnimator;
		[Tooltip("ホバー時の半透明プレビュー。位置と大きさは対象レイヤーに合わせる")]
		[SerializeField] private SpriteRenderer m_PreviewRenderer;

		public int Index => m_Index;

		//----------------------------------------------------
		// 表示をすべて消して初期状態に戻す
		//----------------------------------------------------
		public void ResetView()
		{
			m_TrapAnimator.Hide();
			m_SlimeAnimator.Hide();
			m_ExclamationAnimator.Hide();
			HidePreview();
		}

		//----------------------------------------------------
		// スライムのループアニメーション（通常・勝利）を再生する
		//----------------------------------------------------
		public void PlaySlime(SpriteAnimationClip clip)
		{
			m_SlimeAnimator.Play(clip);
		}

		//----------------------------------------------------
		// スライムの1回きりのアニメーションを再生して終わるまで待つ（コマが無ければ今の表示のまま何もしない）
		//----------------------------------------------------
		public async Awaitable PlaySlimeAsync(SpriteAnimationClip clip, CancellationToken token)
		{
			if (clip == null || !clip.IsValid)
			{
				return;
			}
			await m_SlimeAnimator.PlayAsync(clip, token);
		}

		//----------------------------------------------------
		// ビックリマークを再生し、終わったら消す
		//----------------------------------------------------
		public async Awaitable PlayExclamationAsync(SpriteAnimationClip clip, CancellationToken token)
		{
			await m_ExclamationAnimator.PlayAsync(clip, token);
			m_ExclamationAnimator.Hide();
		}

		//----------------------------------------------------
		// トラップのループアニメーションを表示する
		//----------------------------------------------------
		public void ShowTrap(SpriteAnimationClip clip)
		{
			m_TrapAnimator.Play(clip);
		}

		//----------------------------------------------------
		// トラップの1回きりのアニメーションを再生し、終わったら消す
		//----------------------------------------------------
		public async Awaitable PlayTrapOnceAsync(SpriteAnimationClip clip, CancellationToken token)
		{
			await m_TrapAnimator.PlayAsync(clip, token);
			m_TrapAnimator.Hide();
		}

		//----------------------------------------------------
		// トラップ表示を消す
		//----------------------------------------------------
		public void HideTrap()
		{
			m_TrapAnimator.Hide();
		}

		//----------------------------------------------------
		// ホバー時の半透明プレビューを、スライムかトラップの位置と大きさで表示する
		//----------------------------------------------------
		public void ShowPreview(Sprite sprite, Color color, bool isTrapLayer)
		{
			Transform target = isTrapLayer ? m_TrapAnimator.transform : m_SlimeAnimator.transform;
			Transform preview = m_PreviewRenderer.transform;
			preview.localPosition = target.localPosition;
			preview.localScale = target.localScale;

			m_PreviewRenderer.sprite = sprite;
			m_PreviewRenderer.color = color;
			m_PreviewRenderer.enabled = sprite != null;
		}

		//----------------------------------------------------
		// ホバー時のプレビューを消す
		//----------------------------------------------------
		public void HidePreview()
		{
			m_PreviewRenderer.enabled = false;
		}
	}
}
