# 実機検証手順

## v1.0.0 Contract v1 acceptance

v0.1.13 の最終実機スイープを v1 acceptance として採用する。

確認済み:

- LIGHT snapshot: 294 records
- LIGHT event: 1,749 records
- snapshot + event の共有 sequence: `1..2043`
- sequence gap: 0
- duplicate sequence: 0
- sessionId: 全件一致
- false `stunned=true`: 0
- Auger: `PoweredTool / Auger`
- Auger refuel: request -> complete
- Tactical AR: `Ranged / Rifle`
- Wrench: `Tool / Wrench`
- Medicine / Boost / Food の consumable subtype
- Robotic Turret: `Deployable / RoboticTurret`
- reload complete / cancel lifecycle

v1.0.0 の最終語彙修正:

- robotic deployable actual fire: `action.deployable.fire`
- `resourceBrokenGlass`: `Consumable / Hazard`

この2点は envelope / sequence / transport semantics を変更しない。
