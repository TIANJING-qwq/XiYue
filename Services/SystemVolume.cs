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

    /// <summary>设置系统主音量（0.0 - 1.0）。</summary>
    public static bool SetVolume(float level)
    {
        if (!OperatingSystem.IsWindows()) return false;

        if (level < 0f) level = 0f;
        if (level > 1f) level = 1f;

        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice device);

            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, 23, IntPtr.Zero, out object obj);
            var volume = (IAudioEndpointVolume)obj;

            volume.SetMasterVolumeLevelScalar(level, Guid.Empty);

            if (level > 0f)
                volume.SetMute(false, Guid.Empty);

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
        if (!OperatingSystem.IsWindows()) return -1f;

        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice device);

            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, 23, IntPtr.Zero, out object obj);
            var volume = (IAudioEndpointVolume)obj;

            volume.GetMasterVolumeLevelScalar(out float v);
            return v;
        }
        catch
        {
            return -1f;
        }
    }

    private static CancellationTokenSource? _holdCts;

    /// <summary>
    /// 在指定秒数内持续把系统音量保持在指定水平。
    /// 用户手动调整音量后会被自动拉回。重复调用会取消上一次。
    /// </summary>
    public static void HoldVolumeFor(float level, int seconds)
    {
        _holdCts?.Cancel();
        _holdCts = new CancellationTokenSource();
        var token = _holdCts.Token;

        Task.Run(async () =>
        {
            var endTime = DateTime.Now.AddSeconds(seconds);

            try
            {
                while (DateTime.Now < endTime && !token.IsCancellationRequested)
                {
                    var current = GetVolume();
                    // 偏差超过 2% 就拉回
                    if (current >= 0 && Math.Abs(current - level) > 0.02f)
                    {
                        SetVolume(level);
                    }

                    await Task.Delay(200, token);
                }

                // 最后一次确保到达目标值
                if (!token.IsCancellationRequested)
                    SetVolume(level);
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