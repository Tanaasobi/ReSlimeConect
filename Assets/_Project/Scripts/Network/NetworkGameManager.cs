//====================================================
// NetworkGameManager.cs
// ネットワーク対戦の進行を管理する
// 受付画面で部屋を作る・コードで参加する・ランダムマッチのいずれかで相手と繋がったら対戦を始める
// ホスト（〇）が盤面とトラップを持ってルールを判定し、結果をゲスト（×）に送る
// ゲストはホストから届いた結果を表示し、自分の手番の入力だけをホストに送る
// トラップ設置は両者同時に行い、自分のトラップは自分の画面にだけ表示する
//====================================================
using System;
using System.Collections.Generic;
using System.Threading;
using RESlimeConect.Game;
using UnityEngine;

namespace RESlimeConect.Network
{
	public sealed class NetworkGameManager : MonoBehaviour
	{
		[Header("参照")]
		[SerializeField] private BoardView m_BoardView;
		[SerializeField] private GameUIView m_GameUIView;
		[SerializeField] private NetworkLobbyView m_LobbyView;

		[Header("設定")]
		[Tooltip("1手（トラップ設置を含む）の制限時間（秒）")]
		[SerializeField, Min(1f)] private float m_TurnTimeLimit = 30f;
		[Tooltip("ホストが相手の入力を待つとき、通信の遅れを見込んで制限時間に足す猶予（秒）")]
		[SerializeField, Min(0f)] private float m_RemoteGraceTime = 2f;
		[Tooltip("ホストが相手の演出の終了を待つ最大時間（秒）")]
		[SerializeField, Min(1f)] private float m_ReadyTimeout = 15f;
		[Tooltip("ゲストが対戦開始を待つ間に、準備完了を送り直す間隔（秒）")]
		[SerializeField, Min(0.1f)] private float m_HelloInterval = 1f;

		private const EPlayerId HostPlayer = EPlayerId.Player1;
		private const EPlayerId GuestPlayer = EPlayerId.Player2;
		private const int InvalidIndex = -2;
		private const float LobbyPollInterval = 0.1f;
		private const int Player1TrapBit = 1 << 0;
		private const int Player2TrapBit = 1 << 1;

		private readonly BoardModel m_Board = new BoardModel();
		private readonly NetworkSessionService m_Session = new NetworkSessionService();
		private LocalPlayerController m_LocalController;
		private NetworkMessenger m_Messenger;
		private EPlayerId m_LocalPlayer;
		private int m_Sequence;

		// 手番の時計（0 になったら自分の入力待ちを打ち切る）
		private bool m_IsClockRunning;
		private float m_RemainingTime;
		private CancellationTokenSource m_LocalInput;

		// 対戦中だけ有効なキャンセル（相手との切断や退出で中断する）
		private CancellationTokenSource m_MatchSource;

		// トラップ設置の状態（ゲストは締め切り時にホストが受け付けた位置で上書きする）
		private bool m_IsTrapPhaseOpen;
		private int m_SentTrapIndex = BoardModel.NoTrap;

		private bool m_IsLeaving;

		//----------------------------------------------------
		// 入力の受け口と UI のボタンイベントを用意する
		//----------------------------------------------------
		private void Awake()
		{
			m_LocalController = new LocalPlayerController(m_BoardView, m_GameUIView);
			m_GameUIView.ModeSelectClicked += OnModeSelectClicked;
			m_GameUIView.GameEndClicked += OnGameEndClicked;
		}

		//----------------------------------------------------
		// 受付と対戦を繰り返す
		//----------------------------------------------------
		private async void Start()
		{
			try
			{
				await RunAsync(destroyCancellationToken);
			}
			catch (OperationCanceledException)
			{
				// シーン終了時のキャンセルは正常終了として扱う
			}
		}

		//----------------------------------------------------
		// 切断の検知と、手番の残り時間の更新を行う
		//----------------------------------------------------
		private void Update()
		{
			if (m_Messenger != null && m_Messenger.IsDisconnected && m_MatchSource != null && !m_MatchSource.IsCancellationRequested)
			{
				m_MatchSource.Cancel();
			}

			if (!m_IsClockRunning)
			{
				return;
			}
			m_RemainingTime -= Time.deltaTime;
			m_GameUIView.SetTime(Mathf.Max(0, Mathf.CeilToInt(m_RemainingTime)));
			if (m_RemainingTime <= 0f)
			{
				CancelLocalInput();
			}
		}

