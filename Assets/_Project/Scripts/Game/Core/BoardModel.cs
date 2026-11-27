//====================================================
// BoardModel.cs
// 3x3 の盤面とトラップの状態を管理する純粋なロジッククラス
// Unity の描画や入力には依存しないので、CPU やオンライン対戦でも共通で使う
//====================================================
using System;

namespace RESlimeConect.Game
{
	public sealed class BoardModel
	{
		public const int Size = 3;
		public const int CellCount = Size * Size;
		public const int NoTrap = -1;

		private static readonly int[][] WinLines =
		{
			new[] { 0, 1, 2 },
			new[] { 3, 4, 5 },
			new[] { 6, 7, 8 },
			new[] { 0, 3, 6 },
			new[] { 1, 4, 7 },
			new[] { 2, 5, 8 },
			new[] { 0, 4, 8 },
			new[] { 2, 4, 6 },
		};

		private readonly EMark[] m_Cells = new EMark[CellCount];
		private readonly int[] m_TrapIndices = new int[2];
		private readonly bool[] m_TrapTriggered = new bool[2];

		//----------------------------------------------------
		// 空の盤面で生成する
		//----------------------------------------------------
		public BoardModel()
		{
			Reset();
		}

		//----------------------------------------------------
		// 盤面とトラップをすべて初期状態に戻す
		//----------------------------------------------------
		public void Reset()
		{
			Array.Clear(m_Cells, 0, m_Cells.Length);
			for (int i = 0; i < m_TrapIndices.Length; i++)
			{
				m_TrapIndices[i] = NoTrap;
				m_TrapTriggered[i] = false;
			}
		}

		//----------------------------------------------------
		// 指定マスのマークを返す
		//----------------------------------------------------
		public EMark GetMark(int index)
		{
			return m_Cells[index];
		}

		//----------------------------------------------------
		// 指定マスが空いているかを返す
		//----------------------------------------------------
		public bool IsEmpty(int index)
		{
			return IsValidIndex(index) && m_Cells[index] == EMark.None;
		}

		//----------------------------------------------------
		// マス番号が盤面の範囲内かを返す
		//----------------------------------------------------
		public static bool IsValidIndex(int index)
		{
			return index >= 0 && index < CellCount;
		}

		//----------------------------------------------------
		// プレイヤーのトラップを設置する（NoTrap なら置かない）
		//----------------------------------------------------
		public void SetTrap(EPlayerId owner, int index)
		{
			if (index != NoTrap && !IsValidIndex(index))
			{
				throw new ArgumentOutOfRangeException(nameof(index));
			}

			m_TrapIndices[(int)owner] = index;
			m_TrapTriggered[(int)owner] = false;
		}

		//----------------------------------------------------
		// プレイヤーが設置したトラップのマス番号を返す（未設置なら NoTrap）
		//----------------------------------------------------
		public int GetTrapIndex(EPlayerId owner)
		{
			return m_TrapIndices[(int)owner];
		}

		//----------------------------------------------------
		// プレイヤーのトラップがまだ踏まれずに残っているかを返す
		//----------------------------------------------------
		public bool IsTrapActive(EPlayerId owner)
		{
			return m_TrapIndices[(int)owner] != NoTrap && !m_TrapTriggered[(int)owner];
		}

		//----------------------------------------------------
		// 指定マスにマークを置き、トラップの発動と反転を処理する
		//----------------------------------------------------
		public PlaceResult PlaceMark(EPlayerId player, int index)
		{
			if (!IsEmpty(index))
			{
				throw new InvalidOperationException($"マス {index} には置けません");
			}

			bool triggeredPlayer1 = TryTriggerTrap(EPlayerId.Player1, index);
			bool triggeredPlayer2 = TryTriggerTrap(EPlayerId.Player2, index);
			PlaceResult result = PlaceResult.Create(index, player, triggeredPlayer1, triggeredPlayer2);
			m_Cells[index] = result.FinalMark;
			return result;
		}

		//----------------------------------------------------
		// 別の端末で判定済みの結果をそのまま反映する（相手のトラップ位置を知らない側で使う）
		//----------------------------------------------------
		public void ApplyPlaceResult(PlaceResult result)
		{
			if (!IsEmpty(result.Index))
			{
				throw new InvalidOperationException($"マス {result.Index} には置けません");
			}

			m_Cells[result.Index] = result.FinalMark;
			if (result.TriggeredPlayer1Trap)
			{
				m_TrapTriggered[(int)EPlayerId.Player1] = true;
			}
			if (result.TriggeredPlayer2Trap)
			{
				m_TrapTriggered[(int)EPlayerId.Player2] = true;
			}
		}

		//----------------------------------------------------
		// 指定マスに残っているトラップがあれば発動済みにする
		//----------------------------------------------------
		private bool TryTriggerTrap(EPlayerId owner, int index)
		{
			if (!IsTrapActive(owner) || m_TrapIndices[(int)owner] != index)
			{
				return false;
			}

			m_TrapTriggered[(int)owner] = true;
			return true;
		}

		//----------------------------------------------------
		// 3つ並んだマークがあれば勝者マークと揃ったラインを返す
		//----------------------------------------------------
		public bool TryGetWinner(out EMark winner, out int[] line)
		{
			foreach (int[] candidate in WinLines)
			{
				EMark first = m_Cells[candidate[0]];
				if (first != EMark.None && first == m_Cells[candidate[1]] && first == m_Cells[candidate[2]])
				{
					winner = first;
					line = (int[])candidate.Clone();
					return true;
				}
			}

			winner = EMark.None;
			line = null;
			return false;
		}

		//----------------------------------------------------
		// 盤面がすべて埋まっているかを返す
		//----------------------------------------------------
		public bool IsFull()
		{
			foreach (EMark mark in m_Cells)
			{
				if (mark == EMark.None)
				{
					return false;
				}
			}
			return true;
		}
	}
}
