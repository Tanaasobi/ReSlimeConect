//====================================================
// SpriteNumberText.cs
// 文字ごとのスプライト（数字・記号）を横に並べて文字列を表示する UI
//====================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RESlimeConect.Game
{
	[RequireComponent(typeof(RectTransform))]
	public sealed class SpriteNumberText : MonoBehaviour
	{
		[Tooltip("m_Sprites と同じ順番で並べた文字")]
		[SerializeField] private string m_Characters = "1234567890+-sX";
		[SerializeField] private Sprite[] m_Sprites;
		[SerializeField] private Vector2 m_CharacterSize = new Vector2(64f, 64f);
		[Tooltip("文字同士の間隔（マイナスで詰める）")]
		[SerializeField] private float m_Spacing = -6f;

		private readonly List<Image> m_Images = new List<Image>();
		private string m_Text = string.Empty;

		public string Text => m_Text;

		//----------------------------------------------------
		// 表示する文字列を設定する（未登録の文字は飛ばす）
		//----------------------------------------------------
		public void SetText(string text)
		{
			if (text == m_Text)
			{
				return;
			}
			m_Text = text;

			int count = 0;
			foreach (char character in text)
			{
				int spriteIndex = m_Characters.IndexOf(character);
				if (spriteIndex < 0 || spriteIndex >= m_Sprites.Length)
				{
					continue;
				}

				Image image = GetOrCreateImage(count);
				image.sprite = m_Sprites[spriteIndex];
				image.gameObject.SetActive(true);
				count++;
			}

			for (int i = count; i < m_Images.Count; i++)
			{
				m_Images[i].gameObject.SetActive(false);
			}
			Layout(count);
		}

		//----------------------------------------------------
		// 表示中の文字を中央揃えで横に並べる
		//----------------------------------------------------
		private void Layout(int count)
		{
			float step = m_CharacterSize.x + m_Spacing;
			float startX = -(step * (count - 1)) * 0.5f;
			for (int i = 0; i < count; i++)
			{
				RectTransform rect = m_Images[i].rectTransform;
				rect.sizeDelta = m_CharacterSize;
				rect.anchoredPosition = new Vector2(startX + step * i, 0f);
			}
		}

		//----------------------------------------------------
		// 指定番号の文字用 Image を返す（足りなければ作る）
		//----------------------------------------------------
		private Image GetOrCreateImage(int index)
		{
			while (m_Images.Count <= index)
			{
				GameObject characterObject = new GameObject($"Char_{m_Images.Count}", typeof(RectTransform));
				characterObject.transform.SetParent(transform, false);
				Image image = characterObject.AddComponent<Image>();
				image.raycastTarget = false;
				image.preserveAspect = true;
				m_Images.Add(image);
			}
			return m_Images[index];
		}
	}
}
