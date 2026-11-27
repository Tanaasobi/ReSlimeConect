//====================================================
// NetworkDisconnectedException.cs
// 対戦相手との接続が切れたことを表す例外
//====================================================
using System;

namespace RESlimeConect.Network
{
	public sealed class NetworkDisconnectedException : Exception
	{
		//----------------------------------------------------
		// 既定のメッセージで生成する
		//----------------------------------------------------
		public NetworkDisconnectedException()
			: base("対戦相手との接続が切れました")
		{
		}
	}
}
