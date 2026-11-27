//====================================================
// ModeSelectManager.cs
// モード選択画面で選ばれたモードのシーンへ切り替える
//====================================================
using UnityEngine;

namespace RESlimeConect.ModeSelect
{
	public sealed class ModeSelectManager : MonoBehaviour
	{
		[SerializeField] private ModeSelectView m_View;

		[Header("移動先のシーン名")]
		[SerializeField] private string m_LocalSceneName = SceneLoader.LocalGame;
		[SerializeField] private string m_CpuSceneName = SceneLoader.CpuGame;
		[SerializeField] private string m_NetworkSceneName = SceneLoader.NetworkGame;

		//----------------------------------------------------
		// ボタンのイベントを購読する
		//----------------------------------------------------
		private void Awake()
		{
			m_View.LocalClicked += () => LoadScene(m_LocalSceneName);
			m_View.CpuClicked += () => LoadScene(m_CpuSceneName);
			m_View.NetworkClicked += () => LoadScene(m_NetworkSceneName);
		}

		//----------------------------------------------------
		// シーンを切り替える（読み込めたらボタンを押せなくする）
		//----------------------------------------------------
		private void LoadScene(string sceneName)
		{
			if (SceneLoader.TryLoad(sceneName))
			{
				m_View.SetButtonsInteractable(false);
			}
		}
	}
}
