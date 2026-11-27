//====================================================
// SlimeAnimationSet.cs
// 1色分のスライムのアニメーション一式（置いたとき・通常・勝利・変化）
//====================================================
using System;
using UnityEngine;

namespace RESlimeConect.Game
{
	[Serializable]
	public sealed class SlimeAnimationSet
	{
		[Tooltip("マスに置いたとき（1回）。終わると通常状態に切り替わる")]
		[SerializeField] private SpriteAnimationClip m_Place = new SpriteAnimationClip();
		[Tooltip("通常状態（ループ）。右上の枠の表示にも使う")]
		[SerializeField] private SpriteAnimationClip m_Idle = new SpriteAnimationClip();
		[Tooltip("勝ったとき（ループ）")]
		[SerializeField] private SpriteAnimationClip m_Win = new SpriteAnimationClip();
		[Tooltip("相手の色に変化するとき（1回）")]
		[SerializeField] private SpriteAnimationClip m_Change = new SpriteAnimationClip();

		public SpriteAnimationClip Place => m_Place;
		public SpriteAnimationClip Idle => m_Idle;
		public SpriteAnimationClip Win => m_Win;
		public SpriteAnimationClip Change => m_Change;
	}
}
