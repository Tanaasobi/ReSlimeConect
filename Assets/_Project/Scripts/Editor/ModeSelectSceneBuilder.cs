//====================================================
// ModeSelectSceneBuilder.cs
// モード選択シーンをエディタ API で自動生成するメニュー
// 左から 2人でプレイ（ローカル対戦）・1人でプレイ（CPU 対戦）・ネットワーク対戦 の 3 列に並べる
//====================================================
using System.IO;
using RESlimeConect.Game;
using RESlimeConect.ModeSelect;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static RESlimeConect.EditorTools.SceneBuilderUtility;

namespace RESlimeConect.EditorTools
{
	public static class ModeSelectSceneBuilder
	{
		// 出力先
		private const string ScenePath = SceneDirectory + "/" + SceneLoader.ModeSelect + ".unity";

		// 素材
		private const string SpriteRoot = SpriteImportSetup.SpriteRoot;
		private const string BackgroundPath = SpriteRoot + "/BG/BG.png";
		private const string TitlePath = SpriteRoot + "/UI/Text/ModeSelect.png";
		private const string IconFramePath = SpriteRoot + "/UI/Frame/Player.png";
		private const string VsPath = SpriteRoot + "/Effect/VS.png";
		private const string WifiPath = SpriteRoot + "/Effect/Wifi.png";

		// レイアウト（UI のピクセル、画面中央が原点）
		private static readonly Vector2 TitlePosition = new Vector2(0f, 258f);
		private static readonly Vector2 TitleSize = new Vector2(288f, 96f);
		private const float ColumnSpacing = 400f;
		private const float IconY = 30f;
		private static readonly Vector2 IconSize = new Vector2(360f, 288f);
		private const float ButtonY = -214f;
		private static readonly Vector2 ButtonSize = new Vector2(288f, 96f);

		// アイコン内の配置（アイコン枠の中心が原点）
		private const float LargeSlimeSize = 210f;
		private const float SmallSlimeSize = 106f;
		private static readonly Vector2 VsSize = new Vector2(280f, 280f);
		private static readonly Vector2 WifiSize = new Vector2(96f, 96f);

		// 再生速度
		private const float VsFrameRate = 6f;
		private const float WifiFrameRate = 8f;

		//----------------------------------------------------
		// メニューからモード選択シーンを生成して保存する
		//----------------------------------------------------
		[MenuItem("Tools/RESlimeConect/モード選択シーンを生成")]
		public static void BuildModeSelectScene()
		{
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}
			if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("モード選択シーンを生成", $"{ScenePath} は既に存在します。上書きしますか？", "上書き", "キャンセル"))
			{
				return;
			}

			// 新規シーン作成時に未使用アセットが解放されるため、素材の読み込みはシーンを作った後に行う
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			SpriteImportSetup.ApplyAll();
			BoardSkin skin = GameSceneBuilder.LoadOrCreateSkin();

			CreateCamera();
			ModeSelectView view = CreateUI(skin);
			CreateEventSystem();
			CreateManager(view);

