using LibVLCSharp.Shared;
using SBtools.Models;
using System;
using System.IO;
using System.Linq;

namespace SBtools.Services;

/// <summary>
/// 全局点击音效服务。用 LibVLCSharp 播放 wav，支持音量调节和音效切换。
/// </summary>
public sealed class ClickSoundService : IDisposable
{
    private static readonly Lazy<ClickSoundService> _lazy = new(() => new ClickSoundService());
    public static ClickSoundService Instance => _lazy.Value;

    private LibVLC? _libVLC;
    private MediaPlayer? _player;
    private Media? _media;
    private readonly object _lock = new();
    private DateTime _lastPlayTime = DateTime.MinValue;

    private string? _currentFilePath;

    private const int ThrottleMs = 80;

    public static string SoundsDir =>
        Path.Combine(AppContext.BaseDirectory, "Sounds");

    private ClickSoundService() { }

    /// <summary>获取 Sounds 目录下所有 .wav 文件名。</summary>
    public static string[] GetAvailableSounds()
    {
        try
        {
            if (!Directory.Exists(SoundsDir)) return Array.Empty<string>();
            return Directory.GetFiles(SoundsDir, "*.wav")
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    private void EnsureInit()
    {
        var fileName = ConfigManager.Instance.ClickSoundFile;
        var path = Path.Combine(SoundsDir, fileName);

        if (!File.Exists(path))
        {
            LogService.Log($"音效文件不存在: {path}", "音效");
            return;
        }

        // 如果当前加载的就是同一个文件，跳过
        if (_player != null && _currentFilePath == path) return;

        lock (_lock)
        {
            if (_player != null && _currentFilePath == path) return;

            try
            {
                // 切换文件时先释放旧的
                _player?.Stop();
                _media?.Dispose();
                _player?.Dispose();
                _libVLC?.Dispose();

                _libVLC = new LibVLC("--no-video", "--quiet", "--no-video-title-show");
                _player = new MediaPlayer(_libVLC);

                _media = new Media(_libVLC, new Uri(path));
                _player.Media = _media;
                _player.Volume = Math.Clamp(
                    ConfigManager.Instance.ClickSoundVolume, 0, 100);

                _currentFilePath = path;

                LogService.Log($"点击音效已加载: {fileName}，音量 {_player.Volume}", "音效");
            }
            catch (Exception ex)
            {
                LogService.Log($"初始化点击音效失败: {ex.Message}", "音效");
            }
        }
    }

    /// <summary>播放点击音效。</summary>
    public void Play()
    {
        if (!ConfigManager.Instance.ClickSoundEnabled) return;

        var now = DateTime.Now;
        if ((now - _lastPlayTime).TotalMilliseconds < ThrottleMs) return;
        _lastPlayTime = now;

        try
        {
            EnsureInit();
            if (_player == null) return;

            _player.Stop();
            _player.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClickSound] 播放失败: {ex.Message}");
        }
    }

    /// <summary>强制试听（忽略总开关）。</summary>
    public void Preview()
    {
        try
        {
            EnsureInit();
            if (_player == null) return;

            _player.Stop();
            _player.Play();
        }
        catch { }
    }

    /// <summary>切换音效后调用，让下次播放重新加载。</summary>
    public void Reload()
    {
        lock (_lock)
        {
            try
            {
                _player?.Stop();
                _media?.Dispose();
                _player?.Dispose();
                _libVLC?.Dispose();
            }
            catch { }

            _media = null;
            _player = null;
            _libVLC = null;
            _currentFilePath = null;
        }
    }

    public void UpdateVolume(int volume)
    {
        try
        {
            if (_player != null)
                _player.Volume = Math.Clamp(volume, 0, 100);
        }
        catch { }
    }

    public void Dispose()
    {
        try
        {
            _player?.Stop();
            _media?.Dispose();
            _player?.Dispose();
            _libVLC?.Dispose();
        }
        catch { }

        _media = null;
        _player = null;
        _libVLC = null;
        _currentFilePath = null;
    }
}