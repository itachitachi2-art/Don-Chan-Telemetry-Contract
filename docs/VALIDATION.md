# 実機検証手順

## 0. 前提

- 7 Days to Die V3.x
- EAC無効
- `0_TFP_Harmony` がゲームの `Mods` に存在
- ProbeをReleaseビルドしてインストール済み

## 1. 起動だけで確認するもの

タイトル→ワールド入場後、以下が生成されること。

```text
Mods/DonChanTelemetryProbe/Telemetry/
  session.json
  capabilities.jsonl
  events.jsonl
  catalog/assembly-types.json
  catalog/relevant-members.json
  snapshots/latest.json
  snapshots.jsonl
```

`capabilities.jsonl` でまず `GameManager.Update` patch が `ok:true` か確認する。

## 2. シナリオ

### A. Player baseline

- 30秒静止
- 走る
- ジャンプ
- 落下ダメージを少量受ける

確認: snapshotsでposition/stats候補が変化する。

### B. Inventory

- Toolbeltの2スロットを入れ替え
- BackpackからToolbeltへ移動
- アイテム1個を捨てる/拾う

確認: `events.jsonl` に Inventory/Bag 系 event が出るか。

### C. Combat

- 棍棒でゾンビを3回攻撃
- 銃で3回攻撃
- ゾンビを倒す
- 自分も1回被弾

確認: `combat.hit`, `combat.damage`, `EntityKill`, `GameEntityKilled` の重複関係を記録する。どれを本番採用するかはこの照合後に決める。

### D. Block/Harvest

- 木を切る
- 石を掘る
- 鉄/鉱石を掘る
- ブロックを設置
- アップグレード

確認: BlockChange/Destroy/Place/Upgrade と HarvestItem の引数から material/block/item/count が取れるか。

### E. Item actions

- 食べる
- 飲む
- 包帯等を使う
- 装備を着る
- Scrap
- Repair
- Craft

確認: QuestEventManager各eventのpayload。

### F. Quest

可能な範囲で受注・目的進行・完了。

確認: player instance events と QuestEventManager のどちらが必要十分か。

## 3. 判定基準

本番採用候補は以下を満たすもの。

- 同じ操作で毎回発火する
- 不要な重複が少ない
- 引数から意味を確定できる
- pollingより低コスト
- バージョン固有private memberへの依存が少ない

## 4. ログを見る順

1. `capabilities.jsonl` — そもそも購読/patchできたか
2. `events.jsonl` — 操作ごとのイベント候補
3. `snapshots/latest.json` — 現在値のroot構造
4. `catalog/relevant-members.json` — 欲しい値の正式member候補

## 5. 次の改修判断

- `ok:false not-found` → catalogから実名を探し候補名を更新
- イベントは取れるが引数不足 → 発火元methodをcatalogから辿りHarmony候補化
- Reflection snapshotで値は取れるがevent無し → 2〜5Hz polling候補
- 高頻度すぎるevent → Probeで直接集計せず本番Aggregator側でdebounce/delta化


## G. v0.1.10 gameplay-context validation

Perform these after the existing semantic-action test:

1. Fire a partially loaded gun, reload it, switch ammo type if possible.
   - verify `weapon.loaded`, `weapon.magazineSize`, `weapon.ammoItem`, `weapon.reserve`
   - verify `weapon.reloading` during reload and `action.reload.request/complete` events
2. Aim at a zombie, then a damaged block.
   - verify `focus.kind=entity` then `focus.kind=block`
   - verify entity/block distance and block `remainingHp` when available
3. Let several zombies approach.
   - verify 5/10/15 m counts and `zombiesTargetingPlayer`
4. Walk/run/jump and enter a vehicle.
   - verify movement flags and vehicle speed/fuel/health
5. Apply an injury/buff if practical.
   - verify a normalized status appears only when a matching active buff exists
6. Track a quest.
   - verify quest code/state/POI/distance and objective summaries

Any missing member should result in an omitted/null field, not a guessed value.


## H. v0.1.11 contract/normalization validation

1. Confirm every new snapshot/event has `schemaVersion=1`, one session-scoped `sessionId`, increasing `sequence`, and `observedAt`.
2. Stand still with run mode enabled: `runModeActive=true` may remain, but `running=false` and `locomotion=idle`.
3. Actually run: `moving=true`, `running=true`, `locomotion=running`.
4. Use an auger: `weapon.kind=poweredTool` and gas state appears under `weapon.fuel`.
5. Receive heavy hits without a real stun: `stunned` must remain absent/false even when unrelated stamina/stunt-named buffs are active.
6. If a real `buffInjuryStunned1/2` is present, `stunned=true` is allowed.


## I. v0.1.12 semantic normalization validation

1. オーガーを数秒使用: `action.poweredTool.use`、`action.ranged.fire` は無し。
2. オーガーを燃料補給: `action.refuel.request` → `action.refuel.complete`。
3. ネイルガン等へ持ち替えるだけでは `action.reload.cancel` が出ない。
4. 銃を実リロード: `action.reload.request` → `action.reload.complete`。
5. 銃リロードを途中キャンセル: request 後にのみ `action.reload.cancel`。


## J. v0.1.13 full-equipment semantic normalization

The v0.1.12 full-equipment sweep exposed mechanically valid but commentary-poor classifications. Verify:

- tactical AR -> `action.ranged.fire / Rifle`
- nailgun projectile fire -> `action.ranged.fire / Nailgun`
- robotic sledge -> `action.deployable.use / RoboticSledge`
- robotic turret -> `action.deployable.use / RoboticTurret`
- wrench / ratchet / impact driver / hammer / axe / pickaxe / shovel / torch -> `action.tool.use`
- chainsaw / auger -> `action.poweredTool.use`
- consumables -> `action.consume.complete / Consumable` with `Food / Drink / Medicine / Boost` subtype
- reload/refuel events retain the item subtype and do not emit cancel without a prior request.

The envelope sequence must remain gap-free when snapshots and events are merged by `sequence`.