			Directory.CreateDirectory(SceneDirectory);
			EditorSceneManager.SaveScene(scene, ScenePath);
			AddSceneToBuildSettings(ScenePath, true);
			Debug.Log($"[ModeSelectSceneBuilder] {ScenePath} を生成しました（ビルド設定の先頭に登録）");
		}

		//----------------------------------------------------
		// 背景・タイトル・3 つのアイコンとボタンを作り ModeSelectView に結線する
		//----------------------------------------------------
		private static ModeSelectView CreateUI(BoardSkin skin)
		{
			GameObject canvasObject = CreateCanvas().gameObject;
			Transform root = canvasObject.transform;

			Image background = CreateImage("Background", root, LoadSprite(BackgroundPath), Vector2.zero, Vector2.zero);
			background.preserveAspect = false;
			StretchFull(background.rectTransform);
			CreateImage("Title", root, LoadSprite(TitlePath), TitlePosition, TitleSize);

			// 左：2人でプレイ（右上に × のスライム、左下に 〇 のスライム、中央に VS）
			Transform localIcon = CreateIcon("LocalIcon", root, -ColumnSpacing);
			SpriteAnimator localCross = CreateAnimatedImage("CrossSlime", localIcon, new Vector2(106f, 79f), Vector2.one * SmallSlimeSize);
			SpriteAnimator localCircle = CreateAnimatedImage("CircleSlime", localIcon, new Vector2(-85f, -61f), Vector2.one * SmallSlimeSize);
			SpriteAnimator vs = CreateAnimatedImage("VS", localIcon, new Vector2(0f, 8f), VsSize);

			// 中央：1人でプレイ（大きな 〇 のスライム）
			Transform cpuIcon = CreateIcon("CpuIcon", root, 0f);
			SpriteAnimator cpuCircle = CreateAnimatedImage("CircleSlime", cpuIcon, new Vector2(0f, 35f), Vector2.one * LargeSlimeSize);

			// 右：ネットワーク対戦（並んだスライムの頭の上に電波）
			Transform networkIcon = CreateIcon("NetworkIcon", root, ColumnSpacing);
			SpriteAnimator networkCircle = CreateAnimatedImage("CircleSlime", networkIcon, new Vector2(-61f, -42f), Vector2.one * SmallSlimeSize);
			SpriteAnimator networkCross = CreateAnimatedImage("CrossSlime", networkIcon, new Vector2(57f, -42f), Vector2.one * SmallSlimeSize);
			SpriteAnimator circleWifi = CreateAnimatedImage("CircleWifi", networkIcon, new Vector2(-61f, 48f), WifiSize);
			SpriteAnimator crossWifi = CreateAnimatedImage("CrossWifi", networkIcon, new Vector2(57f, 48f), WifiSize);

			Button localButton = CreateButton("LocalButton", root, "2Pmode_Idle", "2Pmode_Float", "2Pmode_Push", new Vector2(-ColumnSpacing, ButtonY), ButtonSize);
			Button cpuButton = CreateButton("CpuButton", root, "1Pmode_Idle", "1Pmode_Float", "1Pmode_Push", new Vector2(0f, ButtonY), ButtonSize);
			Button networkButton = CreateButton("NetworkButton", root, "NetworkMode_Idle", "NetworkMode_Float", "NetworkMode_Push", new Vector2(ColumnSpacing, ButtonY), ButtonSize);

			ModeSelectView view = canvasObject.AddComponent<ModeSelectView>();
			SetReference(view, "m_LocalButton", localButton);
			SetReference(view, "m_CpuButton", cpuButton);
			SetReference(view, "m_NetworkButton", networkButton);
			SetReference(view, "m_Skin", skin);
			SetReferenceArray(view, "m_CircleSlimes", new Object[] { localCircle, cpuCircle, networkCircle });
			SetReferenceArray(view, "m_CrossSlimes", new Object[] { localCross, networkCross });
			SetReference(view, "m_VsAnimator", vs);
			SetClip(view, "m_VsClip", SpriteImportSetup.LoadSheet(VsPath), VsFrameRate, true);
			SetReferenceArray(view, "m_WifiAnimators", new Object[] { circleWifi, crossWifi });
			SetClip(view, "m_WifiClip", SpriteImportSetup.LoadSheet(WifiPath), WifiFrameRate, true);
			return view;
		}

		//----------------------------------------------------
		// アイコンの背景枠を作り、中身を置く親として返す
		//----------------------------------------------------
		private static Transform CreateIcon(string name, Transform parent, float x)
		{
			Image frame = CreateImage(name, parent, LoadSprite(IconFramePath), new Vector2(x, IconY), IconSize);
			frame.preserveAspect = false;
			return frame.transform;
		}

		//----------------------------------------------------
		// ModeSelectManager を作り画面と結線する
		//----------------------------------------------------
		private static void CreateManager(ModeSelectView view)
		{
			GameObject managerObject = new GameObject("ModeSelectManager");
			ModeSelectManager manager = managerObject.AddComponent<ModeSelectManager>();
			SetReference(manager, "m_View", view);
		}
	}
}
