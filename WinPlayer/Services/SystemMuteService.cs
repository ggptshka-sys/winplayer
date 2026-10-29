using System.Runtime.InteropServices;

namespace WinPlayer.Services;

/// <summary>
/// Toggles default playback device mute via Core Audio (IMMDevice / IAudioEndpointVolume).
/// SMTC sessions do not expose mute; system mute is the MVP stand-in.
/// </summary>
public sealed class SystemMuteService : IDisposable
{
    private const uint ClsctxAll = 23;
    private static readonly Guid MmDeviceEnumeratorClsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid ImmDeviceEnumeratorIid = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid IAudioEndpointVolumeIid = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    private ImmDeviceEnumerator? _enumerator;
    private ImmDevice? _device;
    private IAudioEndpointVolume? _volume;
    private bool _disposed;

    public SystemMuteService()
    {
        Initialize();
    }

    public bool IsMuted
    {
        get
        {
            if (_volume is null) return false;
            _volume.GetMute(out var muted);
            return muted;
        }
    }

    public bool ToggleMute()
    {
        if (_volume is null) return false;
        var next = !IsMuted;
        _volume.SetMute(next, Guid.Empty);
        return next;
    }

    public void SetMute(bool muted)
    {
        _volume?.SetMute(muted, Guid.Empty);
    }

    private void Initialize()
    {
        var type = Type.GetTypeFromCLSID(MmDeviceEnumeratorClsid)
                   ?? throw new InvalidOperationException("MMDeviceEnumerator CLSID unavailable.");

        var raw = Activator.CreateInstance(type)
                  ?? throw new InvalidOperationException("Failed to create MMDeviceEnumerator.");

        _enumerator = (ImmDeviceEnumerator)raw;
        _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out _device);
        var iid = IAudioEndpointVolumeIid;
        _device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out var obj);
        _volume = (IAudioEndpointVolume)obj;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_volume is not null) Marshal.ReleaseComObject(_volume);
        if (_device is not null) Marshal.ReleaseComObject(_device);
        if (_enumerator is not null) Marshal.ReleaseComObject(_enumerator);

        _volume = null;
        _device = null;
        _enumerator = null;
    }

    private enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    private enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ImmDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);

        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out ImmDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ImmDevice
    {
        int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint channelCount);
        int SetMasterVolumeLevel(float levelDb, Guid eventContext);
        int SetMasterVolumeLevelScalar(float level, Guid eventContext);
        int GetMasterVolumeLevel(out float levelDb);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float levelDb, Guid eventContext);
        int SetChannelVolumeLevelScalar(uint channel, float level, Guid eventContext);
        int GetChannelVolumeLevel(uint channel, out float levelDb);
        int GetChannelVolumeLevelScalar(uint channel, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid eventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