		//----------------------------------------------------
		// シーンを離れるときに部屋から抜ける（NetworkManager はシーンをまたいで残るため）
		//----------------------------------------------------
		private void OnDestroy()
		{
			m_Messenger?.Dispose();
			m_Messenger = null;
			if (!m_IsLeaving)
			{
				_ = m_Session.LeaveAsync();
			}
		}

		//====================================================
		// 受付
		//====================================================

		//----------------------------------------------------
		// 受付で相手と繋がったら対戦し、切断されたら受付に戻る
		//----------------------------------------------------
		private async Awaitable RunAsync(CancellationToken token)
		{
			while (true)
			{
				if (!await RunLobbyAsync(token))
				{
					await LeaveForSceneChangeAsync();
					SceneLoader.TryLoad(SceneLoader.ModeSelect);
					return;
				}

				m_LobbyView.Hide();
				await RunMatchAsync(token);
				if (m_IsLeaving)
				{
					return;
				}
				await LeaveSessionAsync();
			}
		}

		//----------------------------------------------------
		// 受付画面の操作を受け付け、相手と繋がったら true、戻るが押されたら false を返す
		//----------------------------------------------------
		private async Awaitable<bool> RunLobbyAsync(CancellationToken token)
		{
			string error = null;
			while (true)
			{
				m_LobbyView.ShowMenu(error);
				error = null;

				ELobbyAction action = await m_LobbyView.WaitForActionAsync(token);
				if (action == ELobbyAction.Back)
				{
					return false;
				}

				try
				{
					await StartSessionAsync(action, token);
					if (await WaitForOpponentAsync(action == ELobbyAction.CreateRoom, token))
					{
						return true;
					}
				}
				catch (Exception exception) when (!token.IsCancellationRequested)
				{
					Debug.LogException(exception);
					error = $"接続できませんでした\n{exception.Message}";
				}
				await LeaveSessionAsync();
			}
		}

		//----------------------------------------------------
		// 選ばれた方法で部屋に入り、メッセージの送受信を始める
		//----------------------------------------------------
		private async Awaitable StartSessionAsync(ELobbyAction action, CancellationToken token)
		{
			switch (action)
			{
				case ELobbyAction.CreateRoom:
					m_LobbyView.ShowWaiting("部屋を作成しています…", null, false);
					await m_Session.CreateRoomAsync();
					break;
				case ELobbyAction.JoinRoom:
					string code = m_LobbyView.EnteredCode;
					m_LobbyView.ShowWaiting($"部屋 {code} に参加しています…", null, false);
					await m_Session.JoinRoomAsync(code);
					break;
				case ELobbyAction.QuickMatch:
					m_LobbyView.ShowWaiting("対戦相手を探しています…", null, false);
					await m_Session.QuickMatchAsync();
					break;
			}
			token.ThrowIfCancellationRequested();

			m_Messenger = new NetworkMessenger(m_Session.Manager);
			m_LocalPlayer = m_Session.IsHost ? HostPlayer : GuestPlayer;
		}

