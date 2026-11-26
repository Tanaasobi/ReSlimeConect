//====================================================
// GameManager.cs
// トラップフェーズ・対戦フェーズ・リザルトフェーズの進行を管理する
//====================================================
using System;
using System.Threading;
using UnityEngine;

namespace RESlimeConect.Game
{
	public sealed class GameManager : MonoBehaviour
	{
		[Header("参照")]
		[SerializeField] private BoardView m_BoardView;
		[SerializeField] private GameUIView m_GameUIView;

		[Header("設定")]
		[Tooltip("1台を交代で使うときに、トラップ設置前に相手に見せない交代画面を挟む")]
		[SerializeField] private bool m_UseHandoffScreen = true;
		[SerializeField] private EPlayerId m_FirstPlayer = EPlayerId.Player1;

		private readonly BoardModel m_Board = new BoardModel();
		private readonly IPlayerController[] m_Controllers = new IPlayerController[2];
		private EGamePhase m_Phase = EGamePhase.None;

		public EGamePhase Phase => m_Phase;
		public BoardModel Board => m_Board;

		public event Action<EGamePhase> PhaseChanged;

		//----------------------------------------------------
		// 操作主体を用意してゲームループを開始する
		//----------------------------------------------------
		private async void Start()
		{
			// 今はローカル対戦のみ。モード選択を作ったらここで CPU やオンラインに差し替える
			SetController(EPlayerId.Player1, new LocalPlayerController(m_BoardView, m_GameUIView));
			SetController(EPlayerId.Player2, new LocalPlayerController(m_BoardView, m_GameUIView));

			try
			{
				await RunGameLoopAsync(destroyCancellationToken);
			}
			catch (OperationCanceledException)
			{
				// シーン終了時のキャンセルは正常終了として扱う
			}
		}

		//----------------------------------------------------
		// 指定プレイヤーの操作主体を設定する
		//----------------------------------------------------
		public void SetController(EPlayerId player, IPlayerController controller)
		{
			m_Controllers[(int)player] = controller;
		}

		//----------------------------------------------------
		// 3つのフェーズをリトライのたびに繰り返す
		//----------------------------------------------------
		private async Awaitable RunGameLoopAsync(CancellationToken token)
		{
			while (true)
			{
				m_Board.Reset();
				m_BoardView.ResetView();

				await RunTrapPhaseAsync(token);
				EGameResult result = await RunBattlePhaseAsync(token);
				await RunResultPhaseAsync(result, token);
			}
		}

		//----------------------------------------------------
		// 各プレイヤーが順番にトラップを1つまで設置する
		//----------------------------------------------------
		private async Awaitable RunTrapPhaseAsync(CancellationToken token)
		{
			ChangePhase(EGamePhase.TrapPlacement);
			m_GameUIView.SetPhase("トラップフェーズ");

			EPlayerId[] order = { m_FirstPlayer, GameRuleUtility.GetOpponent(m_FirstPlayer) };
			foreach (EPlayerId player in order)
			{
				string playerName = GameRuleUtility.GetDisplayName(player);
				m_GameUIView.SetMessage(string.Empty);

				if (m_UseHandoffScreen)
				{
					await m_GameUIView.ShowHandoffAsync($"{playerName} のトラップ設置です\n相手は画面を見ないでください", token);
				}

				m_GameUIView.SetMessage($"{playerName}：トラップを置くマスを選んでください（置かなくてもOK）");
				int trapIndex = await m_Controllers[(int)player].RequestTrapAsync(player, m_Board, token);
				m_Board.SetTrap(player, trapIndex);
			}

			if (m_UseHandoffScreen)
			{
				await m_GameUIView.ShowHandoffAsync("トラップの設置が完了しました\n対戦を始めます", token);
			}
		}

		//----------------------------------------------------
		// 交互にマークを置き、勝敗が決まるか盤面が埋まるまで続ける
		//----------------------------------------------------
		private async Awaitable<EGameResult> RunBattlePhaseAsync(CancellationToken token)
		{
			ChangePhase(EGamePhase.Battle);
			m_GameUIView.SetPhase("対戦フェーズ");

			EPlayerId current = m_FirstPlayer;
			while (true)
			{
				m_GameUIView.SetMessage($"{GameRuleUtility.GetDisplayName(current)} の番です");
				int index = await m_Controllers[(int)current].RequestMoveAsync(current, m_Board, token);

				PlaceResult result = m_Board.PlaceMark(current, index);
				if (result.IsTrapTriggered)
				{
					m_GameUIView.SetMessage(BuildTrapMessage(result));
				}
				await m_BoardView.PlayPlaceAsync(result, token);

				// 反転で相手のラインが揃うこともあるので、置いた側に限らず判定する
				if (m_Board.TryGetWinner(out EMark winner, out int[] line))
				{
					m_BoardView.ShowWinLine(line);
					return GameRuleUtility.GetOwner(winner) == EPlayerId.Player1 ? EGameResult.Player1Win : EGameResult.Player2Win;
				}
				if (m_Board.IsFull())
				{
					return EGameResult.Draw;
				}

				current = GameRuleUtility.GetOpponent(current);
			}
		}

		//----------------------------------------------------
		// 結果と残ったトラップを表示し、もう一度が押されるまで待つ
		//----------------------------------------------------
		private async Awaitable RunResultPhaseAsync(EGameResult result, CancellationToken token)
		{
			ChangePhase(EGamePhase.Result);
			m_GameUIView.SetPhase("リザルト");
			m_GameUIView.SetMessage("踏まれなかったトラップを表示しています");
			m_BoardView.RevealRemainingTraps(m_Board);

			await m_GameUIView.ShowResultAsync(BuildResultMessage(result), token);
		}

		//----------------------------------------------------
		// 現在フェーズを更新して通知する
		//----------------------------------------------------
		private void ChangePhase(EGamePhase phase)
		{
			m_Phase = phase;
			PhaseChanged?.Invoke(phase);
		}

		//----------------------------------------------------
		// トラップ発動時の案内文を作る
		//----------------------------------------------------
		private static string BuildTrapMessage(PlaceResult result)
		{
			if (result.TrapCount >= 2)
			{
				return "両方のトラップを踏んだ！2回反転して元に戻る";
			}

			EPlayerId owner = result.TriggeredPlayer1Trap ? EPlayerId.Player1 : EPlayerId.Player2;
			string ownerText = owner == result.Player ? "自分" : "相手";
			return $"{ownerText}のトラップを踏んだ！マークが反転する";
		}

		//----------------------------------------------------
		// リザルト画面の表示文を作る
		//----------------------------------------------------
		private static string BuildResultMessage(EGameResult result)
		{
			switch (result)
			{
				case EGameResult.Player1Win:
					return $"{GameRuleUtility.GetDisplayName(EPlayerId.Player1)} の勝ち！";
				case EGameResult.Player2Win:
					return $"{GameRuleUtility.GetDisplayName(EPlayerId.Player2)} の勝ち！";
				default:
					return "引き分け";
			}
		}
	}
}
