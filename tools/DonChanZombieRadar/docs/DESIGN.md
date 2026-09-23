# Don-Chan Zombie Radar 設計メモ

## 目的
Telemetry監査で取得可能と確認した内部情報を、実際の独立Modへ再利用できることを検証する。

## データ経路

7DTD World
→ EntityAlivesを列挙
→ EntityAliveかつ型名にZombieを含むものを抽出
→ PlayerとのXZ差分
→ 15m円内へ投影
→ Unity IMGUIで描画

死亡時:
EntityAlive.OnEntityDeath
→ Zombieのみ死亡座標を保存
→ DeathMarkerSecondsだけ×表示

## 座標変換

`delta = zombie.position - player.position`

高さを無視し、`delta.x / delta.z` のみ使用。

RotateWithPlayer=true の場合:
- 横 = dot(delta, playerRight)
- 縦 = dot(delta, playerForward)

レーダーではforwardを上方向へ反転描画する。

## 将来候補

- 高低差表示（▲/▼）
- Sleeper / 未起動Zombieの扱い
- 特殊Zombie別アイコン
- 距離ごとの点サイズ/明滅
- ホード時の密集表現
- 画面ドラッグによる位置変更
- UIサイズのゲーム内変更


## Layout persistence

F8 toggles an in-game placement mode. Dragging updates the radar top-left position, clamps it to the current screen, and writes user layout state to `%APPDATA%\7DaysToDie\DonChanZombieRadar\layout.cfg`. This state is intentionally kept outside the mod directory so `build-install.bat` can replace the mod without destroying user placement.


## v0.1.2 sweep presentation

- Radar default size: 420 px (1.5x previous default)
- Zombie blips are ping-driven, not continuously visible.
- Sweep rotates clockwise.
- A blip lights when the sweep crosses the entity's current 2D bearing.
- Blip visibility fades for `BlipPersistenceSeconds`.
- Death crosses remain event-driven and are rendered at 2x the previous size.
- The top title caption was removed; the compact bottom range/count label remains.


## v0.1.3 sweep tuning

The sweep is intentionally subdued: the sweep communicates scan timing, while detected zombie blips are the primary visual signal.