		//----------------------------------------------------
		// 相手の準備ができるまで待つ（キャンセルされたら false）
		// ホストはゲストからの準備完了を待ち、ゲストは届くまで準備完了を送り直しながらホストの開始を待つ
		//----------------------------------------------------
		private async Awaitable<bool> WaitForOpponentAsync(bool showCode, CancellationToken token)
		{
			bool isCancelled = false;
			Action onCancel = () => isCancelled = true;
			m_LobbyView.CancelClicked += onCancel;
			try
			{
				if (m_Session.IsHost)
				{
					if (showCode)
					{
						m_LobbyView.ShowWaiting("ルームコードを相手に伝えてください\n相手の参加を待っています", m_Session.SessionCode, true);
					}
					else
					{
						m_LobbyView.ShowWaiting("対戦相手を探しています…", null, true);
					}

					while (!isCancelled)
					{
						NetMessage? hello = await m_Messenger.ReceiveAsync(ENetMessageType.Hello, NetworkMessenger.AnySequence, LobbyPollInterval, token);
						if (hello.HasValue)
						{
							return true;
						}
					}
					return false;
				}

				m_LobbyView.ShowWaiting("対戦の開始を待っています…", null, true);
				float nextHelloTime = 0f;
				while (!isCancelled)
				{
					if (m_Messenger.HasPendingMessage)
					{
						return true;
					}
					m_Messenger.ThrowIfDisconnected();
					if (Time.unscaledTime >= nextHelloTime)
					{
						m_Messenger.Send(new NetMessage(ENetMessageType.Hello));
						nextHelloTime = Time.unscaledTime + m_HelloInterval;
					}
					await Awaitable.NextFrameAsync(token);
				}
				return false;
			}
			finally
			{
				m_LobbyView.CancelClicked -= onCancel;
			}
		}

		//====================================================
		// 対戦（共通）
		//====================================================

		//----------------------------------------------------
		// ホストかゲストとして対戦を続け、切断されたら知らせて戻る
		//----------------------------------------------------
		private async Awaitable RunMatchAsync(CancellationToken token)
		{
			m_MatchSource = CancellationTokenSource.CreateLinkedTokenSource(token);
			try
			{
				if (m_Session.IsHost)
				{
					await RunHostAsync(m_MatchSource.Token);
				}
				else
				{
					await RunGuestAsync(m_MatchSource.Token);
				}
			}
			catch (Exception exception) when (!token.IsCancellationRequested && (exception is OperationCanceledException || exception is NetworkDisconnectedException))
			{
				// 相手との切断、または自分から退出した
			}
			finally
			{
				DisposeNextFrame(m_MatchSource);
				m_MatchSource = null;
			}

			if (m_IsLeaving)
			{
				return;
			}
			ResetMatchView();
			await m_GameUIView.ShowHandoffAsync("相手との接続が切れました\n受付画面に戻ります", token);
		}

		//----------------------------------------------------
		// 新しい対戦に向けて盤面と表示を初期化する
		//----------------------------------------------------
		private void StartRound()
		{
			m_Board.Reset();
			m_BoardView.ResetView();
			m_GameUIView.HideResult();
			m_SentTrapIndex = BoardModel.NoTrap;
		}

		//----------------------------------------------------
		// トラップ設置を始める（自分のスライムを表示して時計を動かす）
		//----------------------------------------------------
		private void BeginTrapPhase()
		{
			m_IsTrapPhaseOpen = true;
			m_BoardView.SetHoverPreviewTrap();
			m_GameUIView.SetPlayerSlime(GetSlime(m_LocalPlayer).Idle);
			m_GameUIView.SetStatus("トラップを置くマスを選んでください（相手には見えません）");
			StartClock();
		}

		//----------------------------------------------------
		// トラップ設置を締め切る
		//----------------------------------------------------
		private void EndTrapPhase()
		{
			m_IsTrapPhaseOpen = false;
			StopClock();
		}

		//----------------------------------------------------
		// 手番を始める（手番のスライムと状況を表示して時計を動かす）
		//----------------------------------------------------
		private void BeginTurn(EPlayerId player)
		{
			m_BoardView.SetHoverPreviewSlime(GameRuleUtility.GetMark(player));
			m_GameUIView.SetPlayerSlime(GetSlime(player).Idle);
			m_GameUIView.SetStatus(player == m_LocalPlayer ? $"あなた（{GetMarkName(player)}）の番です" : "相手の番です");
			StartClock();
		}

		//----------------------------------------------------
		// 自分のトラップの設置演出を再生し、締め切りまで自分の画面にだけ表示しておく
		//----------------------------------------------------
		private async Awaitable ShowOwnTrapAsync(int index, CancellationToken token)
		{
			m_GameUIView.SetStatus("相手のトラップ設置を待っています");
			if (!BoardModel.IsValidIndex(index))
			{
				return;
			}

			await m_BoardView.PlayTrapPlaceAsync(index, token);
			// 演出中に締め切られて受け付けられなかった場合は表示しない
			if (m_IsTrapPhaseOpen || m_Board.GetTrapIndex(m_LocalPlayer) == index)
			{
				m_BoardView.RevealTrap(index);
			}
		}

