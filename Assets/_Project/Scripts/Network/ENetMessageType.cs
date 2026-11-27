//====================================================
// ENetMessageType.cs
// ネットワーク対戦でやり取りするメッセージの種類と、各引数の意味
// ホスト（部屋を作った側・〇）がルールを判定し、ゲスト（参加した側・×）は結果を受け取って表示する
//====================================================
namespace RESlimeConect.Network
{
	public enum ENetMessageType : byte
	{
		// ゲストからホスト：受信の準備ができた
		Hello,
		// ホストからゲスト：新しい対戦を始める（Arg0 = 先手の EPlayerId）
		RoundStart,
		// ホストからゲスト：トラップ設置を始める（Sequence = 手番番号）
		TrapPhaseStart,
		// ゲストからホスト：トラップの位置（Sequence = 手番番号、Arg0 = マス番号か NoTrap）
		Trap,
		// ホストからゲスト：トラップ設置を締め切った（Arg0 = ホストが受け付けたゲストのトラップ位置）
		TrapPhaseEnd,
		// ホストからゲスト：手番の開始（Sequence = 手番番号、Arg0 = 手番の EPlayerId）
		TurnStart,
		// ゲストからホスト：置くマス（Sequence = 手番番号、Arg0 = マス番号）
		Move,
		// ホストからゲスト：置いた結果（Sequence = 手番番号、Arg0 = マス番号、Arg1 = 置いた EPlayerId、Arg2 = 踏んだトラップのビット）
		Placed,
		// ゲストからホスト：置いた演出が終わった（Sequence = 手番番号）
		Ready,
		// ホストからゲスト：決着（Arg0 = EGameResult、Arg1 / Arg2 = 残ったトラップの位置か NoTrap）
		RoundEnd,
		// ゲストからホスト：再戦を希望する
		Rematch,
	}
}
