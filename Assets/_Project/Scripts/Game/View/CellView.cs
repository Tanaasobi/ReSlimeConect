//====================================================
// CellView.cs
// 1マス分の見た目（マーク・トラップ・ハイライト）と演出を担当する
//====================================================
using System.Threading;
using UnityEngine;

namespace RESlimeConect.Game
{
	[RequireComponent(typeof(Collider2D))]
	public sealed class CellView : MonoBehaviour
	{
		[SerializeField] private int m_Index;
		[SerializeField] private SpriteRenderer m_MarkRenderer;
		[SerializeField] private SpriteRenderer m_TrapRenderer;
		[SerializeField] private SpriteRenderer m_HighlightRenderer;

		private Vector3 m_MarkBaseScale = Vector3.one;

		public int Index => m_Index;

		//----------------------------------------------------
		// マーク表示の基準スケールを記録する
		//----------------------------------------------------
		private void Awake()
		{
			if (m_MarkRenderer != null)
			{
				m_MarkBaseScale = m_MarkRenderer.transform.localScale;
			}
		}

		//----------------------------------------------------
		// 表示をすべて消して初期状態に戻す
		//----------------------------------------------------
		public void ResetView()
		{
			SetMark(null, Color.white);
			HideTrap();
			SetHighlight(false, Color.clear);
		}

		//----------------------------------------------------
		// マークの画像と色を設定する（null で非表示）
		//----------------------------------------------------
		public void SetMark(Sprite sprite, Color color)
		{
			m_MarkRenderer.sprite = sprite;
			m_MarkRenderer.color = color;
			m_MarkRenderer.enabled = sprite != null;
			m_MarkRenderer.transform.localScale = m_MarkBaseScale;
		}

		//----------------------------------------------------
		// トラップアイコンを指定色で表示する
		//----------------------------------------------------
		public void ShowTrap(Color color)
		{
			m_TrapRenderer.color = color;
			m_TrapRenderer.enabled = true;
		}

		//----------------------------------------------------
		// トラップアイコンを非表示にする
		//----------------------------------------------------
		public void HideTrap()
		{
			m_TrapRenderer.enabled = false;
		}

		//----------------------------------------------------
		// ハイライトの表示状態と色を設定する
		//----------------------------------------------------
		public void SetHighlight(bool visible, Color color)
		{
			m_HighlightRenderer.color = color;
			m_HighlightRenderer.enabled = visible;
		}

		//----------------------------------------------------
		// マークを横につぶして別のマークに差し替える反転演出を再生する
		//----------------------------------------------------
		public async Awaitable PlayFlipAsync(Sprite nextSprite, Color nextColor, float duration, CancellationToken token)
		{
			float half = duration * 0.5f;
			await ScaleMarkXAsync(1f, 0f, half, token);
			m_MarkRenderer.sprite = nextSprite;
			m_MarkRenderer.color = nextColor;
			await ScaleMarkXAsync(0f, 1f, half, token);
		}

		//----------------------------------------------------
		// マークを置いたときに少し弾む演出を再生する
		//----------------------------------------------------
		public async Awaitable PlayPopAsync(float duration, CancellationToken token)
		{
			Transform target = m_MarkRenderer.transform;
			float time = 0f;
			while (time < duration)
			{
				time += Time.deltaTime;
				float rate = Mathf.Clamp01(time / duration);
				float scale = 1f + Mathf.Sin(rate * Mathf.PI) * 0.2f;
				target.localScale = m_MarkBaseScale * scale;
				await Awaitable.NextFrameAsync(token);
			}
			target.localScale = m_MarkBaseScale;
		}

		//----------------------------------------------------
		// マークの横幅倍率を時間をかけて変化させる
		//----------------------------------------------------
		private async Awaitable ScaleMarkXAsync(float from, float to, float duration, CancellationToken token)
		{
			Transform target = m_MarkRenderer.transform;
			float time = 0f;
			while (time < duration)
			{
				time += Time.deltaTime;
				float rate = Mathf.Clamp01(time / duration);
				float x = Mathf.Lerp(from, to, rate);
				target.localScale = new Vector3(m_MarkBaseScale.x * x, m_MarkBaseScale.y, m_MarkBaseScale.z);
				await Awaitable.NextFrameAsync(token);
			}
			target.localScale = new Vector3(m_MarkBaseScale.x * to, m_MarkBaseScale.y, m_MarkBaseScale.z);
		}
	}
}
