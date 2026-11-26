//====================================================
// IPlayerController.cs
// トラップ位置や打つマスを決める操作主体の共通インターフェース
// ローカル操作・CPU・オンライン相手をこれで差し替える
//====================================================
using System.Threading;
using UnityEngine;

namespace RESlimeConect.Game
{
	public interface IPlayerController
	{
		//----------------------------------------------------
		// トラップを置くマスを決める（置かない場合は BoardModel.NoTrap）
		//----------------------------------------------------
		Awaitable<int> RequestTrapAsync(EPlayerId self, BoardModel board, CancellationToken token);

		//----------------------------------------------------
		// マークを置くマスを決める（空きマスを返すこと）
		//----------------------------------------------------
		Awaitable<int> RequestMoveAsync(EPlayerId self, BoardModel board, CancellationToken token);
	}
}
