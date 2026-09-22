# Don-Chan Telemetry Probe v1.0.0

**正式版 v1.0.0 / Don-Chan Telemetry Contract v1** です。v0.1.x の実機監査・全装備スイープを経て、LIGHT snapshot / semantic event / contract envelope を v1 の安定境界として固定しました。

7 Days to Die V3.x の **「ゲーム内パラメータをどこまで直接取得できるか」** を実機で監査するための DLL Mod です。

目的はゲームプレイを変更することではなく、`Assembly-CSharp.dll` とランタイムオブジェクトを観測して、今後の実況・演出 Mod の入力基盤を決めることです。

## 何を出力するか

ゲーム起動後、実際に読み込まれた Mod フォルダ配下の `Telemetry/` に以下を生成します。標準のユーザーMod配置では `%APPDATA%\7DaysToDie\Mods\DonChanTelemetryProbe\Telemetry` です。

- `session.json` — 実行モード、Assembly 情報
- `capabilities.jsonl` — Event、Harmony hook、runtime root と失敗理由
- `events.jsonl` — **軽量・正規化済みイベント**
- `snapshots/latest.json` — **軽量な最新状態**（HP / Stamina / Food / Water / Level / 装備 / 周辺敵など）
- `snapshots.jsonl` — 軽量状態の時系列

`AuditMode=true` の場合だけ、追加で以下を生成します。

- `catalog/assembly-types.json` — Assembly-CSharp の全 Type 一覧
- `catalog/relevant-members.json` — 関連 Type の Field / Property / Event / Method 一覧
- `audit/events.raw.jsonl` — Reflection 展開した生イベント
- `audit/snapshots/latest.json` — 最新の重い Reflection スナップショット
- `audit/snapshots.jsonl` — 重い監査スナップショットの時系列

## 設計上の優先順位

1. **実機 Assembly を正とする** — Web上の古いAPI名を正解扱いしない。
2. **Event/Delegate を優先** — Inventory / Quest / Craft / Harvest / Block 等は polling ではなくイベントを探す。
3. **Harmony は補完** — Damage/Hit 等、十分な公開イベントが無い領域だけフックする。
4. **Reflection は監査用途** — フィールド名がバージョンで変わっても catalog から再発見できる。
5. **AIへ全量を渡さない** — 本番実況ではこのProbeの出力を直接食わせず、別レイヤで集計・差分化する。

## ビルド

通常は展開したフォルダ直下の `build.bat` をダブルクリックしてください。PowerShell の実行ポリシー変更は不要です。

コマンドから実行する場合:

```bat
build.bat
```

PowerShell を直接使う場合:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Steam の場所が異なる場合:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Game7D2D "D:\SteamLibrary\steamapps\common\7 Days To Die"
```

必要なもの:

- Windows に標準搭載されている C# コンパイラ (`csc.exe`)
- 7DTD が同梱する Mono / Unity runtime assemblies (`mscorlib.dll`, `netstandard.dll`, `System*.dll`)
- `7DaysToDie_Data\Managed\Assembly-CSharp.dll`
- `7DaysToDie_Data\Managed\LogLibrary.dll`
- `7DaysToDie_Data\Managed\UnityEngine.CoreModule.dll`
- `Mods\0_TFP_Harmony\0Harmony.dll`

**.NET SDK の追加インストールは不要です。** `build.ps1` は Windows の `csc.exe` をコンパイラ実行ファイルとしてだけ使い、`/nostdlib+` で Windows Framework の標準ライブラリ参照を切ります。型参照は現在インストールされている 7DTD の Mono / Unity runtime から自動検出します。

ゲームDLLは再配布しません。ローカルのインストールを参照してビルドします。

コンパイルに失敗した場合は、実際に `csc.exe` へ渡した引数を `build-last.rsp` に保存します。成功時は自動削除されます。

## インストール

推奨:

```text
install.bat
```

PowerShellからなら:

```powershell
.\install.ps1
```

または `mod\DonChanTelemetryProbe` を `%APPDATA%\7DaysToDie\Mods` 配下へコピーします。

ゲーム本体フォルダ直下の `Mods` はTFP同梱Mod（`0_TFP_Harmony` など）用として残し、ユーザーModは AppData 側を標準配置とします。

Harmony DLL Mod のため **EAC無効** で起動してください。

## モード

### LIGHT（既定）

`TelemetryConfig/telemetryprobe.xml` の `<AuditMode>false</AuditMode>`。
長時間動かす前提の軽量モードです。Assembly 全走査と深い Reflection dump は行いません。`SuppressNoisyEvents=true` では GameStats の高頻度通知と同一Buffの短時間連打も抑制します。

### AUDIT

API再調査やゲーム更新時だけ `<AuditMode>true</AuditMode>` にします。
Assembly catalog、raw event、深い snapshot を出すため、ログ量と負荷が大きくなります。

## 最初の実機監査

監査を取り直す場合は `AuditMode=true` にしてから:

1. 新規/既存ワールドへ入る。
2. 1分程度、以下を順番に行う。
   - 歩く / 走る / ジャンプ
   - 武器を持ち替える
   - 射撃 / 近接 / リロード
   - ゾンビへ攻撃し倒す
   - 木・石・鉄系ブロックを叩き破壊
   - アイテム取得 / 捨てる / Toolbelt移動
   - 飲食 / Buff付与
   - Craft / Scrap / Repair
   - Container開閉
   - Quest受注 / 進行可能なら進行
   - Vehicle搭乗可能なら搭乗
3. 終了後 `Telemetry/capabilities.jsonl` と `events.jsonl` を確認。
4. `catalog/relevant-members.json` と実イベントを突き合わせて、実況用の安定入力を選ぶ。

## 本番化の考え方

v0.1.8 から、通常時はすでに軽量 Telemetry を出します。深い Reflection は Audit mode に隔離しました。実況連携では `events.jsonl` と `snapshots.jsonl` の軽量出力をさらに集計・差分化して使います。

```text
7DTD
  ↓