		//----------------------------------------------------
		// 置いた演出を再生する（踏まれた自分のトラップの表示は消す）
		//----------------------------------------------------
		private Awaitable PlayPlaceAsync(PlaceResult result, CancellationToken token)
		{
			if (result.IsTrapTriggered)
			{
				m_BoardView.HideTrap(result.Index);
			}
			return m_BoardView.PlayPlaceAsync(result, token);
		}

		//----------------------------------------------------
		// 揃ったラインがあれば勝利アニメーションを再生する
		//----------------------------------------------------
		private void ShowWinLine()
		{
			if (m_Board.TryGetWinner(out EMark winner, out int[] line))
			{
				m_BoardView.PlayWinLine(line, winner);
			}
		}

		//----------------------------------------------------
		// 結果を表示し、再戦ボタンが押されるまで待つ
		//----------------------------------------------------
		private async Awaitable ShowResultAsync(EGameResult result, CancellationToken token)
		{
			StopClock();
			m_GameUIView.SetTimeVisible(false);
			m_GameUIView.SetStatus(string.Empty);

			if (result == EGameResult.Draw)
			{
				m_GameUIView.SetPlayerSlime(null);
				await m_GameUIView.ShowResultAsync("引き分け", token);
			}
			else
			{
				EPlayerId winner = result == EGameResult.Player1Win ? EPlayerId.Player1 : EPlayerId.Player2;
				m_GameUIView.SetPlayerSlime(GetSlime(winner).Win);
				await m_GameUIView.ShowResultAsync(winner == m_LocalPlayer ? "あなたの勝利！" : "あなたの負け…", token);
			}
			m_GameUIView.SetStatus("相手の再戦の返事を待っています");
		}

		//----------------------------------------------------
		// 自分の入力を制限時間つきで待ち、時間切れか打ち切られたら onTimeout の結果を返す
		//----------------------------------------------------
		private async Awaitable<int> RequestLocalAsync(Func<CancellationToken, Awaitable<int>> request, Func<int> onTimeout, CancellationToken token)
		{
			CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(token);
			m_LocalInput = source;
			try
			{
				return await request(source.Token);
			}
			catch (OperationCanceledException) when (!token.IsCancellationRequested)
			{
				// 操作主体は打ち切り時に後片付けをしないので、ここで入力状態を戻す
				ResetInputView();
				return onTimeout();
			}
			finally
			{
				if (m_LocalInput == source)
				{
					m_LocalInput = null;
				}
				// Cancel 呼び出しの途中で破棄しないよう、次のフレームで破棄する
				DisposeNextFrame(source);
			}
		}

		//----------------------------------------------------
		// 自分の入力待ちを打ち切る
		//----------------------------------------------------
		private void CancelLocalInput()
		{
			CancellationTokenSource input = m_LocalInput;
			m_LocalInput = null;
			input?.Cancel();
		}

		//----------------------------------------------------
		// 時計を制限時間から動かし、残り時間枠を表示する
		//----------------------------------------------------
		private void StartClock()
		{
			m_RemainingTime = m_TurnTimeLimit;
			m_IsClockRunning = true;
			m_GameUIView.SetTimeVisible(true);
			m_GameUIView.SetTime(Mathf.CeilToInt(m_TurnTimeLimit));
		}

		//----------------------------------------------------
		// 時計を止める
		//----------------------------------------------------
		private void StopClock()
		{
			m_IsClockRunning = false;
		}

		//----------------------------------------------------
		// 盤面の入力とトラップ操作ボタンを元に戻す
		//----------------------------------------------------
		private void ResetInputView()
		{
			m_GameUIView.ShowTrapControls(false);
			m_BoardView.SetInputEnabled(false);
			m_BoardView.HideTrapPreview();
		}

