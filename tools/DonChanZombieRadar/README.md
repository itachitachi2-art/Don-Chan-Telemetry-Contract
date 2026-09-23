# Don-Chan Zombie Radar v0.1.3

7 Days to Die V3.x向けの実験用ゾンビレーダー。
Don-Chan Telemetry Probeで実証した周囲Entity取得と死亡イベントを、独立した表示Modへ派生させる実験。

## v0.1.3

- 上部タイトル `DON-CHAN Z RADAR` を削除
- 時計回りのレーダー走査線を追加
- 走査線には短い残光を追加
- Zombieの赤点は常時表示ではなく、走査線が通過した瞬間に点灯
- 点灯した赤点は徐々にフェードして消える
- Zombie光点の既定サイズを v0.1.1 の2倍（9 -> 18 px）へ変更
- 死亡 `×` マーカーの大きさ・線幅を2倍へ変更
- レーダー本体の既定サイズを1.5倍（280 -> 420 px）へ変更
- 過去の保存位置で拡大後にはみ出した場合も画面内へクランプ

## 基本仕様

- プレイヤー自身を円形レーダー中央に表示
- 水平15m圏内のZombie系Entityを検知
- Y座標（高低差）は距離計算から除外
- プレイヤー進行方向をレーダー上方向にする（既定）
- 5m間隔のグリッド/レンジリング
- Zombie死亡位置を一定時間 `×` 表示し、徐々にフェード
- F7で表示ON/OFF
- F8でゲーム内ドラッグ配置
- Telemetry Probeへの実行時依存なし

## 走査演出

走査周期の既定値は2.4秒。

```text
時計回りに走査
      ↓
走査線がZombie位置を通過
      ↓
赤い光点が点灯
      ↓
約1.5秒かけてフェードアウト
```

Zombie自体の取得は0.10秒間隔で行うが、UI上の赤点は走査線通過に同期して表示する。

設定:

```ini
SweepCycleSeconds=2.40
SweepTrailDegrees=34
BlipPersistenceSeconds=1.50
```

## 距離

距離は2D水平距離。

`distance = sqrt(dx^2 + dz^2)`

上下階にいるZombieでもXZ上で15m以内なら検知対象になる。

## インストール

1. `build-install.bat` を実行
2. `%APPDATA%\7DaysToDie\Mods\DonChanZombieRadar` にインストール
3. EAC無効でゲームを起動

ビルドのみ: `build.bat`

## 主な設定

`Settings/DonChanZombieRadar.cfg`

```ini
RangeMeters=15
DeathMarkerSeconds=2.50
RadarSize=420
GridMeters=5
ZombieDotSize=18
RotateWithPlayer=true
SweepCycleSeconds=2.40
SweepTrailDegrees=34
BlipPersistenceSeconds=1.50
```

位置はINIを直接触る必要はない。

### ゲーム内位置変更

1. F8
2. レーダーを左ドラッグ
3. F8で保存

保存位置:

```text
%APPDATA%\7DaysToDie\DonChanZombieRadar\layout.cfg
```

Modを更新・再インストールしても位置設定は残る。
