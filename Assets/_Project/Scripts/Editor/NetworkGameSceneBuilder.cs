//====================================================
// NetworkGameSceneBuilder.cs
// ネットワーク対戦シーンをエディタ API で自動生成するメニュー
// 盤面と UI はゲームシーンと同じものを使い、受付画面と NetworkGameManager を加える
// NetworkManager はシーンに置かず、実行時に NetworkSessionService が作る
//====================================================
using System.IO;
using RESlimeConect.Game;
using RESlimeConect.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static RESlimeConect.EditorTools.SceneBuilderUtility;

namespace RESlimeConect.EditorTools
{
	public static class NetworkGameSceneBuilder
	{
		// 出力先
		private const string ScenePath = SceneDirectory + "/" + SceneLoader.NetworkGame + ".unity";

		// 素材
		private const string PanelFramePath = SpriteImportSetup.SpriteRoot + "/UI/Frame/Player.png";

		// 受付画面のレイアウト（UI のピクセル、画面中央が原点）
		private const int LobbySortingOrder = 10;
		private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
		private static readonly Vector2 PanelSize = new Vector2(640f, 512f);
		private static readonly Vector2 WideButtonSize = new Vector2(360f, 64f);
		private static readonly Vector2 SmallButtonSize = new Vector2(220f, 56f);

		//----------------------------------------------------
		// メニューからネットワーク対戦シーンを生成して保存する
		//----------------------------------------------------
		[MenuItem("Tools/RESlimeConect/ネットワーク対戦シーンを生成")]
		public static void BuildNetworkGameScene()
		{
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}
			if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("ネットワーク対戦シーンを生成", $"{ScenePath} は既に存在します。上書きしますか？", "上書き", "キャンセル"))
			{
				return;
			}

			// 新規シーン作成時に未使用アセットが解放されるため、素材の読み込みはシーンを作った後に行う
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			SpriteImportSetup.ApplyAll();
			BoardSkin skin = GameSceneBuilder.LoadOrCreateSkin();
			GameObject cellPrefab = GameSceneBuilder.LoadOrCreateCellPrefab();

			Camera camera = CreateCamera();
			BoardView boardView = GameSceneBuilder.CreateBoard(skin, cellPrefab, camera);
			GameUIView gameUIView = GameSceneBuilder.CreateUI(true);
			NetworkLobbyView lobbyView = CreateLobby();
			CreateEventSystem();
			CreateManager(boardView, gameUIView, lobbyView);

			Directory.CreateDirectory(SceneDirectory);
			EditorSceneManager.SaveScene(scene, ScenePath);
			AddSceneToBuildSettings(ScenePath, false);
			Debug.Log($"[NetworkGameSceneBuilder] {ScenePath} を生成しました");
		}

		//----------------------------------------------------
		// 受付画面（メニューと待機中の表示）をゲーム UI より手前の Canvas に作る
		//----------------------------------------------------
		private static NetworkLobbyView CreateLobby()
		{
			Canvas canvas = CreateCanvas();
			canvas.name = "LobbyCanvas";
			canvas.sortingOrder = LobbySortingOrder;
			Transform canvasRoot = canvas.transform;

			// 盤面を暗くして操作できないようにする
			GameObject root = CreateUIObject("Lobby", canvasRoot);
			StretchFull(root.GetComponent<RectTransform>());
			Image dim = root.AddComponent<Image>();
			dim.color = DimColor;

			Image panel = CreateImage("Panel", root.transform, LoadSprite(PanelFramePath), Vector2.zero, PanelSize);
			panel.preserveAspect = false;
			Text title = CreateText("Title", panel.transform, "ネットワーク対戦", 40);
			SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(560f, 56f));
			AddOutline(title);

			// メニュー
			GameObject menuRoot = CreateUIObject("Menu", panel.transform);
			StretchFull(menuRoot.GetComponent<RectTransform>());
			Text errorText = CreateText("ErrorText", menuRoot.transform, string.Empty, 18);
			SetRect(errorText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 145f), new Vector2(560f, 48f));
			errorText.color = new Color(1f, 0.75f, 0.7f);
			AddOutline(errorText);
			Button createRoomButton = CreateTextButton("CreateRoomButton", menuRoot.transform, "部屋を作る", new Vector2(0f, 80f), WideButtonSize, 28);
			Button quickMatchButton = CreateTextButton("QuickMatchButton", menuRoot.transform, "ランダムマッチ", new Vector2(0f, 0f), WideButtonSize, 28);
			InputField codeInput = CreateInputField("CodeInput", menuRoot.transform, "ルームコード", new Vector2(-70f, -85f), new Vector2(220f, 60f), 26);
			codeInput.characterLimit = 12;
			codeInput.contentType = InputField.ContentType.Alphanumeric;
			Button joinButton = CreateTextButton("JoinButton", menuRoot.transform, "参加", new Vector2(130f, -85f), new Vector2(140f, 60f), 28);
			Button backButton = CreateTextButton("BackButton", menuRoot.transform, "戻る", new Vector2(0f, -180f), SmallButtonSize, 26);

			// 待機中
			GameObject waitingRoot = CreateUIObject("Waiting", panel.transform);
			StretchFull(waitingRoot.GetComponent<RectTransform>());
			Text waitingText = CreateText("Message", waitingRoot.transform, string.Empty, 26);
			SetRect(waitingText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), new Vector2(560f, 110f));
			AddOutline(waitingText);
			Text codeText = CreateText("Code", waitingRoot.transform, string.Empty, 56);
			SetRect(codeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(560f, 72f));
			codeText.color = new Color(1f, 0.95f, 0.6f);
			AddOutline(codeText);
			Button copyCodeButton = CreateTextButton("CopyCodeButton", waitingRoot.transform, "コードをコピー", new Vector2(0f, -85f), SmallButtonSize, 22);
			Button cancelButton = CreateTextButton("CancelButton", waitingRoot.transform, "キャンセル", new Vector2(0f, -180f), SmallButtonSize, 26);
			waitingRoot.SetActive(false);

			NetworkLobbyView lobbyView = canvas.gameObject.AddComponent<NetworkLobbyView>();
			SetReference(lobbyView, "m_Root", root);
			SetReference(lobbyView, "m_MenuRoot", menuRoot);
			SetReference(lobbyView, "m_ErrorText", errorText);
			SetReference(lobbyView, "m_CreateRoomButton", createRoomButton);
			SetReference(lobbyView, "m_QuickMatchButton", quickMatchButton);
			SetReference(lobbyView, "m_CodeInput", codeInput);
			SetReference(lobbyView, "m_JoinButton", joinButton);
			SetReference(lobbyView, "m_BackButton", backButton);
			SetReference(lobbyView, "m_WaitingRoot", waitingRoot);
			SetReference(lobbyView, "m_WaitingText", waitingText);
			SetReference(lobbyView, "m_CodeText", codeText);
			SetReference(lobbyView, "m_CopyCodeButton", copyCodeButton);
			SetReference(lobbyView, "m_CancelButton", cancelButton);
			return lobbyView;
		}

		//----------------------------------------------------
		// NetworkGameManager を作り盤面・UI・受付画面と結線する
		//----------------------------------------------------
		private static void CreateManager(BoardView boardView, GameUIView gameUIView, NetworkLobbyView lobbyView)
		{
			GameObject managerObject = new GameObject("NetworkGameManager");
			NetworkGameManager manager = managerObject.AddComponent<NetworkGameManager>();
			SetReference(manager, "m_BoardView", boardView);
			SetReference(manager, "m_GameUIView", gameUIView);
			SetReference(manager, "m_LobbyView", lobbyView);
		}
	}
}
