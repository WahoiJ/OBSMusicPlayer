using System.IO;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using TagLib;

namespace OBSMusicPlayer;

public record TrackInfo(string DisplayText, double DurationSeconds);

public class MusicPlayerService : IDisposable
{
    private readonly AppConfig _config;
    private readonly OBSWebsocket _obs;

    private IWavePlayer? _waveOut;
    private AudioFileReader? _audioReader;

    private readonly Queue<string> _history = new();
    private readonly object _lock = new();
    private volatile bool _manualStop = false;
    private bool _disposed = false;

    public event Action<TrackInfo>? TrackChanged;
    public event Action<string>? StatusUpdated;
    public event Action<bool>? ConnectionChanged;
    public event Action<bool>? PlaybackStateChanged;

    public bool IsPlaying { get; private set; }

    /// <summary>再生中の音量をリアルタイムで変更する（0.0〜1.0）</summary>
    public void SetVolume(float volume)
    {
        var clamped = Math.Clamp(volume, 0f, 1f);
        _config.Volume = clamped;
        if (_audioReader != null)
            _audioReader.Volume = clamped;
    }

    public MusicPlayerService(AppConfig config)
    {
        _config = config;
        _obs = new OBSWebsocket();
        _obs.Connected += (s, e) =>
        {
            StatusUpdated?.Invoke("[OBS] 接続完了");
            ConnectionChanged?.Invoke(true);
        };
        _obs.Disconnected += (s, e) =>
        {
            StatusUpdated?.Invoke("[OBS] 切断");
            ConnectionChanged?.Invoke(false);
        };
    }

    /// <summary>OBSへの接続を開始する。MainWindowでSubscribeEvents後に呼ぶ。</summary>
    public void Connect()
    {
        try
        {
            _obs.ConnectAsync(_config.ObsUrl, _config.ObsPassword);
            StatusUpdated?.Invoke($"[OBS] 接続試行中: {_config.ObsUrl}");
        }
        catch (Exception ex)
        {
            StatusUpdated?.Invoke($"[OBS] 接続エラー: {ex.Message}");
        }
    }

    public void Start() => PlayNext();

    public void PlayNext()
    {
        // OBS未接続の場合はスキップのタイミングで再接続を試みる
        if (!_obs.IsConnected)
        {
            StatusUpdated?.Invoke("[OBS] 未接続のため再接続を試みます...");
            Connect(); // 非同期なので接続完了を待たずに再生は続行する
        }

        lock (_lock)
        {
            _manualStop = true;
            StopCurrent();
            _manualStop = false;

            var mp3Path = GetRandomMp3Smart();
            if (mp3Path == null)
            {
                StatusUpdated?.Invoke("[Error] 再生可能な曲が見つかりません");
                IsPlaying = false;
                PlaybackStateChanged?.Invoke(false);
                return;
            }

            var info = GetTrackInfo(mp3Path);
            StatusUpdated?.Invoke($"♪ {info.DisplayText} ({info.DurationSeconds:F1}秒)");
            TrackChanged?.Invoke(info);

            try
            {
                _audioReader = new AudioFileReader(mp3Path);
                _audioReader.Volume = _config.Volume;
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_audioReader);
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Play();

                IsPlaying = true;
                PlaybackStateChanged?.Invoke(true);
                UpdateObsText($"♪ {info.DisplayText}");
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke($"[Error] 再生失敗 ({Path.GetFileName(mp3Path)}): {ex.Message}");
                StopCurrent();
                IsPlaying = false;
                PlaybackStateChanged?.Invoke(false);
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _manualStop = true;
            StopCurrent();
            IsPlaying = false;
            PlaybackStateChanged?.Invoke(false);
            UpdateObsText("");
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_manualStop) return;
        StatusUpdated?.Invoke("[Auto] 再生終了 → 次の曲へ");
        Task.Run(PlayNext);
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
                _obs.SetInputSettings(_config.SourceText, new JObject { ["text"] = text });
                StatusUpdated?.Invoke($"[OBS] テキスト更新: {text}");
            }
            else
            {
                StatusUpdated?.Invoke("[OBS] 未接続のためテキスト更新をスキップ");
            }
        }
        catch (Exception ex) { StatusUpdated?.Invoke($"[OBS] 更新エラー: {ex.Message}"); }
    }

    private string? GetRandomMp3Smart()
    {
        if (string.IsNullOrEmpty(_config.MusicFolder))
        {
            StatusUpdated?.Invoke("[Warning] フォルダ設定をしてください");
            return null;
        }
        try
        {
            var allFiles = Directory.GetFiles(_config.MusicFolder)
                .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (allFiles.Count == 0) return null;

            var maxHistoryLen = (int)(allFiles.Count * _config.HistoryRatio);
            while (_history.Count > maxHistoryLen)
                _history.Dequeue();

            var rnd = new Random();
            string? selected = null;

            for (int i = 0; i < _config.MaxRetries; i++)
            {
                var candidate = allFiles[rnd.Next(allFiles.Count)];
                if (!_history.Contains(candidate)) { selected = candidate; break; }
            }

            if (selected == null)
            {
                StatusUpdated?.Invoke("[Warning] リトライ上限到達: 履歴にある曲を再利用");
                selected = allFiles[rnd.Next(allFiles.Count)];
            }

            _history.Enqueue(selected);
            StatusUpdated?.Invoke($"[選曲] {Path.GetFileName(selected)}");
            return selected;
        }
        catch (DirectoryNotFoundException)
        {
            StatusUpdated?.Invoke($"[Error] フォルダが見つかりません: {_config.MusicFolder}");
            return null;
        }
    }

    private TrackInfo GetTrackInfo(string filePath)
    {
        try
        {
            using var tag = TagLib.File.Create(filePath, TagLib.ReadStyle.Average);
            var title = tag.Tag.Title;
            var artist = tag.Tag.FirstPerformer;
            var duration = tag.Properties.Duration.TotalSeconds;

            var display = (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(artist))
                ? $"{title} / {artist}"
                : !string.IsNullOrEmpty(title) ? title
                : Path.GetFileNameWithoutExtension(filePath);

            return new TrackInfo(display, duration);
        }
        catch
        {
            try
            {
                using var r = new AudioFileReader(filePath);
                return new TrackInfo(Path.GetFileNameWithoutExtension(filePath), r.TotalTime.TotalSeconds);
            }
            catch { return new TrackInfo(Path.GetFileNameWithoutExtension(filePath), 0); }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        try { _obs.Disconnect(); } catch { }
    }
}