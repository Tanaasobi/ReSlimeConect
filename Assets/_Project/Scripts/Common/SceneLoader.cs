//====================================================
// SceneLoader.cs
// シーン名の定義と、ビルド設定に登録済みかを確かめてからのシーン切り替え
//====================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RESlimeConect
{
	public static class SceneLoader
	{
		public const string ModeSelect = "ModeSelectScene";
		public const string LocalGame = "GameScene";
		public const string CpuGame = "CpuGameScene";
		public const string NetworkGame = "NetworkGameScene";

		//----------------------------------------------------
		// シーンを読み込む（まだ作られていない・ビルド設定に無い場合は警告を出して false を返す）
		//----------------------------------------------------
		public static bool TryLoad(string sceneName)
		{
			if (!Application.CanStreamedLevelBeLoaded(sceneName))
			{
				Debug.LogWarning($"[SceneLoader] シーン {sceneName} はまだ作られていないか、ビルド設定に登録されていません");
				return false;
			}
			SceneManager.LoadScene(sceneName);
			return true;
		}
	}
}
