//====================================================
// NetMessage.cs
// ネットワーク対戦でやり取りする1件分のメッセージ（種類・手番番号・整数の引数3つ）
//====================================================
namespace RESlimeConect.Network
{
	public readonly struct NetMessage
	{
		// 手番番号を使わないメッセージに入れる値
		public const int NoSequence = 0;

		public ENetMessageType Type { get; }
		public int Sequence { get; }
		public int Arg0 { get; }
		public int Arg1 { get; }
		public int Arg2 { get; }

		//----------------------------------------------------
		// 種類と各値を指定して生成する
		//----------------------------------------------------
		public NetMessage(ENetMessageType type, int sequence = NoSequence, int arg0 = 0, int arg1 = 0, int arg2 = 0)
		{
			Type = type;
			Sequence = sequence;
			Arg0 = arg0;
			Arg1 = arg1;
			Arg2 = arg2;
		}

		//----------------------------------------------------
		// ログ用の文字列にする
		//----------------------------------------------------
		public override string ToString()
		{
			return $"{Type}(#{Sequence}: {Arg0}, {Arg1}, {Arg2})";
		}
	}
}
