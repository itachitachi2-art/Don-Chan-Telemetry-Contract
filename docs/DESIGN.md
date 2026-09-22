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

- `EntityAlive.DamageEntity`
- `ItemActionAttack.Hit`
- `EntityAlive.Kill` / `OnEntityDeath`
- `EntityBuffs.AddBuff` / `RemoveBuff`

実機ログで公開Eventだけで足りると分かったものは、本番版ではHarmonyから外す。

## 6. Reflection Inspector

`relevant-members.json` は型ごとに Fields / Properties / Events / Methods + parameter signature を出す。

## 7. 安定性レベル

| Level | 入口 | 本番採用の優先度 |
|---|---|---|
| A | 公式/公開Event・安定Public member | 最優先 |
| B | Public method/property | 高 |
| C | publicized/non-public fieldをReflection | 中 |
| D | Harmony Prefix/Postfix | 必要箇所のみ |
| E | Transpiler/IL依存 | 原則避ける |

## 8. 性能方針

- Snapshot既定 1秒
- Depth既定 2
- member 96件/root
- collection 24件
- Event argumentはDepth 1
- full Assembly catalogは起動後1回のみ

## 9. マルチプレイ

client local only / replicated / server authoritative を区別する。

## 10. 次段階

実機監査を経て `Don-Chan Telemetry Contract v1` を v1.0.0 で正式固定した。Assembly-CSharpの生オブジェクト構造はAI側へ漏らさない。
