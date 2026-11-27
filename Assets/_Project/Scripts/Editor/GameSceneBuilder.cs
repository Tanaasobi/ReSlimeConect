//====================================================
// GameSceneBuilder.cs
// ゲームシーン・マスの Prefab・スキンをエディタ API で自動生成するメニュー
// シーンの YAML を手で書かずに、オブジェクト構成と参照の結線を再現するためのもの
// シーンは毎回作り直す。スキンは空欄だけを埋め、Prefab は構成が古いときだけ作り直す
// 画面は 1280x720・1px = 0.01 ユニット（Pixels Per Unit 100）を基準に配置する
//====================================================
using System.IO;
using System.Linq;
using RESlimeConect.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RESlimeConect.EditorTools
{
	public static class GameSceneBuilder
	{
		// 出力先
		private const string SceneDirectory = "Assets/_Project/Scenes";
		private const string ScenePath = SceneDirectory + "/GameScene.unity";
		private const string SkinDirectory = "Assets/_Project/Skins";
		private const string SkinPath = SkinDirectory + "/DefaultBoardSkin.asset";
		private const string PrefabDirectory = "Assets/_Project/Prefabs";
		private const string CellPrefabPath = PrefabDirectory + "/Cell.prefab";

		// 素材
		private const string SpriteRoot = SpriteImportSetup.SpriteRoot;
		private const string BackgroundPath = SpriteRoot + "/BG/BG.png";
		private const string TilePath = SpriteRoot + "/Tile/Tile.png";
		private const string CircleSlimeDirectory = SpriteRoot + "/Placeholder/Slime/O";
		private const string CrossSlimeDirectory = SpriteRoot + "/Placeholder/Slime/X";
		private const string TrapIdlePath = SpriteRoot + "/Placeholder/Trap/Trap_Idle.png";
		private const string TrapPlacePath = SpriteRoot + "/Placeholder/Trap/Trap_Play.png";
		private const string ExclamationPath = SpriteRoot + "/Effect/Exclamation.png";
		private const string NumberPath = SpriteRoot + "/UI/Text/Number.png";
		private const string PlayerFramePath = SpriteRoot + "/UI/Frame/Player.png";
		private const string TimeFramePath = SpriteRoot + "/UI/Frame/Time.png";
		private const string ButtonDirectory = SpriteRoot + "/UI/Button";

		// 画面・盤面のレイアウト（ワールド座標はユニット、UI はピクセル）
		private const float CameraSize = 3.6f;
		private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
		private static readonly Vector3 BoardPosition = new Vector3(-2.8f, 0f, 0f);
		private static readonly Vector2 GridOffset = new Vector2(0.02f, 0.067f);
		private const float CellSpacing = 1.745f;
		private const float CellColliderSize = 1.54f;
		private const float RightColumnX = 320f;

		// マス内の各レイヤー（位置・大きさ・描画順）
		private static readonly Vector3 TrapLocalPosition = new Vector3(0f, 0.1f, 0f);
		private const float TrapScale = 2.5f;
		private static readonly Vector3 SlimeLocalPosition = new Vector3(0f, 0.12f, 0f);
		private const float SlimeScale = 0.7f;
		// スライムの Idle シートは 1 段 7 コマで、上から 置いたとき・通常・驚き
		private const int SlimeSheetRowLength = 7;
		private static readonly Vector3 ExclamationLocalPosition = new Vector3(0f, 0.75f, 0f);
		private const float ExclamationScale = 0.7f;
		private const int BackgroundOrder = -100;
		private const int BoardOrder = 0;
		private const int TrapOrder = 10;
		private const int PreviewOrder = 15;
		private const int SlimeOrder = 20;
		private const int ExclamationOrder = 30;

		//----------------------------------------------------
		// メニューからゲームシーンを生成して保存する
		//----------------------------------------------------
		[MenuItem("Tools/RESlimeConect/ゲームシーンを生成")]
		public static void BuildGameScene()
		{
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}
			if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("ゲームシーンを生成", $"{ScenePath} は既に存在します。上書きしますか？", "上書き", "キャンセル"))
			{
				return;
			}

			// 新規シーン作成時に未使用アセットが解放されるため、素材の読み込みはシーンを作った後に行う
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			SpriteImportSetup.ApplyAll();
			BoardSkin skin = LoadOrCreateSkin();
			GameObject cellPrefab = LoadOrCreateCellPrefab();

			Camera camera = CreateCamera();
			BoardView boardView = CreateBoard(skin, cellPrefab, camera);
			GameUIView gameUIView = CreateUI();
			CreateEventSystem();
			CreateGameManager(boardView, gameUIView);

			Directory.CreateDirectory(SceneDirectory);
			EditorSceneManager.SaveScene(scene, ScenePath);
			AddSceneToBuildSettings(ScenePath);
			Debug.Log($"[GameSceneBuilder] {ScenePath} を生成しました");
		}

		//====================================================
		// スキン
		//====================================================

		//----------------------------------------------------
		// スキンアセットを読み込み（無ければ作り）、空欄だけ既定の素材で埋める
		//----------------------------------------------------
		private static BoardSkin LoadOrCreateSkin()
		{
			BoardSkin skin = AssetDatabase.LoadAssetAtPath<BoardSkin>(SkinPath);
			if (skin == null)
			{
				Directory.CreateDirectory(SkinDirectory);
				skin = ScriptableObject.CreateInstance<BoardSkin>();
				AssetDatabase.CreateAsset(skin, SkinPath);
			}

			SerializedObject serialized = new SerializedObject(skin);
			AssignSpriteIfEmpty(serialized, "m_BoardSprite", LoadSprite(TilePath));
			AssignSpriteIfEmpty(serialized, "m_BackgroundSprite", LoadSprite(BackgroundPath));
			AssignSlimeIfEmpty(serialized, "m_CircleSlime", CircleSlimeDirectory);
			AssignSlimeIfEmpty(serialized, "m_CrossSlime", CrossSlimeDirectory);
			AssignClipIfEmpty(serialized, "m_TrapIdle", SpriteImportSetup.LoadSheet(TrapIdlePath), 10f, true);
			AssignClipIfEmpty(serialized, "m_TrapPlace", SpriteImportSetup.LoadSheet(TrapPlacePath), 15f, false);
			AssignClipIfEmpty(serialized, "m_Exclamation", SpriteImportSetup.LoadSheet(ExclamationPath), 20f, false);
			serialized.ApplyModifiedPropertiesWithoutUndo();

			EditorUtility.SetDirty(skin);
			AssetDatabase.SaveAssets();
			return skin;
		}

		//----------------------------------------------------
		// メニューからスキンの空欄だけを埋める（シーンは作り直さない）
		//----------------------------------------------------
		[MenuItem("Tools/RESlimeConect/スキンの空欄を埋める")]
		public static void FillSkin()
		{
			BoardSkin skin = LoadOrCreateSkin();
			Selection.activeObject = skin;
			Debug.Log($"[GameSceneBuilder] {SkinPath} の空欄を埋めました", skin);
		}

		//----------------------------------------------------
		// スライム1色分のアニメーションを、空欄のものだけ割り当てる
		// Idle シートは 1 段目が置いたとき、2 段目が通常状態のループ、3 段目が驚き
		//----------------------------------------------------
		private static void AssignSlimeIfEmpty(SerializedObject serialized, string path, string directory)
		{
			Sprite[] idleSheet = SpriteImportSetup.LoadSheet(directory + "/Idle.png");
			Sprite[] placeFrames = idleSheet.Take(SlimeSheetRowLength).ToArray();
			Sprite[] idleFrames = idleSheet.Skip(SlimeSheetRowLength).Take(SlimeSheetRowLength).ToArray();
			Sprite[] surpriseFrames = idleSheet.Skip(SlimeSheetRowLength * 2).ToArray();

			// 以前の既定（シート全コマ、または 2〜3 段目）のままなら、再生速度などは残してコマだけ 2 段目に差し替える
			SerializedProperty idleProperty = serialized.FindProperty(path + ".m_Idle.m_Frames");
			if (IsSameFrames(idleProperty, idleSheet) || IsSameFrames(idleProperty, idleSheet.Skip(SlimeSheetRowLength).ToArray()))
			{
				SetFrames(idleProperty, idleFrames);
			}

			AssignClipIfEmpty(serialized, path + ".m_Place", placeFrames, 12f, false);
			AssignClipIfEmpty(serialized, path + ".m_Idle", idleFrames, 12f, true);
			AssignClipIfEmpty(serialized, path + ".m_Surprise", surpriseFrames, 12f, false);
			AssignClipIfEmpty(serialized, path + ".m_Win", SpriteImportSetup.LoadSheet(directory + "/Win.png"), 12f, true);
			AssignClipIfEmpty(serialized, path + ".m_Change", SpriteImportSetup.LoadSheet(directory + "/Change.png"), 20f, false);
		}

		//----------------------------------------------------
		// コマ配列を指定のスプライト列で置き換える
		//----------------------------------------------------
		private static void SetFrames(SerializedProperty framesProperty, Sprite[] sprites)
		{
			framesProperty.arraySize = sprites.Length;
			for (int i = 0; i < sprites.Length; i++)
			{
				framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
			}
		}

		//----------------------------------------------------
		// コマ配列の中身が指定のスプライト列と完全に一致するかを返す
		//----------------------------------------------------
		private static bool IsSameFrames(SerializedProperty framesProperty, Sprite[] sprites)
		{
			if (framesProperty.arraySize != sprites.Length)
			{
				return false;
			}
			for (int i = 0; i < sprites.Length; i++)
			{
				if (framesProperty.GetArrayElementAtIndex(i).objectReferenceValue != sprites[i])
				{
					return false;
				}
			}
			return true;
		}

		//----------------------------------------------------
		// アニメーションのコマが空なら、コマ・再生速度・ループを設定する
		//----------------------------------------------------
		private static void AssignClipIfEmpty(SerializedObject serialized, string path, Sprite[] frames, float frameRate, bool loop)
		{
			SerializedProperty framesProperty = serialized.FindProperty(path + ".m_Frames");
			if (framesProperty.arraySize > 0 || frames.Length == 0)
			{
				return;
			}

			SetFrames(framesProperty, frames);
			serialized.FindProperty(path + ".m_FrameRate").floatValue = frameRate;
			serialized.FindProperty(path + ".m_Loop").boolValue = loop;
		}

		//----------------------------------------------------
		// 画像の参照が空なら割り当てる
		//----------------------------------------------------
		private static void AssignSpriteIfEmpty(SerializedObject serialized, string path, Sprite sprite)
		{
			SerializedProperty property = serialized.FindProperty(path);
			if (property.objectReferenceValue == null)
			{
				property.objectReferenceValue = sprite;
			}
		}

		//----------------------------------------------------
		// 1枚画像のスプライトを読み込む
		//----------------------------------------------------
		private static Sprite LoadSprite(string path)
		{
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
			if (sprite == null)
			{
				Debug.LogWarning($"[GameSceneBuilder] スプライトが見つかりません: {path}");
			}
			return sprite;
		}

		//====================================================
		// マスの Prefab
		//====================================================

		//----------------------------------------------------
		// マスの Prefab を読み込む（無い、または構成が古い場合は作り直す）
		//----------------------------------------------------
		private static GameObject LoadOrCreateCellPrefab()
		{
			GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
			if (existing != null && IsCurrentCellPrefab(existing))
			{
				return existing;
			}

			// 構成：本体（当たり判定・CellView）の子に トラップ・プレビュー・スライム・ビックリマーク
			GameObject cellObject = new GameObject("Cell");
			BoxCollider2D collider = cellObject.AddComponent<BoxCollider2D>();
			collider.size = Vector2.one * CellColliderSize;

			SpriteAnimator trap = CreateAnimatedSprite("Trap", cellObject.transform, TrapLocalPosition, TrapScale, TrapOrder);
			SpriteRenderer preview = CreateSprite("Preview", cellObject.transform, null, Vector3.zero, Vector3.one, PreviewOrder);
			SpriteAnimator slime = CreateAnimatedSprite("Slime", cellObject.transform, SlimeLocalPosition, SlimeScale, SlimeOrder);
			SpriteAnimator exclamation = CreateAnimatedSprite("Exclamation", cellObject.transform, ExclamationLocalPosition, ExclamationScale, ExclamationOrder);
			preview.enabled = false;

			CellView cellView = cellObject.AddComponent<CellView>();
			SerializedObject serialized = new SerializedObject(cellView);
			serialized.FindProperty("m_TrapAnimator").objectReferenceValue = trap;
			serialized.FindProperty("m_SlimeAnimator").objectReferenceValue = slime;
			serialized.FindProperty("m_ExclamationAnimator").objectReferenceValue = exclamation;
			serialized.FindProperty("m_PreviewRenderer").objectReferenceValue = preview;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			Directory.CreateDirectory(PrefabDirectory);
			GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cellObject, CellPrefabPath);
			Object.DestroyImmediate(cellObject);
			return prefab;
		}

		//----------------------------------------------------
		// Prefab が現在の CellView の構成になっているかを返す
		//----------------------------------------------------
		private static bool IsCurrentCellPrefab(GameObject prefab)
		{
			CellView cellView = prefab.GetComponent<CellView>();
			if (cellView == null)
			{
				return false;
			}
			SerializedObject serialized = new SerializedObject(cellView);
			SerializedProperty slime = serialized.FindProperty("m_SlimeAnimator");
			return slime != null && slime.objectReferenceValue != null;
		}

		//----------------------------------------------------
		// SpriteAnimator 付きのスプライトオブジェクトを作る
		//----------------------------------------------------
		private static SpriteAnimator CreateAnimatedSprite(string name, Transform parent, Vector3 localPosition, float scale, int sortingOrder)
		{
			SpriteRenderer renderer = CreateSprite(name, parent, null, localPosition, Vector3.one * scale, sortingOrder);
			renderer.enabled = false;
			SpriteAnimator animator = renderer.gameObject.AddComponent<SpriteAnimator>();
			SerializedObject serialized = new SerializedObject(animator);
			serialized.FindProperty("m_SpriteRenderer").objectReferenceValue = renderer;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return animator;
		}

		//----------------------------------------------------
		// SpriteRenderer 付きのオブジェクトを作る
		//----------------------------------------------------
		private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Vector3 localPosition, Vector3 localScale, int sortingOrder)
		{
			GameObject spriteObject = new GameObject(name);
			if (parent != null)
			{
				spriteObject.transform.SetParent(parent, false);
			}
			spriteObject.transform.localPosition = localPosition;
			spriteObject.transform.localScale = localScale;

			SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
			renderer.sprite = sprite;
			renderer.sortingOrder = sortingOrder;
			return renderer;
		}

		//====================================================
		// シーン（カメラ・盤面）
		//====================================================

		//----------------------------------------------------
		// 1280x720 で 1px = 0.01 ユニットになる正投影カメラを作る
		//----------------------------------------------------
		private static Camera CreateCamera()
		{
			GameObject cameraObject = new GameObject("Main Camera");
			cameraObject.tag = "MainCamera";
			cameraObject.transform.position = new Vector3(0f, 0f, -10f);

			Camera camera = cameraObject.AddComponent<Camera>();
			camera.orthographic = true;
			camera.orthographicSize = CameraSize;
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = Color.black;
			return camera;
		}

		//----------------------------------------------------
		// 背景・盤面タイル・マスの Prefab インスタンス 9 個を作り BoardView に結線する
		//----------------------------------------------------
		private static BoardView CreateBoard(BoardSkin skin, GameObject cellPrefab, Camera camera)
		{
			SpriteRenderer background = CreateSprite("Background", null, skin.BackgroundSprite, Vector3.zero, Vector3.one, BackgroundOrder);

			GameObject boardObject = new GameObject("Board");
			boardObject.transform.position = BoardPosition;
			BoardView boardView = boardObject.AddComponent<BoardView>();
			SpriteRenderer tile = CreateSprite("Tile", boardObject.transform, skin.BoardSprite, Vector3.zero, Vector3.one, BoardOrder);

			CellView[] cells = new CellView[BoardModel.CellCount];
			for (int i = 0; i < BoardModel.CellCount; i++)
			{
				int column = i % BoardModel.Size;
				int row = i / BoardModel.Size;
				GameObject cellObject = (GameObject)PrefabUtility.InstantiatePrefab(cellPrefab, boardObject.transform);
				cellObject.name = $"Cell_{i}";
				cellObject.transform.localPosition = new Vector3(GridOffset.x + (column - 1) * CellSpacing, GridOffset.y + (1 - row) * CellSpacing, 0f);

				cells[i] = cellObject.GetComponent<CellView>();
				SerializedObject cellSerialized = new SerializedObject(cells[i]);
				cellSerialized.FindProperty("m_Index").intValue = i;
				cellSerialized.ApplyModifiedPropertiesWithoutUndo();
			}

			SerializedObject serialized = new SerializedObject(boardView);
			serialized.FindProperty("m_Camera").objectReferenceValue = camera;
			serialized.FindProperty("m_Skin").objectReferenceValue = skin;
			serialized.FindProperty("m_BoardRenderer").objectReferenceValue = tile;
			serialized.FindProperty("m_BackgroundRenderer").objectReferenceValue = background;
			SerializedProperty cellsProperty = serialized.FindProperty("m_Cells");
			cellsProperty.arraySize = cells.Length;
			for (int i = 0; i < cells.Length; i++)
			{
				cellsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
			}
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return boardView;
		}

		//====================================================
		// UI
		//====================================================

		//----------------------------------------------------
		// Canvas と各 UI パーツを作り GameUIView に結線する（右列に縦並び）
		//----------------------------------------------------
		private static GameUIView CreateUI()
		{
			GameObject canvasObject = new GameObject("Canvas");
			Canvas canvas = canvasObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = ReferenceResolution;
			scaler.matchWidthOrHeight = 0.5f;
			canvasObject.AddComponent<GraphicRaycaster>();
			Transform root = canvasObject.transform;

			// 手番プレイヤー枠（中にスライム）
			Image playerFrame = CreateImage("PlayerFrame", root, LoadSprite(PlayerFramePath), new Vector2(RightColumnX, 210f), new Vector2(300f, 240f));
			Image playerSlimeImage = CreateImage("Slime", playerFrame.transform, null, new Vector2(0f, -10f), new Vector2(190f, 190f));
			playerSlimeImage.enabled = false;
			SpriteAnimator playerSlimeAnimator = playerSlimeImage.gameObject.AddComponent<SpriteAnimator>();
			SetReference(playerSlimeAnimator, "m_Image", playerSlimeImage);

			// 残り時間枠
			Image timeFrame = CreateImage("TimeFrame", root, LoadSprite(TimeFramePath), new Vector2(RightColumnX, -5f), new Vector2(320f, 128f));
			GameObject timeTextObject = CreateUIObject("TimeText", timeFrame.transform);
			SetRect(timeTextObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 100f));
			SpriteNumberText timeText = timeTextObject.AddComponent<SpriteNumberText>();
			SetSpriteArray(timeText, "m_Sprites", SpriteImportSetup.LoadSheet(NumberPath));

			// トラップ設置ボタン（右下に縦並び）
			GameObject trapControlRoot = CreateUIObject("TrapControls", root);
			StretchFull(trapControlRoot.GetComponent<RectTransform>());
			Button trapConfirmButton = CreateButton("DecisionButton", trapControlRoot.transform, "Decision_Button", "Decision_Float", "Decision_Push", new Vector2(RightColumnX, -145f), new Vector2(240f, 96f));
			Button trapSkipButton = CreateButton("NotInstallButton", trapControlRoot.transform, "NotInstall_Idle", "NotInstal_Float", "NotInstall_Push", new Vector2(RightColumnX, -255f), new Vector2(288f, 96f));

			// リザルト（勝者表示とボタン3つ）
			GameObject resultRoot = CreateUIObject("Result", root);
			StretchFull(resultRoot.GetComponent<RectTransform>());
			Text resultText = CreateText("ResultText", resultRoot.transform, string.Empty, 40);
			SetRect(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(RightColumnX, 55f), new Vector2(420f, 60f));
			Outline outline = resultText.gameObject.AddComponent<Outline>();
			outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
			outline.effectDistance = new Vector2(2f, -2f);
			Button modeSelectButton = CreateButton("ModeSelectButton", resultRoot.transform, "ModeSelect_Idle", "ModeSelectFloat", "ModeSelectPush", new Vector2(RightColumnX, -40f), new Vector2(288f, 96f));
			Button rematchButton = CreateButton("RematchButton", resultRoot.transform, "Rematch_Idle", "Rematch_Float", "Rematch_Push", new Vector2(RightColumnX, -145f), new Vector2(288f, 96f));
			Button gameEndButton = CreateButton("GameEndButton", resultRoot.transform, "GameEnd_Idle", "GameEnd_Float", "GameEnd_Push", new Vector2(RightColumnX, -250f), new Vector2(288f, 96f));

			// 交代画面（盤面を完全に隠す。最前面に出すため最後に作る）
			GameObject handoffPanel = CreateUIObject("HandoffPanel", root);
			StretchFull(handoffPanel.GetComponent<RectTransform>());
			Image handoffBackground = handoffPanel.AddComponent<Image>();
			handoffBackground.color = new Color(0.05f, 0.08f, 0.05f, 1f);
			Text handoffText = CreateText("HandoffText", handoffPanel.transform, string.Empty, 40);
			SetRect(handoffText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1100f, 200f));
			Button handoffButton = CreateButton("OkButton", handoffPanel.transform, "Decision_Button", "Decision_Float", "Decision_Push", new Vector2(0f, -120f), new Vector2(240f, 96f));

			trapControlRoot.SetActive(false);
			timeFrame.gameObject.SetActive(false);
			resultRoot.SetActive(false);
			handoffPanel.SetActive(false);

			GameUIView gameUIView = canvasObject.AddComponent<GameUIView>();
			SerializedObject serialized = new SerializedObject(gameUIView);
			serialized.FindProperty("m_PlayerFrameRoot").objectReferenceValue = playerFrame.gameObject;
			serialized.FindProperty("m_PlayerSlimeAnimator").objectReferenceValue = playerSlimeAnimator;
			serialized.FindProperty("m_TimeFrameRoot").objectReferenceValue = timeFrame.gameObject;
			serialized.FindProperty("m_TimeText").objectReferenceValue = timeText;
			serialized.FindProperty("m_TrapControlRoot").objectReferenceValue = trapControlRoot;
			serialized.FindProperty("m_TrapConfirmButton").objectReferenceValue = trapConfirmButton;
			serialized.FindProperty("m_TrapSkipButton").objectReferenceValue = trapSkipButton;
			serialized.FindProperty("m_HandoffPanel").objectReferenceValue = handoffPanel;
			serialized.FindProperty("m_HandoffText").objectReferenceValue = handoffText;
			serialized.FindProperty("m_HandoffButton").objectReferenceValue = handoffButton;
			serialized.FindProperty("m_ResultRoot").objectReferenceValue = resultRoot;
			serialized.FindProperty("m_ResultText").objectReferenceValue = resultText;
			serialized.FindProperty("m_ModeSelectButton").objectReferenceValue = modeSelectButton;
			serialized.FindProperty("m_RematchButton").objectReferenceValue = rematchButton;
			serialized.FindProperty("m_GameEndButton").objectReferenceValue = gameEndButton;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return gameUIView;
		}

		//----------------------------------------------------
		// RectTransform 付きの空 UI オブジェクトを作る
		//----------------------------------------------------
		private static GameObject CreateUIObject(string name, Transform parent)
		{
			GameObject uiObject = new GameObject(name, typeof(RectTransform));
			uiObject.transform.SetParent(parent, false);
			return uiObject;
		}

		//----------------------------------------------------
		// 中央基準で配置した Image を作る
		//----------------------------------------------------
		private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
		{
			GameObject imageObject = CreateUIObject(name, parent);
			SetRect(imageObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), position, size);
			Image image = imageObject.AddComponent<Image>();
			image.sprite = sprite;
			image.preserveAspect = true;
			image.raycastTarget = false;
			return image;
		}

		//----------------------------------------------------
		// 組み込みフォントを使ったテキストを作る
		//----------------------------------------------------
		private static Text CreateText(string name, Transform parent, string content, int fontSize)
		{
			GameObject textObject = CreateUIObject(name, parent);
			Text text = textObject.AddComponent<Text>();
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			text.text = content;
			text.fontSize = fontSize;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = Color.white;
			text.raycastTarget = false;
			text.horizontalOverflow = HorizontalWrapMode.Wrap;
			text.verticalOverflow = VerticalWrapMode.Overflow;
			return text;
		}

		//----------------------------------------------------
		// 通常・カーソルを合わせた状態・押している状態の画像を切り替えるボタンを作る
		//----------------------------------------------------
		private static Button CreateButton(string name, Transform parent, string idleName, string floatName, string pushName, Vector2 position, Vector2 size)
		{
			Image image = CreateImage(name, parent, LoadSprite($"{ButtonDirectory}/{idleName}.png"), position, size);
			image.raycastTarget = true;

			Button button = image.gameObject.AddComponent<Button>();
			button.transition = Selectable.Transition.SpriteSwap;
			button.spriteState = new SpriteState
			{
				highlightedSprite = LoadSprite($"{ButtonDirectory}/{floatName}.png"),
				pressedSprite = LoadSprite($"{ButtonDirectory}/{pushName}.png"),
			};

			// クリック後に「選択中」の見た目が残らないよう、ナビゲーションは使わない
			button.navigation = new Navigation { mode = Navigation.Mode.None };
			return button;
		}

		//----------------------------------------------------
		// 指定アンカー基準で位置とサイズを設定する
		//----------------------------------------------------
		private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
		{
			rect.anchorMin = anchor;
			rect.anchorMax = anchor;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = position;
			rect.sizeDelta = size;
		}

		//----------------------------------------------------
		// 親いっぱいに広げる
		//----------------------------------------------------
		private static void StretchFull(RectTransform rect)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}

		//----------------------------------------------------
		// Input System 用の EventSystem を作る
		//----------------------------------------------------
		private static void CreateEventSystem()
		{
			GameObject eventSystemObject = new GameObject("EventSystem");
			eventSystemObject.AddComponent<EventSystem>();
			eventSystemObject.AddComponent<InputSystemUIInputModule>();
		}

		//----------------------------------------------------
		// GameManager を作り盤面と UI を結線する
		//----------------------------------------------------
		private static void CreateGameManager(BoardView boardView, GameUIView gameUIView)
		{
			GameObject managerObject = new GameObject("GameManager");
			GameManager manager = managerObject.AddComponent<GameManager>();
			SerializedObject serialized = new SerializedObject(manager);
			serialized.FindProperty("m_BoardView").objectReferenceValue = boardView;
			serialized.FindProperty("m_GameUIView").objectReferenceValue = gameUIView;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		//====================================================
		// 共通処理
		//====================================================

		//----------------------------------------------------
		// コンポーネントのオブジェクト参照フィールドを設定する
		//----------------------------------------------------
		private static void SetReference(Object target, string propertyName, Object value)
		{
			SerializedObject serialized = new SerializedObject(target);
			serialized.FindProperty(propertyName).objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		//----------------------------------------------------
		// コンポーネントのスプライト配列フィールドを設定する
		//----------------------------------------------------
		private static void SetSpriteArray(Object target, string propertyName, Sprite[] sprites)
		{
			SerializedObject serialized = new SerializedObject(target);
			SerializedProperty property = serialized.FindProperty(propertyName);
			property.arraySize = sprites.Length;
			for (int i = 0; i < sprites.Length; i++)
			{
				property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
			}
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		//----------------------------------------------------
		// シーンをビルド設定の先頭に登録する
		//----------------------------------------------------
		private static void AddSceneToBuildSettings(string path)
		{
			EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
			if (scenes.Any(scene => scene.path == path))
			{
				return;
			}
			EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) }.Concat(scenes).ToArray();
		}
	}
}
