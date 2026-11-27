//====================================================
// SpriteAnimationClip.cs
// コマ画像の並び・再生速度・ループ有無をまとめたアニメーション定義
//====================================================
using System;
using UnityEngine;

namespace RESlimeConect.Game
{
	[Serializable]
	public sealed class SpriteAnimationClip
	{
		[SerializeField] private Sprite[] m_Frames = Array.Empty<Sprite>();
		[SerializeField, Min(0.1f)] private float m_FrameRate = 12f;
		[SerializeField] private bool m_Loop = true;

		public int FrameCount => m_Frames != null ? m_Frames.Length : 0;
		public float FrameRate => m_FrameRate;
		public bool Loop => m_Loop;
		public float Duration => FrameCount / m_FrameRate;
		public bool IsValid => FrameCount > 0;
		public Sprite FirstFrame => IsValid ? m_Frames[0] : null;

		//----------------------------------------------------
		// 指定番号のコマ画像を返す
		//----------------------------------------------------
		public Sprite GetFrame(int index)
		{
			return m_Frames[index];
		}
	}
}
