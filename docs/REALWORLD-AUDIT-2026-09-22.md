# Real-world audit — 2026-09-22

First successful v0.1.7 runtime capture on 7 Days to Die V3.x.

## Capture size

| File | Size |
|---|---:|
| snapshots.jsonl | 45,484,671 bytes |
| events.jsonl | 4,660,234 bytes |
| capabilities.jsonl | 167,107 bytes |
| catalog/assembly-types.json | 1,180,029 bytes |
| catalog/relevant-members.json | 19,183,996 bytes |

The recursive snapshot format was excellent for discovery but too large for continuous telemetry. This capture directly motivated the v0.1.8 Light/Audit split.

## Event capture

6,659 events were captured:

- delegate events: 4,025
- Harmony postfix events: 2,634

Most frequent:

| Event | Count |
|---|---:|
| buff.remove | 1,793 |
| Bag.OnBackpackItemsChangedInternal | 716 |
| GameStats.OnChangedDelegates | 714 |
| QuestEventManager.AddItem | 644 |
| QuestEventManager.HarvestItem | 633 |
| Inventory.OnToolbeltItemsChangedInternal | 477 |
| buff.add | 440 |
| combat.hit | 365 |
| QuestEventManager.WindowChanged | 235 |
| QuestEventManager.BlockChange | 149 |
| World.EntityLoadedDelegates | 141 |
| World.EntityUnloadedDelegates | 141 |
| QuestEventManager.BlockDestroy | 88 |
| combat.damage | 25 |
| combat.kill | 6 |

v0.1.8 suppresses `GameStats.OnChangedDelegates` from the normalized stream by default and rate-limits repeated identical Buff events. Audit raw events remain available when `AuditMode=true`.

## Runtime roots verified

The capture successfully resolved:

- World
- EntityPlayerLocal
- Inventory
- Bag
- Equipment
- EntityBuffs
- Progression
- PlayerEntityStats
- QuestJournal
- ItemValue for the held item

Observed player-state examples included HP/max HP, stamina/max stamina, food, water, level, skill points, aiming/crouching flags, biome, position, held item quality and durability.

## Event semantics verified

The capture demonstrated that the following can be obtained without screen analysis:

- item harvest and quantity
- block destroy/change and block coordinates
- inventory/toolbelt/backpack changes
- crafting/use/hold events
- entity load/unload
- combat hit/damage/kill/death
- Buff add/remove
- quest-related events

`ItemActionAttack.Hit` exposed useful semantic values such as block hit, harvest-tool flag, damage given/max, material category, critical flag, entity hit, and block being damaged.

## Catalog result

- Assembly-CSharp types enumerated: 7,578
- Relevant types deeply described: 2,830

This catalog is now considered an audit artifact, not a continuous runtime output.
