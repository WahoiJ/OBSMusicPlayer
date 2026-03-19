using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NAudio.Wave;
using TagLib;
using OBSWebsocketDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using File = System.IO.File;

// =============================================================================
// メイン（トップレベルステートメント）
// =============================================================================
AppConfig config = AppConfig.Load("config.json");

Console.WriteLine($"[Config] OBS URL     : {config.ObsUrl}");
Console.WriteLine($"[Config] Music Folder: {config.MusicFolder}");
Console.WriteLine($"[Config] Source Text : {config.SourceText}");
Console.WriteLine($"[Config] HistoryRatio: {config.HistoryRatio}");

OBSMusicPlayer player = new OBSMusicPlayer(config);
player.Start();

Console.WriteLine("=== 自動DJシステム稼働中 ===");
Console.WriteLine("Enterキーで次の曲へスキップ / Ctrl+C で終了");

while (true)
{
    Console.ReadLine();
    Console.WriteLine("[手動スキップ]");
    player.PlayNext();
}

// =============================================================================
// 設定クラス
// =============================================================================
public class AppConfig
{
    public string ObsUrl { get; set; } = "ws://localhost:4455";
    public string ObsPassword { get; set; } = "";
    public string MusicFolder { get; set; } = "";
    public string SourceText { get; set; } = "CurrentSong";
    public double HistoryRatio { get; set; } = 0.5;
    public int MaxRetries { get; set; } = 3;

    public static AppConfig Load(string path = "config.json")
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"[Config] {path} が見つかりません。デフォルト設定で起動します。");
            return new AppConfig();
        }

        var json = File.ReadAllText(path);
        var config = JsonConvert.DeserializeObject<AppConfig>(json);

        if (config == null)
        {
            Console.WriteLine("[Config] 読み込みに失敗しました。デフォルト設定で起動します。");
            return new AppConfig();
        }

        Console.WriteLine($"[Config] 読み込み完了: {path}");
        return config;
    }
}

// =============================================================================
// OBSMusicPlayer クラス
// =============================================================================
public class OBSMusicPlayer
{
    private readonly AppConfig _config;
    private readonly OBSWebsocket _obs;

    private IWavePlayer? _waveOut;
    private AudioFileReader? _audioReader;

    private readonly Queue<string> _history = new();
    private readonly object _lock = new();
    private bool _manualStop = false;

    public OBSMusicPlayer(AppConfig config)
    {
        _config = config;

        _obs = new OBSWebsocket();
        _obs.Connected += (s, e) => Console.WriteLine("[OBS] 接続完了");
        _obs.Disconnected += (s, e) => Console.WriteLine("[OBS] 切断");
        _obs.ConnectAsync(_config.ObsUrl, _config.ObsPassword);

        Thread.Sleep(2000);
    }

    public void Start()
    {
        PlayNext();
    }

    public void PlayNext()
    {
        lock (_lock)
        {
            _manualStop = true;
            StopCurrent();
            _manualStop = false;

            var mp3Path = GetRandomMp3Smart();
            if (mp3Path == null)
            {
                Console.WriteLine("[Error] 再生可能な曲が見つかりません");
                return;
            }

            var displayText = FormatTrackInfo(mp3Path);
            var duration = GetDuration(mp3Path);

            Console.WriteLine($"♪ Now Playing: {displayText} ({duration:F1}秒)");

            _audioReader = new AudioFileReader(mp3Path);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            _waveOut.Play();

            UpdateObsText($"♪ {displayText}");
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_manualStop) return;
        Console.WriteLine("[Event] 再生終了 → 次の曲へ");
        PlayNext();
    }

    private void StopCurrent()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _audioReader?.Dispose();
        _audioReader = null;
    }

    private void UpdateObsText(string text)
    {
        try
        {
            if (_obs.IsConnected)
            {
                var settings = new JObject { ["text"] = text };
                _obs.SetInputSettings(_config.SourceText, settings);
                Console.WriteLine($"[OBS] テキスト更新: {text}");
            }
            else
            {
                Console.WriteLine("[OBS] 未接続のためテキスト更新をスキップ");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[OBS] テキスト更新エラー: {e.Message}");
        }
    }

    private string? GetRandomMp3Smart()
    {
        try
        {
            var allFiles = Directory.GetFiles(_config.MusicFolder, "*.mp3").ToList();
            if (allFiles.Count == 0) return null;

            var maxHistoryLen = (int)(allFiles.Count * _config.HistoryRatio);
            while (_history.Count > maxHistoryLen)
                _history.Dequeue();

            var rnd = new Random();
            string? selected = null;

            for (int i = 0; i < _config.MaxRetries; i++)
            {
                var candidate = allFiles[rnd.Next(allFiles.Count)];
                if (!_history.Contains(candidate))
                {
                    selected = candidate;
                    break;
                }
            }

            if (selected == null)
            {
                Console.WriteLine("[Warning] リトライ上限到達: 履歴にある曲を再利用します");
                selected = allFiles[rnd.Next(allFiles.Count)];
            }

            _history.Enqueue(selected);
            Console.WriteLine($"[選曲] {Path.GetFileName(selected)}");
            return selected;
        }
        catch (DirectoryNotFoundException)
        {
            Console.WriteLine($"[Error] フォルダが見つかりません: {_config.MusicFolder}");
            return null;
        }
    }

    private string FormatTrackInfo(string filePath)
    {
        try
        {
            var tag = TagLib.File.Create(filePath);
            var title = tag.Tag.Title;
            var artist = tag.Tag.FirstPerformer;

            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(artist))
                return $"{title} / {artist}";
            if (!string.IsNullOrEmpty(title))
                return title;

            return Path.GetFileNameWithoutExtension(filePath);
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }
    }

    private double GetDuration(string filePath)
    {
        try
        {
            var tag = TagLib.File.Create(filePath);
            return tag.Properties.Duration.TotalSeconds;
        }
        catch
        {
            using var reader = new AudioFileReader(filePath);
            return reader.TotalTime.TotalSeconds;
        }
    }
}