		//----------------------------------------------------
		// 対戦が中断されたときに、入力待ちや表示を片付ける
		//----------------------------------------------------
		private void ResetMatchView()
		{
			StopClock();
			CancelLocalInput();
			ResetInputView();
			m_GameUIView.HideResult();
			m_GameUIView.SetTimeVisible(false);
			m_GameUIView.SetStatus(string.Empty);
		}

		//====================================================
		// 対戦（ホスト）
		//====================================================

		//----------------------------------------------------
		// 対戦を繰り返す（再戦のたびに先手を入れ替える）
		//----------------------------------------------------
		private async Awaitable RunHostAsync(CancellationToken token)
		{
			EPlayerId firstPlayer = HostPlayer;
			while (true)
			{
				await RunHostRoundAsync(firstPlayer, token);
				firstPlayer = GameRuleUtility.GetOpponent(firstPlayer);
			}
		}

		//----------------------------------------------------
		// 1回分の対戦を判定しながら進め、結果をゲストに送る
		//----------------------------------------------------
		private async Awaitable RunHostRoundAsync(EPlayerId firstPlayer, CancellationToken token)
		{
			StartRound();
			m_Messenger.Send(new NetMessage(ENetMessageType.RoundStart, NetMessage.NoSequence, (int)firstPlayer));

			// トラップ設置（両者同時）
			int trapSequence = ++m_Sequence;
			m_Messenger.Send(new NetMessage(ENetMessageType.TrapPhaseStart, trapSequence));
			BeginTrapPhase();
			Awaitable<int> guestTrapRequest = ReceiveIndexAsync(ENetMessageType.Trap, trapSequence, BoardModel.NoTrap, token);
			int hostTrap = await RequestLocalAsync(
				inputToken => m_LocalController.RequestTrapAsync(m_LocalPlayer, m_Board, inputToken),
				() => BoardModel.NoTrap,
				token);
			await ShowOwnTrapAsync(hostTrap, token);
			int guestTrap = await guestTrapRequest;
			if (!BoardModel.IsValidIndex(guestTrap))
			{
				guestTrap = BoardModel.NoTrap;
			}
			EndTrapPhase();
			m_Board.SetTrap(HostPlayer, hostTrap);
			m_Board.SetTrap(GuestPlayer, guestTrap);
			m_Messenger.Send(new NetMessage(ENetMessageType.TrapPhaseEnd, trapSequence, guestTrap));

			// 対戦
			EGameResult result;
			EPlayerId current = firstPlayer;
			while (true)
			{
				int sequence = ++m_Sequence;
				m_Messenger.Send(new NetMessage(ENetMessageType.TurnStart, sequence, (int)current));
				BeginTurn(current);

				int index;
				if (current == m_LocalPlayer)
				{
					index = await RequestLocalAsync(
						inputToken => m_LocalController.RequestMoveAsync(current, m_Board, inputToken),
						ChooseRandomEmptyCell,
						token);
				}
				else
				{
					// 時間切れや不正なマスなら、ローカル対戦と同じくランダムに置く
					index = await ReceiveIndexAsync(ENetMessageType.Move, sequence, InvalidIndex, token);
					if (!m_Board.IsEmpty(index))
					{
						index = ChooseRandomEmptyCell();
					}
				}
				StopClock();

				PlaceResult placed = m_Board.PlaceMark(current, index);
				m_Messenger.Send(new NetMessage(ENetMessageType.Placed, sequence, index, (int)current, EncodeTriggers(placed)));
				await PlayPlaceAsync(placed, token);
				await m_Messenger.ReceiveAsync(ENetMessageType.Ready, sequence, m_ReadyTimeout, token);

				if (TryGetResult(out result))
				{
					break;
				}
				current = GameRuleUtility.GetOpponent(current);
			}

			// 決着
			m_Messenger.Send(new NetMessage(ENetMessageType.RoundEnd, NetMessage.NoSequence, (int)result, GetActiveTrap(EPlayerId.Player1), GetActiveTrap(EPlayerId.Player2)));
			ShowWinLine();
			m_BoardView.RevealRemainingTraps(m_Board);
			Awaitable<NetMessage?> guestRematch = m_Messenger.ReceiveAsync(ENetMessageType.Rematch, NetworkMessenger.AnySequence, float.PositiveInfinity, token);
			await ShowResultAsync(result, token);
			await guestRematch;
		}

