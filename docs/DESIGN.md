# 設計書 — Don-Chan Telemetry Probe

## 1. 目的

7 Days to Die の画面を画像解析して状態を推測するのではなく、ゲーム内部の保持値・イベントを直接取り出せる範囲を **実機で確定** する。

本Modは監査用。最終的な実況連携Modではない。

## 2. 非目標

- ゲームバランス変更
- AIへの直接送信
- 全オブジェクトを毎フレームJSON化
- 未ロードChunk/Entityの取得
- サーバーがクライアントへ同期していない秘密状態の取得

## 3. アーキテクチャ

```text
IModApi.InitMod
    │
    ├─ ProbeConfig
    ├─ AssemblyCatalog ───────→ catalog/*.json
    ├─ EventSubscriber ───────→ events.jsonl
    │     ├─ static EventInfo / delegate field
    │     └─ player/world/inventory instance events
    ├─ DynamicPatchRegistrar ─→ events.jsonl
    │     ├─ EntityAlive.DamageEntity
    │     ├─ EntityAlive.Kill / OnEntityDeath
    │     ├─ ItemActionAttack.Hit
    │     └─ EntityBuffs Add/Remove
    └─ SnapshotCollector ─────→ snapshots/*.json
          ├─ GameManager
          ├─ World
          ├─ Primary Player
          ├─ Inventory / Bag / Equipment
          ├─ Buffs / Progression / Stats
          └─ QuestJournal
```

`GameManager.Update` の Postfix はタイマー駆動の入口としてだけ使う。重い処理を毎フレームは行わず、設定間隔を過ぎた時だけ実行する。

## 4. なぜ動的購読か

7DTDではイベントの多くが通常のC# `event` だけでなく delegate field として存在する。さらにバージョンで可視性や宣言位置が変わりうる。

そのため以下を実行時に判定する。

1. `EventInfo` があれば `AddEventHandler`
2. delegate Field があれば `Delegate.Combine`
3. どちらも無ければ `capabilities.jsonl` に `not-found` を記録

Delegateの引数型は事前にコンパイル時決め打ちせず、Expression Treeで同じ型のDelegateを生成し `object[]` へ箱詰めして共通Sinkへ渡す。

## 5. Harmonyの役割

イベントで十分取れる箇所はHarmonyを増やさない。

初期版の補完対象:

- `EntityAlive.DamageEntity` — 被ダメ/与ダメの入口候補
- `ItemActionAttack.Hit` — ヒット処理候補
- `EntityAlive.Kill` / `OnEntityDeath` — kill/deathの照合
- `EntityBuffs.AddBuff` / `RemoveBuff` — Buff変化の照合

実機ログで公開Eventだけで足りると分かったものは、本番版ではHarmonyから外す。

## 6. Reflection Inspector

監査時は「値」だけでなく「どの型のどのmemberから取れそうか」が重要。

`relevant-members.json` は型ごとに以下を出す。

- Fields
- Properties
- Events
- Methods + parameter signature

ランタイムSnapshotは循環参照を `$id/$ref` で切り、深さ・member数・collection件数を上限化する。

## 7. 安定性レベル

| Level | 入口 | 本番採用の優先度 |
|---|---|---|
| A | 公式/公開Event・安定Public member | 最優先 |
| B | Public method/property | 高 |
| C | publicized/non-public fieldをReflection | 中 |
| D | Harmony Prefix/Postfix | 必要箇所のみ |
| E | Transpiler/IL依存 | 原則避ける |

監査後、各欲しい情報にA〜Eを付与する。

## 8. 性能方針

監査Modは本番Modより重い。それでもフリーズを避けるため:

- Snapshot既定 1秒
- Depth既定 2
- member 96件/root
- collection 24件
- Event argumentはDepth 1
- full Assembly catalogは起動後1回のみ

本番実況版はイベント中心にして、状態pollingは 2〜5Hz 程度の必要値だけに削る。

## 9. マルチプレイ

クライアント側で見える値とサーバー authoritative な値は一致しない場合がある。

監査結果には少なくとも以下を分ける。

- client local only: camera/input/HUD/local player presentation
- replicated: Entity/HP/position等、同期されている状態
- server authoritative: quest/reward/world mutation等の確定値

実況目的ならローカルクライアント値で十分なものが多いが、ゲーム改変や厳密な判定に使う場合はserver側でも同じProbeを試す。

## 10. 次段階

実機監査を経て `Don-Chan Telemetry Contract v1` を v1.0.0 で正式固定した。

例:

```json
{
  "seq": 1024,
  "gameTime": 183420,
  "player": {"hp":62,"stamina":74,"weapon":"gunShotgunT2"},
  "nearby": {"hostileCount":7,"nearestMeters":3.4},
  "delta": [{"type":"damage","amount":23},{"type":"entityKill","entity":"zombieArlene"}]
}
```

このcontractだけをAItuberKitへ渡す。Assembly-CSharpの生オブジェクト構造はAI側へ漏らさない。

## v0.1.8: Light / Audit separation

The first real-world capture showed that recursive snapshots were excellent for discovery but unsuitable for continuous use (tens of MB in minutes). The runtime is therefore split:

- Light path: explicit state contract + normalized event contract.
- Audit path: assembly/member catalogs + deep reflection snapshots + raw event arguments.

Audit is opt-in. The light path is now the default and is the intended input for a future aggregation layer / AItuberKit bridge.

## v0.1.9: Semantic action layer from DonChanActionTheater

The raw game event layer is intentionally kept, but it does not always express player intent cleanly. v0.1.9 adds a small semantic layer based on the V3.2 hooks already proven by DonChanActionTheater 0.5.0.

The semantic layer emits only local-player actions and favors completion/firing callbacks over button-down guesses:

- `ItemActionEat.consume` -> completed Food/Healing use
- `ItemActionMelee.ExecuteAction` -> melee action
- `ItemActionDynamicMelee.ExecuteAction` -> V3.2 dynamic melee action
- `ItemActionRanged.onHoldingEntityFired` -> actual successful ranged fire
- `XUiC_RecipeStack.outputStack` -> successful craft output
- `EntityVehicle.EnterVehicle` -> completed local-player vehicle attachment

These are additive to the delegate/Harmony audit stream. Consumers that need commentary-oriented meaning should prefer `kind=semantic` for these actions while retaining lower-level events for detailed diagnostics.
