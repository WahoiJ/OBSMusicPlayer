# OBSMusicPlayer

A lightweight OBS BGM player for streamers. Randomly plays MP3s from a folder and updates an OBS text source with the current track name via WebSocket.

OBS向け自動BGMプレイヤー。指定フォルダのMP3をランダム再生し、WebSocket経由でOBSのテキストソースに曲名をリアルタイム表示します。

---

## 機能

- MP3ファイルをランダム再生
- 再生履歴を管理し、直近の曲をリピートしにくくする
- 曲名・アーティスト名をOBSのテキストソースにリアルタイム表示
- GUIから再生・停止・スキップを操作
- ボリュームスライダーによるソフトウェア音量調整
- GUIの設定パネルから各種設定を変更・保存（リビルド不要）
- ログパネルで動作状況を確認可能

---

## 必要環境

- Windows 10 / 11
- [.NET 10.0 デスクトップランタイム](https://dotnet.microsoft.com/download/dotnet/10.0)
- OBS Studio 28以降（obs-websocket 5.x 内蔵版）

---

## OBSの設定

**1. obs-websocketを有効化**
```
OBS → ツール → obs-websocket設定
→ WebSocketサーバーを有効化にチェック
→ ポート: 4455（デフォルト）
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

## セットアップ

**1. config.jsonを作成する**

`config.sample.json` を同じフォルダに `config.json` としてコピーし、内容を編集してください。

```
config.sample.json → config.json にコピー
```

```json
{
  "ObsUrl": "ws://localhost:4455",
  "ObsPassword": "your_password_here",
  "MusicFolder": "C:\\path\\to\\your\\music",
  "SourceText": "CurrentSong",
  "HistoryRatio": 0.5,
  "MaxRetries": 3,
  "Volume": 0.7
}
```

| キー | 説明 | デフォルト |
|---|---|---|
| ObsUrl | obs-websocketのURL | ws://localhost:4455 |
| ObsPassword | obs-websocketのパスワード | 空欄 |
| MusicFolder | MP3ファイルが入っているフォルダのパス | 空欄 |
| SourceText | OBSのテキストソース名 | CurrentSong |
| HistoryRatio | 直近の何割を履歴として保持するか（0.0〜1.0） | 0.5 |
| MaxRetries | 選曲のリトライ回数上限 | 3 |
| Volume | 再生音量（0.0〜1.0） | 0.7 |

> **注意:** `config.json` にはパスワードが含まれるため、Gitにコミットしないでください。`.gitignore` に追加済みです。

設定はGUIの設定パネルからも変更・保存できます。保存した内容は `config.json` に反映されます。

---

## 起動手順

1. OBS Studioを起動し、obs-websocketが有効になっていることを確認する
2. `OBSMusicPlayer.exe` を起動する
3. ログパネルに `[OBS] 接続完了` と表示されれば準備完了
4. 再生ボタンを押すと自動的にランダム再生が始まります

> OBSより先に起動すると接続に失敗します。必ずOBSを先に起動してください。

---

## 画面説明

| 要素 | 説明 |
|---|---|
| 再生 / 停止ボタン | BGMの再生・停止を切り替えます |
| スキップボタン | 現在の曲を停止し、次の曲へ進みます |
| ボリュームスライダー | ソフトウェア音量を調整します（0〜100%） |
| 設定パネル | OBS URL・パスワード・音楽フォルダなどを変更できます |
| 保存ボタン | 設定パネルの内容を `config.json` に保存します |
| ログパネル | OBS接続状況・再生中の曲名などをリアルタイム表示します |
