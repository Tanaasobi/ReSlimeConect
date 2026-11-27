//====================================================
// SceneBuilderUtility.cs
// シーン生成メニューで共通して使う、カメラ・UI パーツの作成と参照の結線の処理
//====================================================
using System.Linq;
using RESlimeConect.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RESlimeConect.EditorTools
{
	public static class SceneBuilderUtility
	{
		public const string SceneDirectory = "Assets/_Project/Scenes";
		public const string ButtonDirectory = SpriteImportSetup.SpriteRoot + "/UI/Button";

		// 画面は 1280x720・1px = 0.01 ユニット（Pixels Per Unit 100）を基準にする
		public const float CameraSize = 3.6f;
		public static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

		// 文字ボタンと入力欄の色（土の枠に合わせた茶色系）
		private static readonly Color TextButtonColor = new Color(0.55f, 0.33f, 0.15f, 1f);
		private static readonly Color InputFieldColor = new Color(0.96f, 0.91f, 0.8f, 1f);

		//----------------------------------------------------
		// 1枚画像のスプライトを読み込む
		//----------------------------------------------------
		public static Sprite LoadSprite(string path)
		{
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
			if (sprite == null)
			{
				Debug.LogWarning($"[SceneBuilderUtility] スプライトが見つかりません: {path}");
			}
			return sprite;
		}

		//----------------------------------------------------
		// 1280x720 で 1px = 0.01 ユニットになる正投影カメラを作る
		//----------------------------------------------------
		public static Camera CreateCamera()
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
		// 1280x720 基準で拡縮する Canvas を作る
		//----------------------------------------------------
		public static Canvas CreateCanvas()
		{
			GameObject canvasObject = new GameObject("Canvas");
			Canvas canvas = canvasObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = ReferenceResolution;
			scaler.matchWidthOrHeight = 0.5f;
			canvasObject.AddComponent<GraphicRaycaster>();
			return canvas;
		}

		//----------------------------------------------------
		// Input System 用の EventSystem を作る
		//----------------------------------------------------
		public static void CreateEventSystem()
		{
			GameObject eventSystemObject = new GameObject("EventSystem");
			eventSystemObject.AddComponent<EventSystem>();
			eventSystemObject.AddComponent<InputSystemUIInputModule>();
		}

		//----------------------------------------------------
		// RectTransform 付きの空 UI オブジェクトを作る
		//----------------------------------------------------
		public static GameObject CreateUIObject(string name, Transform parent)
		{
			GameObject uiObject = new GameObject(name, typeof(RectTransform));
			uiObject.transform.SetParent(parent, false);
			return uiObject;
		}

		//----------------------------------------------------
		// 中央基準で配置した Image を作る
		//----------------------------------------------------
		public static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
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
		// SpriteAnimator で再生する Image を作る（再生されるまでは非表示）
		//----------------------------------------------------
		public static SpriteAnimator CreateAnimatedImage(string name, Transform parent, Vector2 position, Vector2 size)
		{
			Image image = CreateImage(name, parent, null, position, size);
			image.enabled = false;
			SpriteAnimator animator = image.gameObject.AddComponent<SpriteAnimator>();
			SetReference(animator, "m_Image", image);
			return animator;
		}

		//----------------------------------------------------
		// 組み込みフォントを使ったテキストを作る
		//----------------------------------------------------
		public static Text CreateText(string name, Transform parent, string content, int fontSize)
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
		// 通常・カーソルを合わせた状態・押している状態の画像を切り替えるボタンを作る（画像名は UI/Button 内のファイル名）
		//----------------------------------------------------
		public static Button CreateButton(string name, Transform parent, string idleName, string floatName, string pushName, Vector2 position, Vector2 size)
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
		// 専用の画像がまだ無いボタン用に、単色の背景に文字を載せたボタンを作る
		//----------------------------------------------------
		public static Button CreateTextButton(string name, Transform parent, string label, Vector2 position, Vector2 size, int fontSize)
		{
			Image image = CreateImage(name, parent, null, position, size);
			image.color = TextButtonColor;
			image.raycastTarget = true;

			Text text = CreateText("Label", image.transform, label, fontSize);
			StretchFull(text.rectTransform);
			AddOutline(text);

			Button button = image.gameObject.AddComponent<Button>();
			button.navigation = new Navigation { mode = Navigation.Mode.None };
			return button;
		}

		//----------------------------------------------------
		// 1行の文字入力欄を作る（未入力時は placeholder を薄く表示する）
		//----------------------------------------------------
		public static InputField CreateInputField(string name, Transform parent, string placeholder, Vector2 position, Vector2 size, int fontSize)
		{
			Image image = CreateImage(name, parent, null, position, size);
			image.color = InputFieldColor;
			image.raycastTarget = true;

			Text placeholderText = CreateText("Placeholder", image.transform, placeholder, fontSize);
			StretchFull(placeholderText.rectTransform);
			placeholderText.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
			placeholderText.fontStyle = FontStyle.Italic;

			Text inputText = CreateText("Text", image.transform, string.Empty, fontSize);
			StretchFull(inputText.rectTransform);
			inputText.color = Color.black;
			inputText.supportRichText = false;

			InputField inputField = image.gameObject.AddComponent<InputField>();
			inputField.textComponent = inputText;
			inputField.placeholder = placeholderText;
			inputField.navigation = new Navigation { mode = Navigation.Mode.None };
			return inputField;
		}

		//----------------------------------------------------
		// 文字に黒い縁取りを付ける
		//----------------------------------------------------
		public static void AddOutline(Text text)
		{
			Outline outline = text.gameObject.AddComponent<Outline>();
			outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
			outline.effectDistance = new Vector2(2f, -2f);
		}

		//----------------------------------------------------
		// 指定アンカー基準で位置とサイズを設定する
		//----------------------------------------------------
		public static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
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
		public static void StretchFull(RectTransform rect)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}

		//----------------------------------------------------
		// コンポーネントのオブジェクト参照フィールドを設定する
		//----------------------------------------------------
		public static void SetReference(Object target, string propertyName, Object value)
		{
			SerializedObject serialized = new SerializedObject(target);
			serialized.FindProperty(propertyName).objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		//----------------------------------------------------
		// コンポーネントのオブジェクト配列フィールドを設定する
		//----------------------------------------------------
		public static void SetReferenceArray(Object target, string propertyName, Object[] values)
		{
			SerializedObject serialized = new SerializedObject(target);
			SerializedProperty property = serialized.FindProperty(propertyName);
			property.arraySize = values.Length;
			for (int i = 0; i < values.Length; i++)
			{
				property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
			}
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		//----------------------------------------------------
		// コンポーネントのアニメーションクリップ（コマ・再生速度・ループ）を設定する
		//----------------------------------------------------
		public static void SetClip(Object target, string propertyName, Sprite[] frames, float frameRate, bool loop)
		{
			SerializedObject serialized = new SerializedObject(target);
			SerializedProperty framesProperty = serialized.FindProperty(propertyName + ".m_Frames");
			framesProperty.arraySize = frames.Length;
			for (int i = 0; i < frames.Length; i++)
			{
				framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
			}
			serialized.FindProperty(propertyName + ".m_FrameRate").floatValue = frameRate;
			serialized.FindProperty(propertyName + ".m_Loop").boolValue = loop;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		//----------------------------------------------------
		// シーンをビルド設定に登録する（first なら先頭に移し、起動時に開くシーンにする）
		//----------------------------------------------------
		public static void AddSceneToBuildSettings(string path, bool first)
		{
			EditorBuildSettingsScene[] others = EditorBuildSettings.scenes.Where(scene => scene.path != path).ToArray();
			EditorBuildSettingsScene target = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.path == path) ?? new EditorBuildSettingsScene(path, true);
			if (first)
			{
				EditorBuildSettings.scenes = new[] { target }.Concat(others).ToArray();
			}
			else if (!EditorBuildSettings.scenes.Any(scene => scene.path == path))
			{
				EditorBuildSettings.scenes = others.Concat(new[] { target }).ToArray();
			}
		}
	}
}
