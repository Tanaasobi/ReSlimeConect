//====================================================
// LocalPlayerController.cs
// 盤面クリックと UI ボタンで操作するローカルプレイヤー
//====================================================
using System;
using System.Threading;
using UnityEngine;

namespace RESlimeConect.Game
{
	public sealed class LocalPlayerController : IPlayerController
	{
		private readonly BoardView m_BoardView;
		private readonly GameUIView m_GameUIView;

		//----------------------------------------------------
		// 入力を受け取る盤面ビューと UI を指定して生成する
		//----------------------------------------------------
		public LocalPlayerController(BoardView boardView, GameUIView gameUIView)
		{
			m_BoardView = boardView;
			m_GameUIView = gameUIView;
		}

		//----------------------------------------------------
		// マスを選んで決定するか、置かないボタンでトラップ位置を決める
		//----------------------------------------------------
		public async Awaitable<int> RequestTrapAsync(EPlayerId self, BoardModel board, CancellationToken token)
		{
			AwaitableCompletionSource<int> source = new AwaitableCompletionSource<int>();
			int selected = BoardModel.NoTrap;

			Action<int> onCellClicked = index =>
			{
				selected = index;
				m_BoardView.ShowTrapPreview(index);
				m_GameUIView.SetTrapConfirmInteractable(true);
			};
			Action onConfirm = () =>
			{
				if (selected != BoardModel.NoTrap)
				{
					source.TrySetResult(selected);
				}
			};
			Action onSkip = () => source.TrySetResult(BoardModel.NoTrap);

			m_BoardView.CellClicked += onCellClicked;
			m_GameUIView.TrapConfirmClicked += onConfirm;
			m_GameUIView.TrapSkipClicked += onSkip;
			m_GameUIView.ShowTrapControls(true);
			m_GameUIView.SetTrapConfirmInteractable(false);
			m_BoardView.SetInputEnabled(true, board.IsEmpty);

			try
			{
				using (token.Register(() => source.TrySetCanceled()))
				{
					return await source.Awaitable;
				}
			}
			finally
			{
				m_BoardView.CellClicked -= onCellClicked;
				m_GameUIView.TrapConfirmClicked -= onConfirm;
				m_GameUIView.TrapSkipClicked -= onSkip;

				// キャンセル時（時間切れ・シーン破棄）は呼び出し側が後片付けするので、見た目には触らない
				if (!token.IsCancellationRequested)
				{
					m_GameUIView.ShowTrapControls(false);
					m_BoardView.SetInputEnabled(false);
					m_BoardView.HideTrapPreview();
				}
			}
		}

		//----------------------------------------------------
		// 空きマスがクリックされるまで待ち、そのマス番号を返す
		//----------------------------------------------------
		public async Awaitable<int> RequestMoveAsync(EPlayerId self, BoardModel board, CancellationToken token)
		{
			AwaitableCompletionSource<int> source = new AwaitableCompletionSource<int>();
			Action<int> onCellClicked = index => source.TrySetResult(index);

			m_BoardView.CellClicked += onCellClicked;
			m_BoardView.SetInputEnabled(true, board.IsEmpty);

			try
			{
				using (token.Register(() => source.TrySetCanceled()))
				{
					return await source.Awaitable;
				}
			}
			finally
			{
				m_BoardView.CellClicked -= onCellClicked;

				// キャンセル時（時間切れ・シーン破棄）は呼び出し側が後片付けするので、見た目には触らない
				if (!token.IsCancellationRequested)
				{
					m_BoardView.SetInputEnabled(false);
				}
			}
		}
	}
}
