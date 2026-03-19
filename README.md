# OBSMusicPlayer

A lightweight OBS music auto-player. Randomly plays MP3s and updates OBS text source via WebSocket.

OBS向け自動BGMプレイヤー。MP3をランダム再生しながらWebSocket経由でOBSのテキストソースを更新します。

---

## 機能

- MP3ファイルをランダム再生
- 再生履歴を管理し、直近の曲をリピートしにくくする
- 曲名・アーティスト名をOBSのテキストソースにリアルタイム表示
- Enterキーで次の曲へスキップ
- `config.json` で設定を管理（リビルド不要）

---

## 必要環境

- Windows 10 / 11
- .NET 6.0 以降
- OBS Studio（obs-websocket 5.x 内蔵版）
- 以下のNuGetパッケージ

```
Install-Package NAudio
Install-Package TagLibSharp
Install-Package obs-websocket-dotnet
```

---

## OBSの設定

**1. obs-websocketを有効化**
```
OBS → ツール → obs-websocket設定
→ WebSocketサーバーを有効化にチェック
→ ポート: 4455
→ パスワードを設定してメモしておく
```

**2. テキストソースを追加**
```
ソース → + → テキスト(GDI+)
→ 名前: CurrentSong（config.jsonのSourceTextと合わせる）
```

**3. 音声の設定**
```
OBS音声ミキサー → デスクトップ音声が有効になっていることを確認
```
本アプリはデスクトップ音声として出力するため、OBS側でデスクトップ音声をキャプチャしていれば自動的に乗ります。

---

## config.jsonの設定

`config.sample.json` を `config.json` にコピーして編集してください。

```json
{
  "ObsUrl": "ws://localhost:4455",
  "ObsPassword": "your_password_here",
  "MusicFolder": "C:\\path\\to\\your\\music",
  "SourceText": "CurrentSong",
  "HistoryRatio": 0.5,
  "MaxRetries": 3
}
```

| キー | 説明 | デフォルト |
|---|---|---|
| ObsUrl | obs-websocketのURL | ws://localhost:4455 |
| ObsPassword | obs-websocketのパスワード | 空欄 |
| MusicFolder | MP3ファイルが入っているフォルダのパス | 空欄 |
| SourceText | OBSのテキストソース名 | CurrentSong |
| HistoryRatio | 直近の何割を履歴として保持するか | 0.5 |
| MaxRetries | 選曲のリトライ回数上限 | 3 |

> **注意:** `config.json` にはパスワードが含まれるため、Gitにコミットしないでください。`.gitignore` に追加済みです。

---

## 起動手順

1. OBS Studioを起動する
2. `OBSMusicPlayer.exe` を起動する
3. コンソールに `[OBS] 接続完了` と表示されれば準備完了
4. 自動的に再生が始まります

> OBSより先に起動すると接続に失敗します。必ずOBSを先に起動してください。
