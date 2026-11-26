# プロジェクト概要
（ゲーム内容・Unityバージョン・URP等を記載）

# コーディング規約
- .editorconfig に準拠（タブインデント・LF・BOM付きUTF-8）
- { は改行、アクセス修飾子は省略しない
- フィールドは m_ + PascalCase（例: m_Health）、const / static readonly は PascalCase（例: MaxHealth）
- インターフェースは I + PascalCase（例: IDamageable）、抽象クラスは A + PascalCase（例: AEnemyBase）、列挙型は E + PascalCase（例: EPlayerState）
- クラス・メソッド・プロパティ・列挙メンバーは PascalCase、引数は camelCase
- 自作コードは Assets/_Project/Scripts 以下に置く
- ファイル先頭に //==== 区切りのヘッダー（ファイル名＋概要）を付ける
- 各関数に //---- 区切りの1行説明を付ける
- コメントに矢印記号を使わない

# 禁止事項
- Library/ Temp/ は触らない
- .meta ファイルを手で作成・削除しない（アセットの移動・リネームはUnity側かMCP経由で行う）
- シーン(.unity)やPrefabのYAMLを直接書き換えない