		//----------------------------------------------------
		// ゲストからマス番号つきのメッセージを制限時間（猶予込み）まで待ち、届かなければ fallback を返す
		//----------------------------------------------------
		private async Awaitable<int> ReceiveIndexAsync(ENetMessageType type, int sequence, int fallback, CancellationToken token)
		{
			NetMessage? message = await m_Messenger.ReceiveAsync(type, sequence, m_TurnTimeLimit + m_RemoteGraceTime, token);
			return message.HasValue ? message.Value.Arg0 : fallback;
		}

		//----------------------------------------------------
		// 勝敗が決まっていれば結果を返す
		//----------------------------------------------------
		private bool TryGetResult(out EGameResult result)
		{
			// 変化で相手のラインが揃うこともあるので、置いた側に限らず判定する
			if (m_Board.TryGetWinner(out EMark winner, out _))
			{
				result = GameRuleUtility.GetOwner(winner) == EPlayerId.Player1 ? EGameResult.Player1Win : EGameResult.Player2Win;
				return true;
			}
			if (m_Board.IsFull())
			{
				result = EGameResult.Draw;
				return true;
			}
			result = EGameResult.None;
			return false;
		}

		//----------------------------------------------------
		// 踏まれずに残っているトラップの位置を返す（無ければ NoTrap）
		//----------------------------------------------------
		private int GetActiveTrap(EPlayerId owner)
		{
			return m_Board.IsTrapActive(owner) ? m_Board.GetTrapIndex(owner) : BoardModel.NoTrap;
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
		// 踏んだトラップをビットにまとめる
		//----------------------------------------------------
		private static int EncodeTriggers(PlaceResult result)
		{
			return (result.TriggeredPlayer1Trap ? Player1TrapBit : 0) | (result.TriggeredPlayer2Trap ? Player2TrapBit : 0);
		}

		//====================================================
		// 対戦（ゲスト）
		//====================================================

		//----------------------------------------------------
		// ホストから届くメッセージを順に処理する
		//----------------------------------------------------
		private async Awaitable RunGuestAsync(CancellationToken token)
		{
			while (true)
			{
				NetMessage message = await m_Messenger.ReceiveNextAsync(token);
				switch (message.Type)
				{
					case ENetMessageType.RoundStart:
						StartRound();
						break;

					case ENetMessageType.TrapPhaseStart:
						BeginTrapPhase();
						SendOwnTrap(message.Sequence, token);
						break;

					case ENetMessageType.TrapPhaseEnd:
						CancelLocalInput();
						EndTrapPhase();
						ApplyAcceptedTrap(message.Arg0);
						break;

					case ENetMessageType.TurnStart:
						EPlayerId player = (EPlayerId)message.Arg0;
						BeginTurn(player);
						if (player == m_LocalPlayer)
						{
							SendOwnMove(message.Sequence, token);
						}
						break;

					case ENetMessageType.Placed:
						CancelLocalInput();
						StopClock();
						PlaceResult result = PlaceResult.Create(message.Arg0, (EPlayerId)message.Arg1, (message.Arg2 & Player1TrapBit) != 0, (message.Arg2 & Player2TrapBit) != 0);
						m_Board.ApplyPlaceResult(result);
						await PlayPlaceAsync(result, token);
						m_Messenger.Send(new NetMessage(ENetMessageType.Ready, message.Sequence));
						break;

					case ENetMessageType.RoundEnd:
						ShowWinLine();
						m_BoardView.RevealTrap(message.Arg1);
						m_BoardView.RevealTrap(message.Arg2);
						await ShowResultAsync((EGameResult)message.Arg0, token);
						m_Messenger.Send(new NetMessage(ENetMessageType.Rematch));
						break;

					default:
						Debug.LogWarning($"[NetworkGameManager] 想定していないメッセージです: {message}");
						break;
				}
			}
		}

		//----------------------------------------------------
		// 自分のトラップ位置を選んでホストに送る（時間切れなら置かないとして送る）
		//----------------------------------------------------
		private async void SendOwnTrap(int sequence, CancellationToken token)
		{
			try
			{
				int index = await RequestLocalAsync(
					inputToken => m_LocalController.RequestTrapAsync(m_LocalPlayer, m_Board, inputToken),
					() => BoardModel.NoTrap,
					token);
				m_SentTrapIndex = index;
				m_Messenger?.Send(new NetMessage(ENetMessageType.Trap, sequence, index));
				await ShowOwnTrapAsync(index, token);
			}
			catch (OperationCanceledException)
			{
				// 切断・退出で対戦ごと中断された
			}
		}

		//----------------------------------------------------
		// ホストが受け付けた自分のトラップ位置を反映する（締め切りに間に合わなかった表示は消す）
		//----------------------------------------------------
		private void ApplyAcceptedTrap(int acceptedIndex)
		{
			if (m_SentTrapIndex != acceptedIndex)
			{
				m_BoardView.HideTrap(m_SentTrapIndex);
			}
			m_Board.SetTrap(m_LocalPlayer, BoardModel.IsValidIndex(acceptedIndex) ? acceptedIndex : BoardModel.NoTrap);
		}

		//----------------------------------------------------
		// 自分の置くマスを選んでホストに送る（時間切れならホストがランダムに置くので何も送らない）
		//----------------------------------------------------
		private async void SendOwnMove(int sequence, CancellationToken token)
		{
			try
			{
				int index = await RequestLocalAsync(
					inputToken => m_LocalController.RequestMoveAsync(m_LocalPlayer, m_Board, inputToken),
					() => InvalidIndex,
					token);
				if (index != InvalidIndex)
				{
					m_Messenger?.Send(new NetMessage(ENetMessageType.Move, sequence, index));
				}
			}
			catch (OperationCanceledException)
			{
				// 切断・退出で対戦ごと中断された
			}
		}

		//====================================================
		// 退出
		//====================================================

		//----------------------------------------------------
		// モード選択ボタン（部屋から抜けてモード選択画面に戻る）
		//----------------------------------------------------
		private async void OnModeSelectClicked()
		{
			await LeaveForSceneChangeAsync();
			SceneLoader.TryLoad(SceneLoader.ModeSelect);
		}

		//----------------------------------------------------
		// ゲーム終了ボタン（部屋から抜けて終了する。エディタでは再生を止める）
		//----------------------------------------------------
		private async void OnGameEndClicked()
		{
			await LeaveForSceneChangeAsync();
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}

		//----------------------------------------------------
		// シーンを離れる前に、対戦を止めて部屋から抜ける
		//----------------------------------------------------
		private async Awaitable LeaveForSceneChangeAsync()
		{
			if (m_IsLeaving)
			{
				return;
			}
			m_IsLeaving = true;
			m_MatchSource?.Cancel();
			await LeaveSessionAsync();
		}

		//----------------------------------------------------
		// メッセージの送受信を止めて部屋から抜ける
		//----------------------------------------------------
		private async Awaitable LeaveSessionAsync()
		{
			m_Messenger?.Dispose();
			m_Messenger = null;
			await m_Session.LeaveAsync();
		}

		//====================================================
		// 共通処理
		//====================================================

		//----------------------------------------------------
		// プレイヤーのスライムのアニメーション一式を返す
		//----------------------------------------------------
		private SlimeAnimationSet GetSlime(EPlayerId player)
		{
			return m_BoardView.Skin.GetSlime(GameRuleUtility.GetMark(player));
		}

		//----------------------------------------------------
		// プレイヤーのマークの表示名を返す
		//----------------------------------------------------
		private static string GetMarkName(EPlayerId player)
		{
			return GameRuleUtility.GetMark(player) == EMark.Circle ? "〇" : "×";
		}

		//----------------------------------------------------
		// Cancel 呼び出しの途中で破棄しないよう、次のフレームで破棄する
		//----------------------------------------------------
		private static async void DisposeNextFrame(CancellationTokenSource source)
		{
			await Awaitable.NextFrameAsync();
			source.Dispose();
		}
	}
}
