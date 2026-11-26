//====================================================
// GameSceneBuilder.cs
// ゲームシーンとプレースホルダー画像をエディタ API で自動生成するメニュー
// シーンの YAML を手で書かずに、オブジェクト構成と参照の結線を再現するためのもの
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
		private const string SceneDirectory = "Assets/_Project/Scenes";
		private const string ScenePath = SceneDirectory + "/GameScene.unity";
		private const string SpriteDirectory = "Assets/_Project/Sprites/Placeholder";
		private const int TextureSize = 256;

		private const float CellSpacing = 2.1f;
		private const float CellSize = 1.95f;
		private static readonly Vector3 BoardPosition = new Vector3(0f, -0.4f, 0f);

		private static readonly Color BackgroundColor = new Color(0.1f, 0.11f, 0.15f);
		private static readonly Color BoardColor = new Color(0.18f, 0.2f, 0.26f);
		private static readonly Color CellColor = new Color(0.26f, 0.29f, 0.36f);

		private sealed class SpriteSet
		{
			public Sprite Square;
			public Sprite Circle;
			public Sprite Cross;
			public Sprite Trap;
		}

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

			SpriteSet sprites = LoadOrCreateSprites();

			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			Camera camera = CreateCamera();
			BoardView boardView = CreateBoard(sprites, camera);
			GameUIView gameUIView = CreateUI();
			CreateEventSystem();
			CreateGameManager(boardView, gameUIView);

			Directory.CreateDirectory(SceneDirectory);
			EditorSceneManager.SaveScene(scene, ScenePath);
			AddSceneToBuildSettings(ScenePath);
			Debug.Log($"[GameSceneBuilder] {ScenePath} を生成しました");
		}

		//----------------------------------------------------
		// 2D 用の正投影カメラを作る
		//----------------------------------------------------
		private static Camera CreateCamera()
		{
			GameObject cameraObject = new GameObject("Main Camera");
			cameraObject.tag = "MainCamera";
			cameraObject.transform.position = new Vector3(0f, 0f, -10f);

			Camera camera = cameraObject.AddComponent<Camera>();
			camera.orthographic = true;
			camera.orthographicSize = 5.4f;
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = BackgroundColor;
			return camera;
		}

		//----------------------------------------------------
		// 盤面と 9 マスを作り BoardView に結線する
		//----------------------------------------------------
		private static BoardView CreateBoard(SpriteSet sprites, Camera camera)
		{
			GameObject boardObject = new GameObject("Board");
			boardObject.transform.position = BoardPosition;
			BoardView boardView = boardObject.AddComponent<BoardView>();

			float boardSize = CellSpacing * BoardModel.Size + 0.2f;
			CreateSprite("Background", boardObject.transform, sprites.Square, BoardColor, Vector3.zero, Vector3.one * boardSize, 0);

			CellView[] cells = new CellView[BoardModel.CellCount];
			for (int i = 0; i < BoardModel.CellCount; i++)
			{
				int column = i % BoardModel.Size;
				int row = i / BoardModel.Size;
				Vector3 position = new Vector3((column - 1) * CellSpacing, (1 - row) * CellSpacing, 0f);
				cells[i] = CreateCell(i, boardObject.transform, position, sprites);
			}

			SerializedObject serialized = new SerializedObject(boardView);
			serialized.FindProperty("m_Camera").objectReferenceValue = camera;
			serialized.FindProperty("m_CircleSprite").objectReferenceValue = sprites.Circle;
			serialized.FindProperty("m_CrossSprite").objectReferenceValue = sprites.Cross;
			SerializedProperty cellsProperty = serialized.FindProperty("m_Cells");
			cellsProperty.arraySize = cells.Length;
			for (int i = 0; i < cells.Length; i++)
			{
				cellsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
			}
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return boardView;
		}

		//----------------------------------------------------
		// 1マス分のオブジェクト（背景・ハイライト・トラップ・マーク）を作る
		//----------------------------------------------------
		private static CellView CreateCell(int index, Transform parent, Vector3 position, SpriteSet sprites)
		{
			SpriteRenderer background = CreateSprite($"Cell_{index}", parent, sprites.Square, CellColor, position, Vector3.one * CellSize, 1);
			GameObject cellObject = background.gameObject;

			BoxCollider2D collider = cellObject.AddComponent<BoxCollider2D>();
			collider.size = Vector2.one;

			// 子は親のスケール（CellSize）を受け継ぐので、マス1つ分を 1 とした比率で指定する
			SpriteRenderer highlight = CreateSprite("Highlight", cellObject.transform, sprites.Square, Color.clear, Vector3.zero, Vector3.one, 2);
			SpriteRenderer trap = CreateSprite("Trap", cellObject.transform, sprites.Trap, Color.white, Vector3.zero, Vector3.one * 0.92f, 3);
			SpriteRenderer mark = CreateSprite("Mark", cellObject.transform, null, Color.white, Vector3.zero, Vector3.one * 0.72f, 4);
			highlight.enabled = false;
			trap.enabled = false;
			mark.enabled = false;

			CellView cellView = cellObject.AddComponent<CellView>();
			SerializedObject serialized = new SerializedObject(cellView);
			serialized.FindProperty("m_Index").intValue = index;
			serialized.FindProperty("m_MarkRenderer").objectReferenceValue = mark;
			serialized.FindProperty("m_TrapRenderer").objectReferenceValue = trap;
			serialized.FindProperty("m_HighlightRenderer").objectReferenceValue = highlight;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return cellView;
		}

		//----------------------------------------------------
		// SpriteRenderer 付きのオブジェクトを作る
		//----------------------------------------------------
		private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Color color, Vector3 localPosition, Vector3 localScale, int sortingOrder)
		{
			GameObject spriteObject = new GameObject(name);
			spriteObject.transform.SetParent(parent, false);
			spriteObject.transform.localPosition = localPosition;
			spriteObject.transform.localScale = localScale;

			SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
			renderer.sprite = sprite;
			renderer.color = color;
			renderer.sortingOrder = sortingOrder;
			return renderer;
		}

		//----------------------------------------------------
		// Canvas と各 UI パーツを作り GameUIView に結線する
		//----------------------------------------------------
		private static GameUIView CreateUI()
		{
			GameObject canvasObject = new GameObject("Canvas");
			Canvas canvas = canvasObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;
			canvasObject.AddComponent<GraphicRaycaster>();
			Transform root = canvasObject.transform;

			// 常時表示のテキスト
			Text phaseText = CreateText("PhaseText", root, "フェーズ", 56, TextAnchor.MiddleCenter);
			SetRect(phaseText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(1400f, 80f));
			Text messageText = CreateText("MessageText", root, string.Empty, 34, TextAnchor.MiddleCenter);
			SetRect(messageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1600f, 60f));

			// トラップ設置ボタン（盤面の右側に縦並び）
			GameObject trapControlRoot = CreateUIObject("TrapControls", root);
			SetRect(trapControlRoot.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-260f, -40f), new Vector2(400f, 260f));
			Button trapConfirmButton = CreateButton("ConfirmButton", trapControlRoot.transform, "このマスに決定", new Vector2(0f, 60f), new Vector2(360f, 100f));
			Button trapSkipButton = CreateButton("SkipButton", trapControlRoot.transform, "トラップを置かない", new Vector2(0f, -60f), new Vector2(360f, 100f));

			// 交代画面（盤面を完全に隠す）
			GameObject handoffPanel = CreatePanel("HandoffPanel", root, new Color(0.05f, 0.05f, 0.08f, 1f));
			StretchFull(handoffPanel.GetComponent<RectTransform>());
			Text handoffText = CreateText("HandoffText", handoffPanel.transform, string.Empty, 48, TextAnchor.MiddleCenter);
			SetRect(handoffText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1600f, 240f));
			Button handoffButton = CreateButton("OkButton", handoffPanel.transform, "OK", new Vector2(0f, -140f), new Vector2(320f, 100f));

			// リザルト（盤面が見えるよう下部の帯にする）
			GameObject resultPanel = CreatePanel("ResultPanel", root, new Color(0f, 0f, 0f, 0.75f));
			RectTransform resultRect = resultPanel.GetComponent<RectTransform>();
			resultRect.anchorMin = new Vector2(0f, 0f);
			resultRect.anchorMax = new Vector2(1f, 0f);
			resultRect.pivot = new Vector2(0.5f, 0f);
			resultRect.anchoredPosition = Vector2.zero;
			resultRect.sizeDelta = new Vector2(0f, 180f);
			Text resultText = CreateText("ResultText", resultPanel.transform, string.Empty, 60, TextAnchor.MiddleCenter);
			SetRect(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-200f, 0f), new Vector2(1000f, 120f));
			Button retryButton = CreateButton("RetryButton", resultPanel.transform, "もう一度", new Vector2(480f, 0f), new Vector2(320f, 100f));

			trapControlRoot.SetActive(false);
			handoffPanel.SetActive(false);
			resultPanel.SetActive(false);

			GameUIView gameUIView = canvasObject.AddComponent<GameUIView>();
			SerializedObject serialized = new SerializedObject(gameUIView);
			serialized.FindProperty("m_PhaseText").objectReferenceValue = phaseText;
			serialized.FindProperty("m_MessageText").objectReferenceValue = messageText;
			serialized.FindProperty("m_TrapControlRoot").objectReferenceValue = trapControlRoot;
			serialized.FindProperty("m_TrapConfirmButton").objectReferenceValue = trapConfirmButton;
			serialized.FindProperty("m_TrapSkipButton").objectReferenceValue = trapSkipButton;
			serialized.FindProperty("m_HandoffPanel").objectReferenceValue = handoffPanel;
			serialized.FindProperty("m_HandoffText").objectReferenceValue = handoffText;
			serialized.FindProperty("m_HandoffButton").objectReferenceValue = handoffButton;
			serialized.FindProperty("m_ResultPanel").objectReferenceValue = resultPanel;
			serialized.FindProperty("m_ResultText").objectReferenceValue = resultText;
			serialized.FindProperty("m_RetryButton").objectReferenceValue = retryButton;
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
		// 単色背景のパネルを作る
		//----------------------------------------------------
		private static GameObject CreatePanel(string name, Transform parent, Color color)
		{
			GameObject panel = CreateUIObject(name, parent);
			Image image = panel.AddComponent<Image>();
			image.color = color;
			return panel;
		}

		//----------------------------------------------------
		// 組み込みフォントを使ったテキストを作る
		//----------------------------------------------------
		private static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment)
		{
			GameObject textObject = CreateUIObject(name, parent);
			Text text = textObject.AddComponent<Text>();
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			text.text = content;
			text.fontSize = fontSize;
			text.alignment = alignment;
			text.color = Color.white;
			text.raycastTarget = false;
			text.horizontalOverflow = HorizontalWrapMode.Wrap;
			text.verticalOverflow = VerticalWrapMode.Overflow;
			return text;
		}

		//----------------------------------------------------
		// ラベル付きのボタンを作る
		//----------------------------------------------------
		private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size)
		{
			GameObject buttonObject = CreateUIObject(name, parent);
			SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), position, size);

			Image image = buttonObject.AddComponent<Image>();
			image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
			image.type = Image.Type.Sliced;
			image.color = new Color(0.95f, 0.95f, 0.98f);
			Button button = buttonObject.AddComponent<Button>();

			Text text = CreateText("Label", buttonObject.transform, label, 36, TextAnchor.MiddleCenter);
			text.color = new Color(0.15f, 0.15f, 0.2f);
			StretchFull(text.rectTransform);
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

		//----------------------------------------------------
		// プレースホルダー画像を読み込む（無ければ生成する）
		//----------------------------------------------------
		private static SpriteSet LoadOrCreateSprites()
		{
			Directory.CreateDirectory(SpriteDirectory);
			return new SpriteSet
			{
				Square = LoadOrCreateSprite("Square", (x, y) => 1f),
				Circle = LoadOrCreateSprite("Circle", CircleAlpha),
				Cross = LoadOrCreateSprite("Cross", CrossAlpha),
				Trap = LoadOrCreateSprite("Trap", TrapAlpha),
			};
		}

		//----------------------------------------------------
		// 1枚分のスプライトを読み込むか、アルファ関数から PNG を生成してインポートする
		//----------------------------------------------------
		private static Sprite LoadOrCreateSprite(string name, System.Func<float, float, float> alphaFunc)
		{
			string path = $"{SpriteDirectory}/{name}.png";
			if (!File.Exists(path))
			{
				Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
				Color32[] pixels = new Color32[TextureSize * TextureSize];
				for (int y = 0; y < TextureSize; y++)
				{
					for (int x = 0; x < TextureSize; x++)
					{
						// 中心を原点、端を 1 とした座標で透明度を計算する
						float u = (x + 0.5f) / TextureSize * 2f - 1f;
						float v = (y + 0.5f) / TextureSize * 2f - 1f;
						byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaFunc(u, v)) * 255f);
						pixels[y * TextureSize + x] = new Color32(255, 255, 255, alpha);
					}
				}
				texture.SetPixels32(pixels);
				File.WriteAllBytes(path, texture.EncodeToPNG());
				Object.DestroyImmediate(texture);
				AssetDatabase.ImportAsset(path);

				TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
				importer.textureType = TextureImporterType.Sprite;
				importer.spriteImportMode = SpriteImportMode.Single;
				importer.spritePixelsPerUnit = TextureSize;
				importer.alphaIsTransparency = true;
				importer.mipmapEnabled = false;
				importer.SaveAndReimport();
			}
			return AssetDatabase.LoadAssetAtPath<Sprite>(path);
		}

		//----------------------------------------------------
		// 輪っか（〇）の透明度を返す
		//----------------------------------------------------
		private static float CircleAlpha(float u, float v)
		{
			float distance = Mathf.Sqrt(u * u + v * v);
			return StrokeAlpha(Mathf.Abs(distance - 0.72f), 0.13f);
		}

		//----------------------------------------------------
		// バツ（×）の透明度を返す
		//----------------------------------------------------
		private static float CrossAlpha(float u, float v)
		{
			const float HalfLength = 0.82f;
			float along1 = Mathf.Abs(u + v) * 0.7071f;
			float along2 = Mathf.Abs(u - v) * 0.7071f;
			float line1 = along1 <= HalfLength ? along2 : 10f;
			float line2 = along2 <= HalfLength ? along1 : 10f;
			return StrokeAlpha(Mathf.Min(line1, line2), 0.13f);
		}

		//----------------------------------------------------
		// トラップ（ひし形の枠と中央の点）の透明度を返す
		//----------------------------------------------------
		private static float TrapAlpha(float u, float v)
		{
			float diamond = Mathf.Abs(Mathf.Abs(u) + Mathf.Abs(v) - 0.85f) * 0.7071f;
			float dot = Mathf.Sqrt(u * u + v * v) - 0.12f;
			return Mathf.Max(StrokeAlpha(diamond, 0.07f), Mathf.Clamp01(-dot * TextureSize * 0.5f + 0.5f));
		}

		//----------------------------------------------------
		// 線からの距離と太さから、縁を滑らかにした透明度を返す
		//----------------------------------------------------
		private static float StrokeAlpha(float distance, float halfWidth)
		{
			// 1ピクセル分の幅で縁をなめらかにする
			float pixel = 2f / TextureSize;
			return Mathf.Clamp01((halfWidth - distance) / pixel + 0.5f);
		}
	}
}
