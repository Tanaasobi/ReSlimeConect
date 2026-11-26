//====================================================
// GameRuleUtility.cs
// プレイヤーとマークの対応や反転など、ルール上の小さな変換処理
//====================================================
namespace RESlimeConect.Game
{
	public static class GameRuleUtility
	{
		//----------------------------------------------------
		// プレイヤーが使うマークを返す
		//----------------------------------------------------
		public static EMark GetMark(EPlayerId player)
		{
			return player == EPlayerId.Player1 ? EMark.Circle : EMark.Cross;
		}

		//----------------------------------------------------
		// マークの持ち主となるプレイヤーを返す（None は呼ばない前提）
		//----------------------------------------------------
		public static EPlayerId GetOwner(EMark mark)
		{
			return mark == EMark.Circle ? EPlayerId.Player1 : EPlayerId.Player2;
		}

		//----------------------------------------------------
		// 相手プレイヤーを返す
		//----------------------------------------------------
		public static EPlayerId GetOpponent(EPlayerId player)
		{
			return player == EPlayerId.Player1 ? EPlayerId.Player2 : EPlayerId.Player1;
		}

		//----------------------------------------------------
		// マークを反転する（None はそのまま）
		//----------------------------------------------------
		public static EMark FlipMark(EMark mark)
		{
			switch (mark)
			{
				case EMark.Circle:
					return EMark.Cross;
				case EMark.Cross:
					return EMark.Circle;
				default:
					return EMark.None;
			}
		}

		//----------------------------------------------------
		// 表示用のプレイヤー名を返す
		//----------------------------------------------------
		public static string GetDisplayName(EPlayerId player)
		{
			return player == EPlayerId.Player1 ? "プレイヤー1（〇）" : "プレイヤー2（×）";
		}
	}
}
