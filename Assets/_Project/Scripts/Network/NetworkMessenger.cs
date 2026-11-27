//====================================================
// NetworkMessenger.cs
// Netcode for GameObjects の名前付きメッセージで、相手の端末と NetMessage をやり取りする
// 受け取ったメッセージは届いた順に溜めておき、ゲーム進行側が必要なときに取り出す
// NetworkObject を使わないので、シーンに Prefab やネットワーク用オブジェクトを置く必要がない
//====================================================
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RESlimeConect.Network
{
	public sealed class NetworkMessenger : IDisposable
	{
		private const string MessageName = "RESlimeConect.Game";
		private const int MessageSize = sizeof(byte) + sizeof(int) * 4;
		private const ulong NoClient = ulong.MaxValue;

		// 手番番号を問わずに受け取るときに指定する値
		public const int AnySequence = -1;

		private readonly NetworkManager m_Manager;
		private readonly Queue<NetMessage> m_Inbox = new Queue<NetMessage>();
		private ulong m_RemoteClientId;
		private bool m_IsDisposed;

		public bool IsDisconnected { get; private set; }
		public bool HasPendingMessage => m_Inbox.Count > 0;

		//----------------------------------------------------
		// 起動済みの NetworkManager に受信処理を登録する（ゲストの相手は常にホスト）
		//----------------------------------------------------
		public NetworkMessenger(NetworkManager manager)
		{
			m_Manager = manager;
			m_RemoteClientId = manager.IsServer ? NoClient : NetworkManager.ServerClientId;
			m_Manager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessageReceived);
			m_Manager.OnClientDisconnectCallback += OnClientDisconnected;
			m_Manager.OnTransportFailure += OnTransportFailure;
		}

		//----------------------------------------------------
		// 受信処理の登録を解除する
		//----------------------------------------------------
		public void Dispose()
		{
			if (m_IsDisposed)
			{
				return;
			}
			m_IsDisposed = true;

			if (m_Manager == null)
			{
				return;
			}
			m_Manager.OnClientDisconnectCallback -= OnClientDisconnected;
			m_Manager.OnTransportFailure -= OnTransportFailure;
			m_Manager.CustomMessagingManager?.UnregisterNamedMessageHandler(MessageName);
		}

		//----------------------------------------------------
		// 相手の端末にメッセージを送る（切断後や、ホストで相手が未確定のときは送らない）
		//----------------------------------------------------
		public void Send(NetMessage message)
		{
			if (m_IsDisposed || IsDisconnected || m_Manager.CustomMessagingManager == null)
			{
				return;
			}
			if (m_RemoteClientId == NoClient)
			{
				Debug.LogWarning($"[NetworkMessenger] 相手が未確定のため {message} を送れません");
				return;
			}

			using (FastBufferWriter writer = new FastBufferWriter(MessageSize, Allocator.Temp))
			{
				writer.WriteValueSafe((byte)message.Type);
				writer.WriteValueSafe(message.Sequence);
				writer.WriteValueSafe(message.Arg0);
				writer.WriteValueSafe(message.Arg1);
				writer.WriteValueSafe(message.Arg2);
				m_Manager.CustomMessagingManager.SendNamedMessage(MessageName, m_RemoteClientId, writer, NetworkDelivery.ReliableSequenced);
			}
		}

		//----------------------------------------------------
		// 指定の種類（と手番番号）のメッセージが届くまで待つ
		// 途中に届いた別のメッセージは、時間切れ後に遅れて届いた古いものとして捨てる
		// timeout 秒を過ぎたら null を返す（無限に待つなら float.PositiveInfinity）
		//----------------------------------------------------
		public async Awaitable<NetMessage?> ReceiveAsync(ENetMessageType type, int sequence, float timeout, CancellationToken token)
		{
			float deadline = Time.unscaledTime + timeout;
			while (true)
			{
				while (m_Inbox.Count > 0)
				{
					NetMessage message = m_Inbox.Dequeue();
					if (message.Type == type && (sequence == AnySequence || message.Sequence == sequence))
					{
						return message;
					}
					Debug.Log($"[NetworkMessenger] 古いメッセージを捨てました: {message}");
				}

				ThrowIfDisconnected();
				if (Time.unscaledTime >= deadline)
				{
					return null;
				}
				await Awaitable.NextFrameAsync(token);
			}
		}

		//----------------------------------------------------
		// 種類を問わず次のメッセージが届くまで待つ
		//----------------------------------------------------
		public async Awaitable<NetMessage> ReceiveNextAsync(CancellationToken token)
		{
			while (m_Inbox.Count == 0)
			{
				ThrowIfDisconnected();
				await Awaitable.NextFrameAsync(token);
			}
			return m_Inbox.Dequeue();
		}

		//----------------------------------------------------
		// 切断済みなら例外を投げる
		//----------------------------------------------------
		public void ThrowIfDisconnected()
		{
			if (IsDisconnected)
			{
				throw new NetworkDisconnectedException();
			}
		}

		//----------------------------------------------------
		// 届いたメッセージを読み取って溜める（ホストは最初に届いた相手を対戦相手として扱う）
		//----------------------------------------------------
		private void OnMessageReceived(ulong senderClientId, FastBufferReader reader)
		{
			if (m_Manager.IsServer)
			{
				if (m_RemoteClientId == NoClient)
				{
					m_RemoteClientId = senderClientId;
				}
				else if (senderClientId != m_RemoteClientId)
				{
					return;
				}
			}

			reader.ReadValueSafe(out byte type);
			reader.ReadValueSafe(out int sequence);
			reader.ReadValueSafe(out int arg0);
			reader.ReadValueSafe(out int arg1);
			reader.ReadValueSafe(out int arg2);
			m_Inbox.Enqueue(new NetMessage((ENetMessageType)type, sequence, arg0, arg1, arg2));
		}

		//----------------------------------------------------
		// 相手が抜けたら切断として扱う（ゲストでは自分の切断として呼ばれる）
		//----------------------------------------------------
		private void OnClientDisconnected(ulong clientId)
		{
			if (!m_Manager.IsServer || clientId == m_RemoteClientId)
			{
				IsDisconnected = true;
			}
		}

		//----------------------------------------------------
		// 通信そのものが失敗したら切断として扱う
		//----------------------------------------------------
		private void OnTransportFailure()
		{
			IsDisconnected = true;
		}
	}
}
