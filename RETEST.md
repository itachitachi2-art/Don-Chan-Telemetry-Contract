# 修正版2の再確認（全16項目のやり直し不要）

ゲーム終了後 install.bat。検証版は同じTelemetryProbeの入れ替えです。旧MODとログはMods外へ退避します。
このZIPでは検証ON/0.5秒です。DLL入りでビルド不要。レーダーDLLは含みません。

F9の番号は前回と同じです。不要な番号はF9を押して飛ばせます。F1コンソールで番号を確認。
1. F9を4回まで進める：ゾンビクマを近くで生存のまま5秒観察。倒して5秒待つ。
2. F9を6まで進める：ダイアウルフで同じ操作。
3. F9で7：グレースで同じ操作。
4. F9で8：近接Aで胴体。F9で9：同じ武器で頭。各2回を目安。
5. F9で10：近接Bで胴体。F9で11：同じ武器で頭。
6. F9で12：銃で胴体。F9で13：同じ銃で頭。

部位の検証は「生き残った対象への命中」が必要。強すぎる武器や必殺攻撃を避け、倒れたら新しい個体で。
各攻撃間3秒。テスト用に設定を変えたらメモへ記載。全部実施が難しければ近接1種と銃だけでも先に解析できます。
終了後ゲームを閉じ export-logs.bat。生成ZIPと武器名・飛ばした番号を送付。
通常設定へ戻すときは導入先TelemetryConfig/telemetryprobe.xmlのEnableCombatValidation=false、SnapshotIntervalSeconds=1.0として再起動。

## 修正と確認状況

- EntityClass.GetEntityClassName(int)経由でクラス名を解決。従来はDictionarySaveをIDictionaryへ変換できず失敗。
- ドローン、車両、プレイヤー、タレットの既知型を対象外へ分類。未知の敵を一般除外しない。
- ProcessDamageResponseLocalの応答をcombat.responseとして記録。死亡しない攻撃もHitBodyPart/Sourceを記録する経路。
- combat.hitのSimulate/不明モードを通常イベントから除外。診断validation.combatには残す。
- responseとdamage/killは同じ攻撃に由来し得るため加算しない。Kill呼び出しだけで撃破を数えない。
- 旧ログの238 hit呼び出し中Simulate120、実モード118。実モードでも有効命中を保証しない。
- フルDLLビルド、近傍27項目・モード判定6項目の自動テスト成功。修正後の実機動作は未確認。
- クラスIDの数値はゲーム実行環境依存なのでハードコードしない。
