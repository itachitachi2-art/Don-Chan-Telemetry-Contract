# 戦闘実況入力の差分調査・再開地点（2026-09-23）
対象HEAD: c691b41a0c29d787ab079899530cd1c935cb6551。設計: Private-AItuber PR #120。
この記録はソース監査。実機で値が取得できたこと、イベントの意味が確定したことを示さない。

## 確認結果
|入力|既存処理|不足・次の作業|
|---|---|---|
|命中|DynamicPatchRegistrarがItemActionAttack.Hitへpostfix。combat.hitにentityHit、bKilled、damageGiven、critical、命中位置を保存|攻撃者の明示情報なし。各overloadの引数・主体を確認。criticalを頭部命中とみなさない|
|ダメージ|EntityAlive.DamageEntity。target、source.ownerEntityId、CreatorEntityId、damageSource/type、bodyParts、AttackingItem、amountを保存するコードあり|引数0/1固定のためoverload対応確認。amountは入力値で実HP減少と保証しない。所有者と直接攻撃者を区別|
|Kill|EntityAlive.Kill。target、response.Fatal/Critical/HitBodyPart等を保存するコードあり|Kill呼出し=プレイヤー撃破とみなさない。HitBodyPartの型・列挙値を確認|
|死亡|EntityAlive.OnEntityDeath。targetを保存|重複・順序・死亡原因の対応が未確定。死亡自体と撃破帰属を分離|
|delegate撃破|EntityKill/GameEntityKilledでargs0をactor、args1をtargetへ変換|引数順の実契約未確認。名前付けだけで帰属確定不可|
|距離フィルター|上記combatフック/正規化には15mや敵対状態のフィルターなし|この経路は維持。アプリ側で遠距離イベントを捨てない|
|頭部情報|combat.damageのsource.bodyParts、combat.killのresponse.bodyPartへの取得コードが既に存在|取得値と意味は未確認。未実装ではなく「取得試行あり・意味未検証」へ認識を更新|
|個体情報|EntitySummaryにtype/entityId/entityClass/name/position/health/dead|deadのReadはフィールド/プロパティのみ。IsDead()メソッド呼出しにはならない。レーダーの明示呼出しへ整合|
|周辺検出|EntityAlives、型名Zombie、3D距離、512件打切り、catch後途中集計|新しい水平15m契約、生存・指定6群・完走/取得失敗の区別が必要|
|敵対参照|attackTarget/attackTargetClientまたはIDを比較|nullと読取失敗、古い参照の区別が必要。両ID nullのEquals成立も防ぐ|

## 次に実装する単位
1. 新しい周辺契約を既存3D項目と別に追加。水平15m、明示生存判定、指定6群、対象参照の取得状態、完走/失敗情報。レーダーと敵分類を一致させる。実ゲームentity classとの対応確認前に分類網羅済みとはしない。
2. combatの実メソッド署名・部位値・主体情報を既存capabilities/実ログから照合。距離不問で、命中/被害/死亡を事実の粒度で渡す。撃破帰属・頭部命中は確認できたものだけ拡張する。
3. AITuberの候補一律除外を廃し、PR #120の緊急レーン/通常順位/話題選択を実装。指定6群の気配と、狩猟も含む距離不問combatは独立経路。

## 停止・再開
今回コード変更、再ビルド、テスト、実機試験は未実施。現在の稼働MOD/AITuberの挙動は変更なし。
再開時は両リポジトリの最新HEADとPR #120を確認し、この差分表から着手する。
レーダーソースはtools/DonChanZombieRadar/に保全済み。ユーザーWindows側のreset/cleanは不要。
