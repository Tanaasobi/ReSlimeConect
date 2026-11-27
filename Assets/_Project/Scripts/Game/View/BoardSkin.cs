//====================================================
// BoardSkin.cs
// 盤面の見た目（画像とアニメーション）をまとめたスキンアセット
// 素材の差し替えはこのアセットを編集するか、別のスキンを作って BoardView に割り当てる
//====================================================
using UnityEngine;

namespace RESlimeConect.Game
{
	[CreateAssetMenu(fileName = "BoardSkin", menuName = "RESlimeConect/Board Skin")]
	public sealed class BoardSkin : ScriptableObject
	{
		[Header("盤面・背景")]
		[SerializeField] private Sprite m_BoardSprite;
		[SerializeField] private Sprite m_BackgroundSprite;

		[Header("スライム")]
		[SerializeField] private SlimeAnimationSet m_CircleSlime = new SlimeAnimationSet();
		[SerializeField] private SlimeAnimationSet m_CrossSlime = new SlimeAnimationSet();

		[Header("トラップ")]
		[Tooltip("設置場所を選んでいる間の表示（ループ）")]
		[SerializeField] private SpriteAnimationClip m_TrapIdle = new SpriteAnimationClip();
		[Tooltip("設置を決定したときの演出（1回）")]
		[SerializeField] private SpriteAnimationClip m_TrapPlace = new SpriteAnimationClip();

		[Header("エフェクト")]
		[Tooltip("トラップを踏んだときに駒の上に出す演出（1回）")]
		[SerializeField] private SpriteAnimationClip m_Exclamation = new SpriteAnimationClip();

		[Header("ホバー")]
		[Tooltip("カーソルを合わせたマスに出す半透明プレビューの色")]
		[SerializeField] private Color m_HoverPreviewColor = new Color(1f, 1f, 1f, 0.45f);

		public Sprite BoardSprite => m_BoardSprite;
		public Sprite BackgroundSprite => m_BackgroundSprite;
		public SpriteAnimationClip TrapIdle => m_TrapIdle;
		public SpriteAnimationClip TrapPlace => m_TrapPlace;
		public SpriteAnimationClip Exclamation => m_Exclamation;
		public Color HoverPreviewColor => m_HoverPreviewColor;

		//----------------------------------------------------
		// マークに対応するスライムのアニメーション一式を返す
		//----------------------------------------------------
		public SlimeAnimationSet GetSlime(EMark mark)
		{
			return mark == EMark.Circle ? m_CircleSlime : m_CrossSlime;
		}
	}
}
