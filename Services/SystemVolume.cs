using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Services;

/// <summary>
/// 系统主音量控制（仅 Windows 有效）。
/// </summary>
public static class SystemVolume
{
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, Guid pguidEventContext);
        int GetMute(out bool pbMute);
        int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        int VolumeStepUp(Guid pguidEventContext);
        int VolumeStepDown(Guid pguidEventContext);
        int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams,
                     [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    // ============================================================
    // 获取 IAudioEndpointVolume 接口
    // ============================================================
    private static IAudioEndpointVolume? GetEndpointVolume()
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice device);

            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, 23, IntPtr.Zero, out object obj);
            return (IAudioEndpointVolume)obj;
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // 设置主音量（0.0 - 1.0），同时取消静音
    // ============================================================
    public static bool SetVolume(float level)
    {
        if (level < 0f) level = 0f;
        if (level > 1f) level = 1f;

        var vol = GetEndpointVolume();
        if (vol == null) return false;

        try
        {
            vol.SetMasterVolumeLevelScalar(level, Guid.Empty);

            // ★ 只要 level > 0 就强制取消静音
            if (level > 0f)
                vol.SetMute(false, Guid.Empty);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>获取系统主音量（0.0 - 1.0）。失败返回 -1。</summary>
    public static float GetVolume()
    {
        var vol = GetEndpointVolume();
        if (vol == null) return -1f;

        try
        {
            vol.GetMasterVolumeLevelScalar(out float v);
            return v;
        }
        catch
        {
            return -1f;
        }
    }

    /// <summary>获取当前是否静音。</summary>
    public static bool GetMute()
    {
        var vol = GetEndpointVolume();
        if (vol == null) return false;

        try
        {
            vol.GetMute(out bool mute);
            return mute;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>取消静音。</summary>
    public static bool Unmute()
    {
        var vol = GetEndpointVolume();
        if (vol == null) return false;

        try
        {
            vol.SetMute(false, Guid.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ============================================================
    // 音量保持
    // ============================================================
    private static CancellationTokenSource? _holdCts;

    /// <summary>
    /// 在指定秒数内持续把系统音量保持在指定水平，并且强制取消静音。
    /// 用户手动调音量或静音都会被自动拉回。重复调用会取消上一次。
    /// </summary>
    public static void HoldVolumeFor(float level, int seconds)
    {
        _holdCts?.Cancel();
        _holdCts = new CancellationTokenSource();
        var token = _holdCts.Token;

        // 立即设置一次
        SetVolume(level);
        if (level > 0f) Unmute();

        Task.Run(async () =>
        {
            var endTime = DateTime.Now.AddSeconds(seconds);

            try
            {
                while (DateTime.Now < endTime && !token.IsCancellationRequested)
                {
                    // ★ 每次循环都检查静音 + 音量
                    if (level > 0f)
                    {
                        var isMuted = GetMute();
                        if (isMuted)
                        {
                            Unmute();
                        }

                        var current = GetVolume();
                        if (current >= 0 && Math.Abs(current - level) > 0.01f)
                        {
                            SetVolume(level);
                        }
                    }
                    else
                    {
                        // level = 0 时只保证音量为 0，不管静音
                        var current = GetVolume();
                        if (current > 0.01f)
                        {
                            SetVolume(0f);
                        }
                    }

                    await Task.Delay(100, token);
                }

                // 最后一次确保到达目标值
                if (!token.IsCancellationRequested && level > 0f)
                {
                    Unmute();
                    SetVolume(level);
                }
            }
            catch (TaskCanceledException) { }
            catch { }
        });
    }

    /// <summary>取消音量保持。</summary>
    public static void CancelHold()
    {
        try { _holdCts?.Cancel(); } catch { }
        _holdCts = null;
    }
}