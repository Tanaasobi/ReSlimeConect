//====================================================
// NetworkSessionService.cs
// Unity Multiplayer Services（Sessions + Relay）で部屋を作る・コードで参加する・ランダムマッチする処理と、
// 通信に使う NetworkManager の生成・破棄をまとめたもの
// 使うには Unity Cloud にプロジェクトを紐付ける必要がある（Edit > Project Settings > Services）
//====================================================
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RESlimeConect.Network
{
	public sealed class NetworkSessionService
	{
		private const int MaxPlayers = 2;
		private const float QuickJoinTimeoutSeconds = 5f;

		// 同じ PC で複数起動して試せるよう、起動ごとに別のプレイヤーとしてログインする
		private static readonly string ProfileName = "p" + Guid.NewGuid().ToString("N").Substring(0, 16);

		private ISession m_Session;

		public bool IsInSession => m_Session != null;
		public bool IsHost => m_Session != null && m_Session.IsHost;
		public string SessionCode => m_Session?.Code;
		public NetworkManager Manager => NetworkManager.Singleton;

		//----------------------------------------------------
		// 非公開の部屋を作る（相手はルームコードで参加する）
		//----------------------------------------------------
		public async Task CreateRoomAsync()
		{
			await PrepareAsync();
			SessionOptions options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }.WithRelayNetwork();
			m_Session = await MultiplayerService.Instance.CreateSessionAsync(options);
		}

		//----------------------------------------------------
		// ルームコードで部屋に参加する
		//----------------------------------------------------
		public async Task JoinRoomAsync(string code)
		{
			await PrepareAsync();
			m_Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
		}

		//----------------------------------------------------
		// 空いている公開の部屋に入る（見つからなければ自分で部屋を作って待つ）
		//----------------------------------------------------
		public async Task QuickMatchAsync()
		{
			await PrepareAsync();
			QuickJoinOptions quickJoinOptions = new QuickJoinOptions
			{
				Timeout = TimeSpan.FromSeconds(QuickJoinTimeoutSeconds),
				CreateSession = true,
			};
			SessionOptions options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
			m_Session = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoinOptions, options);
		}

		//----------------------------------------------------
		// 部屋から抜けて通信を止め、NetworkManager を破棄する（部屋に入っていなくても呼べる）
		//----------------------------------------------------
		public async Task LeaveAsync()
		{
			ISession session = m_Session;
			m_Session = null;
			if (session != null)
			{
				try
				{
					if (session.IsHost)
					{
						await session.AsHost().DeleteAsync();
					}
					else
					{
						await session.LeaveAsync();
					}
				}
				catch (Exception exception)
				{
					// 既に部屋が消えている場合などは、退出できたものとして扱う
					Debug.LogWarning($"[NetworkSessionService] 部屋から抜ける処理に失敗しました: {exception.Message}");
				}
			}
			DestroyNetworkManager();
		}

		//----------------------------------------------------
		// サービスの初期化・ログインと、NetworkManager の用意を行う
		//----------------------------------------------------
		private static async Task PrepareAsync()
		{
			if (UnityServices.State == ServicesInitializationState.Uninitialized)
			{
				await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(ProfileName));
			}
			if (!AuthenticationService.Instance.IsSignedIn)
			{
				await AuthenticationService.Instance.SignInAnonymouslyAsync();
			}
			EnsureNetworkManager();
		}

		//----------------------------------------------------
		// NetworkManager が無ければ作る（シーンをまたいで残るため、シーンには置かずにここで作る）
		//----------------------------------------------------
		private static void EnsureNetworkManager()
		{
			if (NetworkManager.Singleton != null)
			{
				return;
			}

			GameObject managerObject = new GameObject("NetworkManager");
			UnityTransport transport = managerObject.AddComponent<UnityTransport>();
			NetworkManager manager = managerObject.AddComponent<NetworkManager>();
			manager.NetworkConfig = new NetworkConfig
			{
				NetworkTransport = transport,
				// シーンの同期は使わず、メッセージだけでやり取りする
				EnableSceneManagement = false,
			};
		}

		//----------------------------------------------------
		// 通信を止めて NetworkManager を破棄する
		//----------------------------------------------------
		private static void DestroyNetworkManager()
		{
			NetworkManager manager = NetworkManager.Singleton;
			if (manager == null)
			{
				return;
			}
			if (manager.IsListening)
			{
				manager.Shutdown();
			}
			Object.Destroy(manager.gameObject);
		}
	}
}
