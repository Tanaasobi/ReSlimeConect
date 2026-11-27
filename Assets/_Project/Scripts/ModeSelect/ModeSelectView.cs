//====================================================
// ModeSelectView.cs
// モード選択画面の見た目（各アイコンのアニメーション）とボタン入力を担当する
//====================================================
using System;
using RESlimeConect.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RESlimeConect.ModeSelect
{
	public sealed class ModeSelectView : MonoBehaviour
	{
		[Header("ボタン")]
		[SerializeField] private Button m_LocalButton;
		[SerializeField] private Button m_CpuButton;
		[SerializeField] private Button m_NetworkButton;

		[Header("スライム（通常状態はスキンのものを使う）")]
		[SerializeField] private BoardSkin m_Skin;
		[SerializeField] private SpriteAnimator[] m_CircleSlimes;
		[SerializeField] private SpriteAnimator[] m_CrossSlimes;

		[Header("エフェクト")]
		[SerializeField] private SpriteAnimator m_VsAnimator;
		[SerializeField] private SpriteAnimationClip m_VsClip = new SpriteAnimationClip();
		[SerializeField] private SpriteAnimator[] m_WifiAnimators;
		[SerializeField] private SpriteAnimationClip m_WifiClip = new SpriteAnimationClip();

		public event Action LocalClicked;
		public event Action CpuClicked;
		public event Action NetworkClicked;

		//----------------------------------------------------
		// ボタンのクリックをイベントとして公開する
		//----------------------------------------------------
		private void Awake()
		{
			m_LocalButton.onClick.AddListener(() => LocalClicked?.Invoke());
			m_CpuButton.onClick.AddListener(() => CpuClicked?.Invoke());
			m_NetworkButton.onClick.AddListener(() => NetworkClicked?.Invoke());
		}

		//----------------------------------------------------
		// 各アイコンのループアニメーションを開始する
		//----------------------------------------------------
		private void Start()
		{
			if (m_Skin == null)
			{
				Debug.LogWarning("[ModeSelectView] スキンが設定されていません", this);
			}
			else
			{
				PlayAll(m_CircleSlimes, m_Skin.GetSlime(EMark.Circle).Idle);
				PlayAll(m_CrossSlimes, m_Skin.GetSlime(EMark.Cross).Idle);
			}
			m_VsAnimator.Play(m_VsClip);
			PlayAll(m_WifiAnimators, m_WifiClip);
		}

		//----------------------------------------------------
		// ボタンの押せる・押せないをまとめて切り替える（シーン移動中の二度押し防止）
		//----------------------------------------------------
		public void SetButtonsInteractable(bool interactable)
		{
			m_LocalButton.interactable = interactable;
			m_CpuButton.interactable = interactable;
			m_NetworkButton.interactable = interactable;
		}

		//----------------------------------------------------
		// 複数の表示先に同じアニメーションを再生する
		//----------------------------------------------------
		private static void PlayAll(SpriteAnimator[] animators, SpriteAnimationClip clip)
		{
			foreach (SpriteAnimator animator in animators)
			{
				animator.Play(clip);
			}
		}
	}
}
