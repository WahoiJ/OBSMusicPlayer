using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace OBSMusicPlayer;

public partial class MainWindow : Window
{
    private AppConfig _config;
    private MusicPlayerService? _player;

    public MainWindow()
    {
        InitializeComponent();
        _config = AppConfig.Load();
        LoadSettingsToUI();
        EnsurePlayer();
    }

    private void LoadSettingsToUI()
    {
        ObsUrlBox.Text = _config.ObsUrl;
        ObsPasswordBox.Password = _config.ObsPassword;
        MusicFolderBox.Text = _config.MusicFolder;
        SourceTextBox.Text = _config.SourceText;
        HistoryRatioSlider.Value = _config.HistoryRatio;
        VolumeSlider.Value = _config.Volume;
        VolumeLabel.Text = $"{(int)(_config.Volume * 100)}%";
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "音楽フォルダを選択",
            InitialDirectory = string.IsNullOrEmpty(MusicFolderBox.Text)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
                : MusicFolderBox.Text
        };
        if (dialog.ShowDialog() == true)
            MusicFolderBox.Text = dialog.FolderName;
    }

    private void PlayStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_player?.IsPlaying == true)
            _player.Stop();
        else
        {
            EnsurePlayer();
            _player!.Start();
        }
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        EnsurePlayer();
        _player!.PlayNext();
    }

    private void EnsurePlayer()
    {
        if (_player != null) return;
        _player = new MusicPlayerService(_config);
        SubscribeEvents();
        _player.Connect();
    }

    private void SubscribeEvents()
    {
        if (_player == null) return;

        _player.TrackChanged += info => Dispatcher.Invoke(() =>
        {
            NowPlayingText.Text = info.DisplayText;
            DurationText.Text = $"{info.DurationSeconds:F1} 秒";
        });

        _player.StatusUpdated += msg => Dispatcher.Invoke(() => AppendLog(msg));

        _player.ConnectionChanged += connected => Dispatcher.Invoke(() =>
        {
            ObsStatusDot.Fill = connected
                ? new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1))
                : new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
            ObsStatusText.Text = connected ? "OBS 接続中" : "OBS 未接続";
        });

        _player.PlaybackStateChanged += playing => Dispatcher.Invoke(() =>
        {
            PlayStopButton.Content = playing ? "⏹  停止" : "▶  再生";
            PlayStopButton.Background = playing
                ? new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8))
                : new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
        });
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeLabel == null) return;
        var volume = (float)e.NewValue;
        VolumeLabel.Text = $"{(int)(volume * 100)}%";
        // 再生中の場合はリアルタイムで音量変更
        _player?.SetVolume(volume);
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _config.ObsUrl = ObsUrlBox.Text.Trim();
        _config.ObsPassword = ObsPasswordBox.Password;
        _config.MusicFolder = MusicFolderBox.Text.Trim();
        _config.SourceText = SourceTextBox.Text.Trim();
        _config.HistoryRatio = HistoryRatioSlider.Value;
        _config.Volume = (float)VolumeSlider.Value;
        _config.Save();

        _player?.Dispose();
        _player = null;

        NowPlayingText.Text = "---";
        DurationText.Text = "";
        PlayStopButton.Content = "▶  再生";
        PlayStopButton.Background = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));

        EnsurePlayer();
        AppendLog("[Config] 設定を保存しました。");
    }

    private void HistoryRatioSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HistoryRatioLabel != null)
            HistoryRatioLabel.Text = e.NewValue.ToString("F1");
    }

    private void AppendLog(string message)
    {
        LogText.Text = $"[{DateTime.Now:HH:mm:ss}] {message}\n" + LogText.Text;
        var lines = LogText.Text.Split('\n');
        if (lines.Length > 150)
            LogText.Text = string.Join('\n', lines[..150]);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        => _player?.Dispose();
}