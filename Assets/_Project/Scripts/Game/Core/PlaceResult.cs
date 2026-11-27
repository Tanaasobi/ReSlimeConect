//====================================================
// PlaceResult.cs
// マークを置いた結果（トラップ発動の有無・最終的なマーク）
//====================================================
namespace RESlimeConect.Game
{
	public readonly struct PlaceResult
	{
		public int Index { get; }
		public EPlayerId Player { get; }
		public EMark PlacedMark { get; }
		public EMark FinalMark { get; }
		public bool TriggeredPlayer1Trap { get; }
		public bool TriggeredPlayer2Trap { get; }

		public int TrapCount => (TriggeredPlayer1Trap ? 1 : 0) + (TriggeredPlayer2Trap ? 1 : 0);
		public bool IsTrapTriggered => TrapCount > 0;
		public bool IsFlipped => PlacedMark != FinalMark;

		//----------------------------------------------------
		// 結果の各値を受け取って生成する
		//----------------------------------------------------
		public PlaceResult(int index, EPlayerId player, EMark placedMark, EMark finalMark, bool triggeredPlayer1Trap, bool triggeredPlayer2Trap)
		{
			Index = index;
			Player = player;
			PlacedMark = placedMark;
			FinalMark = finalMark;
			TriggeredPlayer1Trap = triggeredPlayer1Trap;
			TriggeredPlayer2Trap = triggeredPlayer2Trap;
		}

		//----------------------------------------------------
		// 置いたプレイヤーと踏んだトラップから、最終的なマークを求めて生成する
		// トラップ1つにつき1回反転するので、2つ重なっていれば元に戻る
		//----------------------------------------------------
		public static PlaceResult Create(int index, EPlayerId player, bool triggeredPlayer1Trap, bool triggeredPlayer2Trap)
		{
			EMark placedMark = GameRuleUtility.GetMark(player);
			EMark finalMark = placedMark;
			if (triggeredPlayer1Trap)
			{
				finalMark = GameRuleUtility.FlipMark(finalMark);
			}
			if (triggeredPlayer2Trap)
			{
				finalMark = GameRuleUtility.FlipMark(finalMark);
			}
			return new PlaceResult(index, player, placedMark, finalMark, triggeredPlayer1Trap, triggeredPlayer2Trap);
		}
	}
}
