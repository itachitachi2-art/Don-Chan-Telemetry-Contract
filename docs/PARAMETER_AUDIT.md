# パラメータ監査表

この表は「取得できると推測した一覧」ではなく、**Probe実機ログで確認して埋める台帳**として使う。

記号:

- `未` = 未検証
- `OK` = 実機取得確認
- `△` = 取得可能だが不安定/高コスト/条件付き
- `NG` = 現方式では取得不可

## Player

| 項目 | 状態 | 推奨入口 | 備考 |
|---|---|---|---|
| Entity ID | 未 | EntityPlayer field/property | |
| Position XYZ | 未 | Entity/Transform | |
| Rotation / facing | 未 | Entity | |
| HP / Max HP | 未 | Stats | |
| Stamina / Max | 未 | Stats | |
| Food / Water等needs | 未 | Stats/CVars | V3実構造確認 |
| Level / XP | 未 | Progression | |
| Skill points | 未 | Progression/Event | `SkillPointSpent`候補 |
| Buff / Debuff | 未 | EntityBuffs | Add/Removeを照合 |
| GameStage | 未 | Player/World | |
| LootStage | 未 | Player/location calculation | |

## Item / Inventory

| 項目 | 状態 | 推奨入口 | 備考 |
|---|---|---|---|
| Holding item | 未 | Inventory | |
| Toolbelt slot | 未 | Inventory | change delegate候補 |
| Backpack | 未 | Bag | change delegate候補 |
| Stack count | 未 | ItemStack | |
| Quality | 未 | ItemValue | |
| Durability | 未 | ItemValue/Metadata | |
| Installed mods | 未 | ItemValue | |
| Ammo / magazine | 未 | holding item / action data | 実構造確認 |
| Reload state | 未 | ItemAction/held action | Harmony候補 |

## Combat / Entity

| 項目 | 状態 | 推奨入口 | 備考 |
|---|---|---|---|
| Damage event | 未 | `EntityAlive.DamageEntity` | Harmony fallback |
| Hit target | 未 | `ItemActionAttack.Hit` | Harmony fallback |
| Entity kill | 未 | `QuestEventManager.EntityKill` / GameEventManager | event優先 |
| Entity spawn | 未 | GameEventManager | |
| Entity despawn | 未 | GameEventManager | |
| Nearby hostile count | 未 | loaded World entities | polling |
| Nearest hostile distance | 未 | positions | polling |
| Entity HP | 未 | EntityAlive stats | loaded entityのみ |
| AI target/state | 未 | EntityAlive/AI fields | Reflection依存度高 |

## World / Block

| 項目 | 状態 | 推奨入口 | 備考 |
|---|---|---|---|
| World time/day | 未 | World/GameStats | |
| Biome enter | 未 | QuestEventManager.BiomeEnter | |
| Block change | 未 | QuestEventManager.BlockChange / ChunkCluster | |
| Block damage | 未 | ChunkCluster delegate / Harmony | |
| Block destroy | 未 | QuestEventManager.BlockDestroy | |
| Block place | 未 | QuestEventManager.BlockPlace | |
| Block upgrade | 未 | QuestEventManager.BlockUpgrade | |
| Container open/close | 未 | QuestEventManager | |
| POI/prefab | 未 | World/Prefab systems | catalog確認 |
| Weather | 未 | weather manager classes | catalogから型探索 |

## Craft / Quest / Progression

| 項目 | 状態 | 推奨入口 | 備考 |
|---|---|---|---|
| Craft item | 未 | QuestEventManager.CraftItem | |
| Harvest item | 未 | QuestEventManager.HarvestItem | |
| Repair item | 未 | QuestEventManager.RepairItem | |
| Scrap item | 未 | QuestEventManager.ScrapItem | |
| Use item | 未 | QuestEventManager.UseItem | |
| Wear item | 未 | QuestEventManager.WearItem | |
| Recipe unlock | 未 | CraftingManager.RecipeUnlocked | |
| Quest accepted | 未 | EntityPlayer.QuestAccepted | instance event |
| Quest changed | 未 | EntityPlayer.QuestChanged | instance event |
| Quest removed | 未 | EntityPlayer.QuestRemoved | instance event |
| Quest complete | 未 | QuestEventManager.QuestComplete | |

## Vehicle / Workstation / Trader

| 項目 | 状態 | 推奨入口 | 備考 |
|---|---|---|---|
| Vehicle type/entity | 未 | World entities | |
| Vehicle speed | 未 | Vehicle fields | catalog確認 |
| Fuel | 未 | Vehicle fields | |
| Mounted player | 未 | Entity/Vehicle | |
| Workstation fuel | 未 | TileEntityWorkstation.FuelChanged | instance購読拡張候補 |
| Workstation input | 未 | TileEntityWorkstation.InputChanged | instance購読拡張候補 |
| Craft queue | 未 | workstation object | |
| Trader entity/location | 未 | World entities/EntityTrader | |
| Trader stock/restock | 未 | TraderData | |

## 実機監査での優先順

1. Player HP/Stamina/Position/held item
2. Damage/Hit/Kill + nearby enemies
3. Toolbelt/Backpack change
4. Block Destroy/Harvest
5. Craft/Repair/Scrap/Use
6. Quest/Progression
7. Vehicle/Trader/Workstation
8. Weather/POI/AI internal state

この順でOKになった情報から実況用contractへ移す。

## 2026-09-22 real-world verification

The first successful runtime capture verified direct access to player vitals, progression, held-item state, inventory roots, world roots, block/harvest events, entity lifecycle events, combat hit/damage/kill/death, Buff changes, and multiple QuestEventManager signals. See `REALWORLD-AUDIT-2026-09-22.md` and `TELEMETRY-CONTRACT.md`.