専用 Telemetry Mod
  ↓  event / state
集計・差分レイヤ
  ↓  例: HP -32, zombieKill +1, nearbyEnemies 2→7
AItuberKit
  ↓
発話判定 → AI
```

詳細は `docs/DESIGN.md` と `docs/PARAMETER_AUDIT.md` を参照してください。

## Windows quick build

For repeated local builds, use the included batch files from the extracted root folder:

- `build.bat` — build only
- `install.bat` — install the already-built mod
- `build-install.bat` — build then install

The batch files call the PowerShell scripts with `-ExecutionPolicy Bypass`, so changing the machine-wide PowerShell execution policy is unnecessary.

## v0.1.4 build-reference fix

The Windows-SDK-free build now references the current 7DTD V3 game assemblies required by the probe:

- `Assembly-CSharp.dll`
- `0Harmony.dll`
- `LogLibrary.dll`
- `UnityEngine.CoreModule.dll`

If present, `UnityEngine.dll` and `Assembly-CSharp-firstpass.dll` are also added automatically.
The `CS1684` warnings about `Span` / `ReadOnlySpan` can be emitted by the legacy Windows `csc.exe`; they are warnings, not a build failure by themselves.


## v0.1.5 Unity runtime reference fix

V3 の Unity assemblies は .NET Standard 2.1 / Unity Mono profile を前提とするため、Windows Framework の `mscorlib.dll` と混在させると `System.Object -> netstandard 2.1` や `Span<T>` の型解決不整合が発生します。v0.1.5 では `/nostdlib+` を使用し、7DTD 同梱の `mscorlib.dll`, `netstandard.dll`, `System*.dll` を自動検出して参照します。失敗時は `build-last.rsp` と `build-env.txt` を残します。

## v0.1.6 csc /noconfig fix

Windows Framework の `csc.exe` は既定で自身の `csc.rsp` を読み込み、Windows 側の `System.dll`, `System.Core.dll`, `System.Xml.dll`, `System.Xml.Linq.dll` を暗黙参照します。7DTD 同梱ランタイムを `/nostdlib+` で明示参照する場合、これが二重参照 `CS1703` を起こします。v0.1.6 では Excuse Me Drone で実績のある方式に合わせ、`/noconfig` を **response file 内ではなく csc.exe のコマンドライン**へ渡します。

実行形: `csc.exe /noconfig @<temporary.rsp>`


## v0.1.8 light/audit split

最初の実機監査では約6分で `snapshots.jsonl` が約45 MB、`events.jsonl` が約4.7 MBまで増えました。v0.1.8 では通常出力を明示的な軽量フィールドに限定し、深い Reflection dump と raw event を `AuditMode=true` のときだけ生成します。

軽量 snapshot には Player vitals、Progression、現在装備、World/Blood Moon、Biome、ロード済みEntity概要、10/20/30m以内のZombie数と最短距離を含めます。


## v0.1.9 semantic action telemetry

DonChanActionTheater 0.5.0 の実績ある V3.2 フックを参照し、LIGHT モードへ高信頼の行動イベントを追加しました。

- 食事/医療品の消費完了 (`action.consume.complete`)
- 近接攻撃と武器種 (`action.melee`)
- 実際に発射された射撃と武器種 (`action.ranged.fire`)
- 成功したクラフト出力 (`action.craft.complete`)
- 成立した乗車と車種 (`action.vehicle.enter`)

射撃は `ItemActionRanged.onHoldingEntityFired` を使うため、空マガジンのクリックを射撃として記録しません。v0.1.13 では Action Theater のフック位置は再利用しつつ、実況向けの意味分類は Telemetry Contract 側で正規化します。

Reference: `itachitachi2-art/DonChanActionTheater`, main commit `8301a33b1af1cbb2312fbf0d54b8b1fefa9fc9d1`.

## v0.1.10 gameplay fields

Light snapshots now also expose the gameplay context needed before freezing Don-Chan Telemetry Contract v1:

- `weapon`: loaded ammo, effective magazine size, selected ammo item, reserve ammo, reload state
- `focus`: the entity or block currently under the player's game ray hit, including distance and block HP when available
- `movement`: velocity, speed, running/jumping/in-air/mounted state
- `vehicle`: mounted vehicle type, speed, fuel, health and storage capability
- `status`: normalized important debuffs/conditions derived from active buffs
- `quest`: tracked/active quest, POI, objective summaries and distance
- `world.nearby`: 5/10/15/20/30 m zombie counts plus zombies currently targeting the player
- semantic reload events: `action.reload.request`, `action.reload.complete`, `action.reload.cancel`

These are compact runtime summaries. Raw members remain an AUDIT-mode concern. Fields are intentionally nullable: a missing field means that the corresponding runtime object/member was unavailable, not that the game state is false or zero.


## v0.1.11 normalization + Contract envelope

- Adds `schemaVersion`, `sessionId`, monotonic `sequence`, and `observedAt` to light event/snapshot records.
- Separates raw `runModeActive` from derived `moving / walking / running / locomotion`.
- Normalizes auger/chainsaw gas usage as `poweredTool` + `fuel` instead of firearm ammo.
- Replaces broad `stun` substring matching with a conservative stun-buff allowlist after an observed false positive from `buffPowerAttackStaminaStunt`.

v1.0.0 は **Don-Chan Telemetry Contract v1 の正式版**です。


## v0.1.12 semantic normalization correction

実機 v0.1.11 ログで、Snapshot 側ではオーガーが `poweredTool` と正規化できている一方、
Action Theater 由来の semantic hook では `ItemActionRanged.onHoldingEntityFired` を共有するため
`action.ranged.fire / Generic` として出ていたことを確認しました。

- オーガー等の motor tool は `action.poweredTool.use` として記録。
- チェーンソーは v0.1.12 では `action.melee / Chainsaw`。v0.1.13 から `action.poweredTool.use / Chainsaw` へ統一。
- powered tool の燃料補給は `action.refuel.request|complete|cancel`。
- `CancelReload` は、実際に `requestReload` を観測した同一 action-data に対してのみ complete/cancel を出す。
- オーガーやネイルガンの「実リロード無しの cancel」ノイズを除去する。

Contract envelope (`schemaVersion/sessionId/sequence/observedAt`) は変更しません。


## v1.0.0 formal release

v0.1.13 の実機スイープを受け、Contract v1 を正式固定しました。

最終語彙修正:

- ロボスレッジ / ロボタレットの実発火: `action.deployable.fire`
- `resourceBrokenGlass`: `Consumable / Hazard`

LIGHT snapshot と semantic event の共通 envelope は `schemaVersion=1 / sessionId / sequence / observedAt` を使用します。AITuber 側は raw event 全量ではなく、LIGHT snapshot + semantic event を主入力として集計・差分化する前提です。

正式仕様は `docs/TELEMETRY-CONTRACT.md` を参照してください。
