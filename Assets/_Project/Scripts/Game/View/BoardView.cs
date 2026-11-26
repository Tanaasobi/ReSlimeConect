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

		[Header("マーク")]
		[SerializeField] private Sprite m_CircleSprite;
		[SerializeField] private Sprite m_CrossSprite;
		[SerializeField] private Color m_CircleColor = new Color(0.25f, 0.6f, 1f);
		[SerializeField] private Color m_CrossColor = new Color(1f, 0.35f, 0.35f);

		[Header("トラップ")]
		[SerializeField] private Color m_Player1TrapColor = new Color(0.25f, 0.6f, 1f, 0.8f);
		[SerializeField] private Color m_Player2TrapColor = new Color(1f, 0.35f, 0.35f, 0.8f);
		[SerializeField] private Color m_BothTrapColor = new Color(0.8f, 0.4f, 1f, 0.9f);

		[Header("ハイライト")]
		[SerializeField] private Color m_HoverColor = new Color(1f, 1f, 1f, 0.15f);
		[SerializeField] private Color m_WinColor = new Color(1f, 0.85f, 0.2f, 0.45f);

		[Header("演出")]
		[SerializeField] private float m_PopDuration = 0.2f;
		[SerializeField] private float m_TrapRevealDelay = 0.4f;
		[SerializeField] private float m_FlipDuration = 0.4f;
		[SerializeField] private float m_AfterFlipDelay = 0.4f;

		private const int NoCell = -1;

		private bool m_IsInputEnabled;
		private Func<int, bool> m_CanSelect;
		private int m_HoverIndex = NoCell;
		private int m_TrapPreviewIndex = NoCell;

		public event Action<int> CellClicked;

		//----------------------------------------------------
		// カメラ未設定ならメインカメラを使う
		//----------------------------------------------------
		private void Awake()
		{
			if (m_Camera == null)
			{
				m_Camera = Camera.main;
			}
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
		// トラップ設置フェーズで選択中のマスにトラップを仮表示する
		//----------------------------------------------------
		public void ShowTrapPreview(int index, EPlayerId owner)
		{
			HideTrapPreview();
			m_TrapPreviewIndex = index;
			m_Cells[index].ShowTrap(GetTrapColor(owner == EPlayerId.Player1, owner == EPlayerId.Player2));
		}

		//----------------------------------------------------
		// トラップの仮表示を消す
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
		// マークを置く演出を再生し、トラップがあれば公開して反転させてから消す
		//----------------------------------------------------
		public async Awaitable PlayPlaceAsync(PlaceResult result, CancellationToken token)
		{
			CellView cell = m_Cells[result.Index];
			cell.SetMark(GetSprite(result.PlacedMark), GetColor(result.PlacedMark));
			await cell.PlayPopAsync(m_PopDuration, token);

			if (!result.IsTrapTriggered)
			{
				return;
			}

			await Awaitable.WaitForSecondsAsync(m_TrapRevealDelay, token);
			cell.ShowTrap(GetTrapColor(result.TriggeredPlayer1Trap, result.TriggeredPlayer2Trap));

			// 踏んだトラップの数だけ反転させる（2つなら元に戻る）
			EMark mark = result.PlacedMark;
			for (int i = 0; i < result.TrapCount; i++)
			{
				mark = GameRuleUtility.FlipMark(mark);
				await cell.PlayFlipAsync(GetSprite(mark), GetColor(mark), m_FlipDuration, token);
			}

			await Awaitable.WaitForSecondsAsync(m_AfterFlipDelay, token);
			cell.HideTrap();
		}

		//----------------------------------------------------
		// 揃ったラインのマスをハイライトする
		//----------------------------------------------------
		public void ShowWinLine(int[] line)
		{
			foreach (int index in line)
			{
				m_Cells[index].SetHighlight(true, m_WinColor);
			}
		}

		//----------------------------------------------------
		// 踏まれずに残ったトラップをリザルト用に公開する
		//----------------------------------------------------
		public void RevealRemainingTraps(BoardModel board)
		{
			int trap1 = board.IsTrapActive(EPlayerId.Player1) ? board.GetTrapIndex(EPlayerId.Player1) : BoardModel.NoTrap;
			int trap2 = board.IsTrapActive(EPlayerId.Player2) ? board.GetTrapIndex(EPlayerId.Player2) : BoardModel.NoTrap;

			if (trap1 != BoardModel.NoTrap)
			{
				m_Cells[trap1].ShowTrap(GetTrapColor(true, trap1 == trap2));
			}
			if (trap2 != BoardModel.NoTrap && trap2 != trap1)
			{
				m_Cells[trap2].ShowTrap(GetTrapColor(false, true));
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
		// ホバー中のマスを切り替える
		//----------------------------------------------------
		private void SetHover(int index)
		{
			if (m_HoverIndex == index)
			{
				return;
			}
			if (m_HoverIndex != NoCell)
			{
				m_Cells[m_HoverIndex].SetHighlight(false, Color.clear);
			}
			m_HoverIndex = index;
			if (m_HoverIndex != NoCell)
			{
				m_Cells[m_HoverIndex].SetHighlight(true, m_HoverColor);
			}
		}

		//----------------------------------------------------
		// マークに対応する画像を返す
		//----------------------------------------------------
		private Sprite GetSprite(EMark mark)
		{
			switch (mark)
			{
				case EMark.Circle:
					return m_CircleSprite;
				case EMark.Cross:
					return m_CrossSprite;
				default:
					return null;
			}
		}

		//----------------------------------------------------
		// マークに対応する色を返す
		//----------------------------------------------------
		private Color GetColor(EMark mark)
		{
			return mark == EMark.Circle ? m_CircleColor : m_CrossColor;
		}

		//----------------------------------------------------
		// トラップの持ち主に応じた表示色を返す
		//----------------------------------------------------
		private Color GetTrapColor(bool hasPlayer1, bool hasPlayer2)
		{
			if (hasPlayer1 && hasPlayer2)
			{
				return m_BothTrapColor;
			}
			return hasPlayer1 ? m_Player1TrapColor : m_Player2TrapColor;
		}
	}
}
