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

- `catalog/assembly-types.json`
- `catalog/relevant-members.json`
- `audit/events.raw.jsonl`
- `audit/snapshots/latest.json`
- `audit/snapshots.jsonl`

## 設計上の優先順位

1. **実機 Assembly を正とする**
2. **Event/Delegate を優先**
3. **Harmony は補完**
4. **Reflection は監査用途**
5. **AIへ全量を渡さない**

## ビルド

通常は展開したフォルダ直下の `build.bat` をダブルクリックしてください。

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

**.NET SDK の追加インストールは不要です。** Windows の `csc.exe` をコンパイラとして使い、`/nostdlib+` と `/noconfig` で現在の 7DTD 同梱 Mono / Unity runtime assemblies を参照します。

## インストール

```text
install.bat
```

または `build-install.bat` でビルドとインストールを一括実行できます。

Harmony DLL Mod のため **EAC無効** で起動してください。

## Contract v1

LIGHT snapshot と semantic event の共通 envelope は次を正式仕様とします。

```json
{
  "schemaVersion": 1,
  "sessionId": "...",
  "sequence": 1234,
  "observedAt": "2026-09-22T..."
}
```

AITuber 側は raw event 全量ではなく、**LIGHT snapshot + semantic event** を主入力として集計・差分化する前提です。

正式仕様は [docs/TELEMETRY-CONTRACT.md](docs/TELEMETRY-CONTRACT.md) を参照してください。

## v1.0.0 final vocabulary

- robotic sled / turret actual fire: `action.deployable.fire`
- broken glass consumption: `Consumable / Hazard`
- semantic source: `DonChanTelemetryProbe`

詳細な変更履歴は [CHANGELOG.md](CHANGELOG.md) を参照してください。
