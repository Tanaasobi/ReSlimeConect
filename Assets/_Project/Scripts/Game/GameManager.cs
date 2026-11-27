//====================================================
// GameManager.cs
// トラップフェーズ・対戦フェーズ・リザルトフェーズの進行と、1手ごとの時間制限を管理する
//====================================================
using System;
using System.Collections.Generic;
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
		[Tooltip("1手（トラップ設置を含む）の制限時間（秒）")]
		[SerializeField, Min(1f)] private float m_TurnTimeLimit = 30f;

		private readonly BoardModel m_Board = new BoardModel();
		private readonly IPlayerController[] m_Controllers = new IPlayerController[2];
		private EGamePhase m_Phase = EGamePhase.None;

		private CancellationTokenSource m_TurnTimer;
		private float m_RemainingTime;

		public EGamePhase Phase => m_Phase;
		public BoardModel Board => m_Board;

		public event Action<EGamePhase> PhaseChanged;

		//----------------------------------------------------
		// UI のボタンイベントを登録する
		//----------------------------------------------------
		private void Awake()
		{
			m_GameUIView.ModeSelectClicked += OnModeSelectClicked;
			m_GameUIView.GameEndClicked += OnGameEndClicked;
		}

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
		// 手番の残り時間を減らし、0 になったら手番の入力待ちを打ち切る
		//----------------------------------------------------
		private void Update()
		{
			if (m_TurnTimer == null)
			{
				return;
			}

			m_RemainingTime -= Time.deltaTime;
			m_GameUIView.SetTime(Mathf.Max(0, Mathf.CeilToInt(m_RemainingTime)));
			if (m_RemainingTime <= 0f)
			{
				CancellationTokenSource timer = m_TurnTimer;
				m_TurnTimer = null;
				timer.Cancel();
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
		// 3つのフェーズを再戦のたびに繰り返す
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
			m_BoardView.SetHoverPreviewTrap();

			EPlayerId[] order = { m_FirstPlayer, GameRuleUtility.GetOpponent(m_FirstPlayer) };
			foreach (EPlayerId player in order)
			{
				if (m_UseHandoffScreen)
				{
					await m_GameUIView.ShowHandoffAsync($"{GameRuleUtility.GetDisplayName(player)} のトラップ設置です\n相手は画面を見ないでください", token);
				}

				ShowTurnPlayer(player);
				IPlayerController controller = m_Controllers[(int)player];
				int trapIndex = await RequestWithTimeLimitAsync(
					turnToken => controller.RequestTrapAsync(player, m_Board, turnToken),
					() => BoardModel.NoTrap,
					token);

				m_Board.SetTrap(player, trapIndex);
				if (trapIndex != BoardModel.NoTrap)
				{
					await m_BoardView.PlayTrapPlaceAsync(trapIndex, token);
				}
			}

			m_GameUIView.SetTimeVisible(false);
			if (m_UseHandoffScreen)
			{
				await m_GameUIView.ShowHandoffAsync("トラップの設置が完了しました\n対戦を始めます", token);
			}
		}

		//----------------------------------------------------
		// 交互にスライムを置き、勝敗が決まるか盤面が埋まるまで続ける
		//----------------------------------------------------
		private async Awaitable<EGameResult> RunBattlePhaseAsync(CancellationToken token)
		{
			ChangePhase(EGamePhase.Battle);

			EPlayerId current = m_FirstPlayer;
			while (true)
			{
				ShowTurnPlayer(current);
				m_BoardView.SetHoverPreviewSlime(GameRuleUtility.GetMark(current));

				IPlayerController controller = m_Controllers[(int)current];
				EPlayerId player = current;
				int index = await RequestWithTimeLimitAsync(
					turnToken => controller.RequestMoveAsync(player, m_Board, turnToken),
					ChooseRandomEmptyCell,
					token);

				PlaceResult result = m_Board.PlaceMark(current, index);
				await m_BoardView.PlayPlaceAsync(result, token);

				// 変化で相手のラインが揃うこともあるので、置いた側に限らず判定する
				if (m_Board.TryGetWinner(out EMark winner, out int[] line))
				{
					m_BoardView.PlayWinLine(line, winner);
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
		// 勝者と残ったトラップを表示し、再戦が押されるまで待つ
		//----------------------------------------------------
		private async Awaitable RunResultPhaseAsync(EGameResult result, CancellationToken token)
		{
			ChangePhase(EGamePhase.Result);
			m_GameUIView.SetTimeVisible(false);
			m_BoardView.RevealRemainingTraps(m_Board);

			if (result == EGameResult.Draw)
			{
				m_GameUIView.SetPlayerSlime(null);
				await m_GameUIView.ShowResultAsync("引き分け", token);
				return;
			}

			EMark winner = result == EGameResult.Player1Win ? EMark.Circle : EMark.Cross;
			m_GameUIView.SetPlayerSlime(m_BoardView.Skin.GetSlime(winner).Win);
			await m_GameUIView.ShowResultAsync($"{(winner == EMark.Circle ? "〇" : "×")} の勝利！", token);
		}

		//----------------------------------------------------
		// 手番プレイヤーのスライムと残り時間枠を表示する
		//----------------------------------------------------
		private void ShowTurnPlayer(EPlayerId player)
		{
			m_GameUIView.SetPlayerSlime(m_BoardView.Skin.GetSlime(GameRuleUtility.GetMark(player)).Idle);
			m_GameUIView.SetTimeVisible(true);
			m_GameUIView.SetTime(Mathf.CeilToInt(m_TurnTimeLimit));
		}

		//----------------------------------------------------
		// 制限時間つきで入力を待ち、時間切れなら代わりの結果を返す
		//----------------------------------------------------
		private async Awaitable<int> RequestWithTimeLimitAsync(Func<CancellationToken, Awaitable<int>> request, Func<int> onTimeout, CancellationToken token)
		{
			using (CancellationTokenSource turnSource = CancellationTokenSource.CreateLinkedTokenSource(token))
			{
				m_TurnTimer = turnSource;
				m_RemainingTime = m_TurnTimeLimit;
				try
				{
					return await request(turnSource.Token);
				}
				catch (OperationCanceledException) when (!token.IsCancellationRequested)
				{
					// 時間切れ。操作主体は後片付けをしないので、ここで入力状態を戻す
					m_GameUIView.ShowTrapControls(false);
					m_BoardView.SetInputEnabled(false);
					m_BoardView.HideTrapPreview();

					// Update 内の Cancel 呼び出しの途中で turnSource を破棄しないよう、1フレーム待ってから進める
					await Awaitable.NextFrameAsync(token);
					return onTimeout();
				}
				finally
				{
					m_TurnTimer = null;
				}
			}
		}

		//----------------------------------------------------
		// 空いているマスからランダムに1つ選ぶ
		//----------------------------------------------------
		private int ChooseRandomEmptyCell()
		{
			List<int> emptyCells = new List<int>();
			for (int i = 0; i < BoardModel.CellCount; i++)
			{
				if (m_Board.IsEmpty(i))
				{
					emptyCells.Add(i);
				}
			}
			return emptyCells[UnityEngine.Random.Range(0, emptyCells.Count)];
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
		// モード選択ボタン（モード選択画面ができるまでは何もしない）
		//----------------------------------------------------
		private void OnModeSelectClicked()
		{
			Debug.Log("[GameManager] モード選択は未実装です");
		}

		//----------------------------------------------------
		// ゲーム終了ボタン（エディタでは再生を止める）
		//----------------------------------------------------
		private void OnGameEndClicked()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}
	}
}
