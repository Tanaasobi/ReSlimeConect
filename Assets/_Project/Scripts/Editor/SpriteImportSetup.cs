//====================================================
// SpriteImportSetup.cs
// Assets/_Project/Sprites 以下の画像にドット絵用のインポート設定を適用し、スプライトシートをグリッド分割する
//====================================================
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace RESlimeConect.EditorTools
{
	public static class SpriteImportSetup
	{
		public const string SpriteRoot = "Assets/_Project/Sprites";
		private const int PixelsPerUnit = 100;
		private const int MaxTextureSize = 4096;

		private sealed class SheetDefinition
		{
			public readonly string Path;
			public readonly int Columns;
			public readonly int Rows;
			public readonly int Count;
			public readonly Vector2 Pivot;

			//----------------------------------------------------
			// シートのパスとコマ割り（横の数・縦の数・使うコマ数）を指定して生成する（ピボットは中央）
			//----------------------------------------------------
			public SheetDefinition(string path, int columns, int rows, int count)
				: this(path, columns, rows, count, new Vector2(0.5f, 0.5f))
			{
			}

			//----------------------------------------------------
			// コマ割りに加えて、絵がコマの中央からずれている場合のピボット（0〜1）を指定して生成する
			//----------------------------------------------------
			public SheetDefinition(string path, int columns, int rows, int count, Vector2 pivot)
			{
				Path = path;
				Columns = columns;
				Rows = rows;
				Count = count;
				Pivot = pivot;
			}
		}

		// 左上から右へ、行ごとに番号を振る
		private static readonly SheetDefinition[] Sheets =
		{
			new SheetDefinition(SpriteRoot + "/Placeholder/Slime/O/Idle.png", 7, 3, 21),
			new SheetDefinition(SpriteRoot + "/Placeholder/Slime/O/Win.png", 6, 2, 12),
			new SheetDefinition(SpriteRoot + "/Placeholder/Slime/O/Change.png", 7, 2, 14),
			new SheetDefinition(SpriteRoot + "/Placeholder/Slime/X/Idle.png", 7, 3, 21),
			new SheetDefinition(SpriteRoot + "/Placeholder/Slime/X/Win.png", 6, 2, 12),
			new SheetDefinition(SpriteRoot + "/Placeholder/Slime/X/Change.png", 7, 2, 14),
			// ポーションは幅 46px のコマの x=2〜29 に描かれているので、横方向の中心（16px）にピボットを合わせる
			new SheetDefinition(SpriteRoot + "/Placeholder/Trap/Trap_Idle.png", 5, 2, 10, new Vector2(16f / 46f, 0.5f)),
			new SheetDefinition(SpriteRoot + "/Placeholder/Trap/Trap_Play.png", 5, 2, 9),
			new SheetDefinition(SpriteRoot + "/Effect/Exclamation.png", 5, 3, 15),
			new SheetDefinition(SpriteRoot + "/UI/Text/Number.png", 14, 1, 14),
		};

		//----------------------------------------------------
		// メニューから全画像のインポート設定を適用する
		//----------------------------------------------------
		[MenuItem("Tools/RESlimeConect/素材のインポート設定を適用")]
		public static void ApplyAll()
		{
			string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteRoot });
			foreach (string guid in guids)
			{
				Apply(AssetDatabase.GUIDToAssetPath(guid));
			}
			Debug.Log($"[SpriteImportSetup] {guids.Length} 枚の画像にインポート設定を適用しました");
		}

		//----------------------------------------------------
		// 1枚の画像にインポート設定を適用し、シートならグリッド分割する
		//----------------------------------------------------
		public static void Apply(string path)
		{
			TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer == null)
			{
				return;
			}

			SheetDefinition sheet = Sheets.FirstOrDefault(definition => definition.Path == path);
			importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = sheet != null ? SpriteImportMode.Multiple : SpriteImportMode.Single;
			importer.spritePixelsPerUnit = PixelsPerUnit;
			importer.filterMode = FilterMode.Point;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.mipmapEnabled = false;
			importer.alphaIsTransparency = true;
			importer.maxTextureSize = MaxTextureSize;

			// ドット絵の端が欠けないよう、メッシュは矩形のままにする
			TextureImporterSettings settings = new TextureImporterSettings();
			importer.ReadTextureSettings(settings);
			settings.spriteMeshType = SpriteMeshType.FullRect;
			importer.SetTextureSettings(settings);

			if (sheet != null)
			{
				SliceGrid(importer, sheet);
			}

			// 分割情報（ピボットなど）だけが変わった場合も .meta に書き出されるよう、変更ありの印を付けてから保存する
			EditorUtility.SetDirty(importer);
			importer.SaveAndReimport();
		}

		//----------------------------------------------------
		// シートをグリッドで分割する（既存の分割と数が同じなら ID を引き継いで参照を保つ）
		//----------------------------------------------------
		private static void SliceGrid(TextureImporter importer, SheetDefinition sheet)
		{
			importer.GetSourceTextureWidthAndHeight(out int width, out int height);
			float cellWidth = (float)width / sheet.Columns;
			float cellHeight = (float)height / sheet.Rows;
			string baseName = Path.GetFileNameWithoutExtension(sheet.Path);

			SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
			factories.Init();
			ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
			dataProvider.InitSpriteEditorDataProvider();

			Dictionary<string, GUID> existingIds = dataProvider.GetSpriteRects().ToDictionary(rect => rect.name, rect => rect.spriteID);
			SpriteRect[] rects = new SpriteRect[sheet.Count];
			for (int i = 0; i < sheet.Count; i++)
			{
				int column = i % sheet.Columns;
				int row = i / sheet.Columns;
				string name = $"{baseName}_{i}";

				// Unity の矩形は左下原点なので、上の行ほど y が大きい
				rects[i] = new SpriteRect
				{
					name = name,
					rect = new Rect(column * cellWidth, height - (row + 1) * cellHeight, cellWidth, cellHeight),
					alignment = sheet.Pivot == new Vector2(0.5f, 0.5f) ? SpriteAlignment.Center : SpriteAlignment.Custom,
					pivot = sheet.Pivot,
					spriteID = existingIds.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
				};
			}

			dataProvider.SetSpriteRects(rects);
			ISpriteNameFileIdDataProvider nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
			nameFileIdProvider.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
			dataProvider.Apply();
		}

		//----------------------------------------------------
		// 分割済みシートのスプライトを番号順に読み込む
		//----------------------------------------------------
		public static Sprite[] LoadSheet(string path)
		{
			return AssetDatabase.LoadAllAssetsAtPath(path)
				.OfType<Sprite>()
				.OrderBy(sprite => ParseFrameNumber(sprite.name))
				.ToArray();
		}

		//----------------------------------------------------
		// 「名前_番号」の番号部分を返す（無ければ 0）
		//----------------------------------------------------
		private static int ParseFrameNumber(string spriteName)
		{
			int separator = spriteName.LastIndexOf('_');
			return separator >= 0 && int.TryParse(spriteName.Substring(separator + 1), out int number) ? number : 0;
		}
	}
}
