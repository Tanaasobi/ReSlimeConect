//====================================================
// BoardView.cs
// 盤面全体の表示とクリック入力（Input System + Physics2D）を担当する
//====================================================
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RESlimeConect.Game
{
	public sealed class BoardView : MonoBehaviour
	{
		[Header("参照")]
		[SerializeField] private Camera m_Camera;
		[SerializeField] private CellView[] m_Cells = new CellView[BoardModel.CellCount];

		[Header("見た目")]
		[SerializeField] private BoardSkin m_Skin;
		[SerializeField] private SpriteRenderer m_BoardRenderer;
		[SerializeField] private SpriteRenderer m_BackgroundRenderer;

		[Header("演出")]
		[Tooltip("駒を置いてからトラップが発動するまでの間")]
		[SerializeField] private float m_TrapRevealDelay = 0.4f;
		[Tooltip("変化し終わってから次の手番に移るまでの間")]
		[SerializeField] private float m_AfterChangeDelay = 0.3f;

		private const int NoCell = -1;

		private bool m_IsInputEnabled;
		private Func<int, bool> m_CanSelect;
		private int m_HoverIndex = NoCell;
		private int m_TrapPreviewIndex = NoCell;
		private Sprite m_HoverPreviewSprite;
		private bool m_IsHoverPreviewTrap;

		public BoardSkin Skin => m_Skin;

		public event Action<int> CellClicked;

		//----------------------------------------------------
		// カメラ未設定ならメインカメラを使い、スキンを反映する
		//----------------------------------------------------
		private void Awake()
		{
			if (m_Camera == null)
			{
				m_Camera = Camera.main;
			}
			ApplySkin();
		}

		//----------------------------------------------------
		// 入力が有効な間、ホバー表示とクリック判定を行う
		//----------------------------------------------------
		private void Update()
		{
			if (!m_IsInputEnabled)
			{
				return;
			}

			Pointer pointer = Pointer.current;
			if (pointer == null)
			{
				return;
			}

			int index = FindSelectableCell(pointer.position.ReadValue());
			SetHover(index);

			if (index != NoCell && pointer.press.wasPressedThisFrame)
			{
				CellClicked?.Invoke(index);
			}
		}

		//----------------------------------------------------
		// スキンを差し替えて見た目に反映する
		//----------------------------------------------------
		public void SetSkin(BoardSkin skin)
		{
			m_Skin = skin;
			ApplySkin();
		}

		//----------------------------------------------------
		// 現在のスキンを盤面と背景に反映する
		//----------------------------------------------------
		public void ApplySkin()
		{
			if (m_Skin == null)
			{
				Debug.LogWarning("[BoardView] スキンが設定されていません", this);
				return;
			}

			if (m_BoardRenderer != null)
			{
				m_BoardRenderer.sprite = m_Skin.BoardSprite;
			}
			if (m_BackgroundRenderer != null)
			{
				m_BackgroundRenderer.sprite = m_Skin.BackgroundSprite;
			}
		}

		//----------------------------------------------------
		// エディタ上でスキンの変更をシーンに反映して確認する（Inspector の右クリックメニュー）
		//----------------------------------------------------
		[ContextMenu("スキンを適用")]
		private void ApplySkinFromContextMenu()
		{
#if UNITY_EDITOR
			UnityEditor.Undo.RecordObjects(new UnityEngine.Object[] { m_BoardRenderer, m_BackgroundRenderer }, "スキンを適用");
#endif
			ApplySkin();
		}

		//----------------------------------------------------
		// クリック入力の有効・無効と選択可能なマスの条件を設定する
		//----------------------------------------------------
		public void SetInputEnabled(bool enabled, Func<int, bool> canSelect = null)
		{
			m_IsInputEnabled = enabled;
			m_CanSelect = canSelect;
			if (!enabled)
			{
				SetHover(NoCell);
			}
		}

		//----------------------------------------------------
		// ホバー時のプレビューをトラップにする（トラップフェーズ用）
		//----------------------------------------------------
		public void SetHoverPreviewTrap()
		{
			m_HoverPreviewSprite = m_Skin.TrapIdle.FirstFrame;
			m_IsHoverPreviewTrap = true;
		}

		//----------------------------------------------------
		// ホバー時のプレビューを指定マークのスライムにする（対戦フェーズ用）
		//----------------------------------------------------
		public void SetHoverPreviewSlime(EMark mark)
		{
			m_HoverPreviewSprite = m_Skin.GetSlime(mark).Idle.FirstFrame;
			m_IsHoverPreviewTrap = false;
		}

		//----------------------------------------------------
		// 盤面の見た目をすべて初期化する
		//----------------------------------------------------
		public void ResetView()
		{
			SetInputEnabled(false);
			m_TrapPreviewIndex = NoCell;
			foreach (CellView cell in m_Cells)
			{
				cell.ResetView();
			}
		}

		//----------------------------------------------------
		// トラップフェーズで選択中のマスにトラップを表示する
		//----------------------------------------------------
		public void ShowTrapPreview(int index)
		{
			HideTrapPreview();
			SetHover(NoCell);
			m_TrapPreviewIndex = index;
			m_Cells[index].ShowTrap(m_Skin.TrapIdle);
		}

		//----------------------------------------------------
		// 選択中のトラップ表示を消す
		//----------------------------------------------------
		public void HideTrapPreview()
		{
			if (m_TrapPreviewIndex != NoCell)
			{
				m_Cells[m_TrapPreviewIndex].HideTrap();
				m_TrapPreviewIndex = NoCell;
			}
		}

		//----------------------------------------------------
		// トラップ設置を決定したときの演出を再生する
		//----------------------------------------------------
		public Awaitable PlayTrapPlaceAsync(int index, CancellationToken token)
		{
			return m_Cells[index].PlayTrapOnceAsync(m_Skin.TrapPlace, token);
		}

		//----------------------------------------------------
		// スライムを置く演出を再生し、トラップを踏んでいればビックリマークの後に変化させる
		//----------------------------------------------------
		public async Awaitable PlayPlaceAsync(PlaceResult result, CancellationToken token)
		{
			CellView cell = m_Cells[result.Index];
			SlimeAnimationSet placed = m_Skin.GetSlime(result.PlacedMark);
			await cell.PlaySlimeAsync(placed.Place, token);
			cell.PlaySlime(placed.Idle);

			if (!result.IsTrapTriggered)
			{
				return;
			}

			// 踏んだトラップの数だけ「ビックリマークと驚き、変化」を繰り返す（2つなら元に戻る）
			EMark mark = result.PlacedMark;
			for (int i = 0; i < result.TrapCount; i++)
			{
				await Awaitable.WaitForSecondsAsync(m_TrapRevealDelay, token);

				SlimeAnimationSet current = m_Skin.GetSlime(mark);
				Awaitable surprise = cell.PlaySlimeAsync(current.Surprise, token);
				await cell.PlayExclamationAsync(m_Skin.Exclamation, token);
				await surprise;

				await cell.PlaySlimeAsync(current.Change, token);
				mark = GameRuleUtility.FlipMark(mark);
				cell.PlaySlime(m_Skin.GetSlime(mark).Idle);
			}

			await Awaitable.WaitForSecondsAsync(m_AfterChangeDelay, token);
		}

		//----------------------------------------------------
		// 揃ったラインのスライムに勝利アニメーションを再生させる
		//----------------------------------------------------
		public void PlayWinLine(int[] line, EMark winner)
		{
			SpriteAnimationClip clip = m_Skin.GetSlime(winner).Win;
			foreach (int index in line)
			{
				m_Cells[index].PlaySlime(clip);
			}
		}

		//----------------------------------------------------
		// 踏まれずに残ったトラップをリザルト用に公開する
		//----------------------------------------------------
		public void RevealRemainingTraps(BoardModel board)
		{
			foreach (EPlayerId owner in new[] { EPlayerId.Player1, EPlayerId.Player2 })
			{
				if (board.IsTrapActive(owner))
				{
					m_Cells[board.GetTrapIndex(owner)].ShowTrap(m_Skin.TrapIdle);
				}
			}
		}

		//----------------------------------------------------
		// 画面座標の下にある選択可能なマス番号を返す（なければ NoCell）
		//----------------------------------------------------
		private int FindSelectableCell(Vector2 screenPosition)
		{
			if (m_Camera == null)
			{
				return NoCell;
			}
			if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
			{
				return NoCell;
			}

			Vector2 worldPosition = m_Camera.ScreenToWorldPoint(screenPosition);
			Collider2D hit = Physics2D.OverlapPoint(worldPosition);
			if (hit == null || !hit.TryGetComponent(out CellView cell))
			{
				return NoCell;
			}
			if (m_CanSelect != null && !m_CanSelect(cell.Index))
			{
				return NoCell;
			}
			return cell.Index;
		}

		//----------------------------------------------------
		// ホバー中のマスを切り替え、半透明プレビューを出し直す
		//----------------------------------------------------
		private void SetHover(int index)
		{
			if (m_HoverIndex == index)
			{
				return;
			}
			if (m_HoverIndex != NoCell)
			{
				m_Cells[m_HoverIndex].HidePreview();
			}
			m_HoverIndex = index;

			// 既にトラップを表示しているマスには重ねない
			if (m_HoverIndex != NoCell && m_HoverIndex != m_TrapPreviewIndex)
			{
				m_Cells[m_HoverIndex].ShowPreview(m_HoverPreviewSprite, m_Skin.HoverPreviewColor, m_IsHoverPreviewTrap);
			}
		}
	}
}
