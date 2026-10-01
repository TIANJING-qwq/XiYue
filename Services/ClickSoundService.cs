using LibVLCSharp.Shared;
using SBtools.Models;
using System;
using System.IO;

namespace SBtools.Services;

/// <summary>
/// 全局点击音效服务。用 LibVLCSharp 播放 wav，支持音量调节。
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

    // ★ 节流间隔（毫秒），防止连点导致声音重叠
    private const int ThrottleMs = 80;

    private ClickSoundService() { }

    private void EnsureInit()
    {
        if (_player != null) return;

        lock (_lock)
        {
            if (_player != null) return;

            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Sounds", "冰冰冰.wav");
                if (!File.Exists(path))
                {
                    LogService.Log($"点击音效文件不存在: {path}", "音效");
                    return;
                }

                _libVLC = new LibVLC("--no-video", "--quiet", "--no-video-title-show");
                _player = new MediaPlayer(_libVLC);

                _media = new Media(_libVLC, new Uri(path));
                _player.Media = _media;
                _player.Volume = Math.Clamp(
                    ConfigManager.Instance.ClickSoundVolume, 0, 100);

                LogService.Log($"点击音效已初始化，音量 {_player.Volume}", "音效");
            }
            catch (Exception ex)
            {
                LogService.Log($"初始化点击音效失败: {ex.Message}", "音效");
            }
        }
    }

    /// <summary>播放点击音效。会根据配置和节流决定是否播放。</summary>
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

            // 从当前位置重新开始（点击音效很短，Stop 后 Play 即可）
            _player.Stop();
            _player.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClickSound] 播放失败: {ex.Message}");
        }
    }

    /// <summary>更新音量（0-100）。</summary>
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
    }
}