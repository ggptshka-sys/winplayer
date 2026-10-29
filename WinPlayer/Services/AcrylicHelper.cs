using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WinPlayer.Services;

/// <summary>
/// Taskbar-like acrylic blur, clipped to flat-top rounded region (keeps brow corners).
/// Requires AllowsTransparency=false.
/// </summary>
public static class AcrylicHelper
{
    private const int BottomCornerRadiusDip = 22;

    /// <param name="tintAlpha">Opacity of tint over blur (taskbar-ish ~0xC8–0xE0).</param>
    /// <param name="bgr">Win32 COLORREF low 24 bits: 0x00BBGGRR.</param>
    public static void Apply(Window window, byte tintAlpha = 0xD0, uint bgr = 0x00202020)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            if (hwnd == IntPtr.Zero) return;

            EnableBlur(hwnd, tintAlpha, bgr);
            ApplyFlatTopRoundRegion(window, hwnd);
        }
        catch
        {
            // XAML fill is the fallback.
        }
    }

    public static void ApplyFlatTopRoundRegion(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            ApplyFlatTopRoundRegion(window, hwnd);
        }
        catch
        {
            // ignore
        }
    }

    private static void EnableBlur(IntPtr hwnd, byte tintAlpha, uint bgr)
    {
        var accent = new AccentPolicy
        {
            AccentState = AccentState.AccentEnableAcrylicBlurBehind,
            // GradientColor = 0xAABBGGRR
            GradientColor = ((uint)tintAlpha << 24) | (bgr & 0x00FFFFFF)
        };

        var size = Marshal.SizeOf<AccentPolicy>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.AccentPolicy,
                Data = ptr,
                SizeOfData = size
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static void ApplyFlatTopRoundRegion(Window window, IntPtr hwnd)
    {
        if (window.ActualWidth <= 0 || window.ActualHeight <= 0) return;

        var dpi = VisualTreeHelper.GetDpi(window);
        var w = Math.Max(1, (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX));
        var h = Math.Max(1, (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY));
        var r = Math.Max(1, (int)Math.Ceiling(BottomCornerRadiusDip * dpi.DpiScaleX));

        // Flat top + rounded bottom (like a brow with taskbar material).
        var region = CreateRoundRectRgn(0, -r, w + 1, h + 1, r * 2, r * 2);
        if (region != IntPtr.Zero)
        {
            SetWindowRgn(hwnd, region, true);
        }
    }

    private enum AccentState
    {
        AccentDisabled = 0,
        AccentEnableGradient = 1,
        AccentEnableTransparentGradient = 2,
        AccentEnableBlurBehind = 3,
        AccentEnableAcrylicBlurBehind = 4,
        AccentEnableHostBackdrop = 5
    }

    private enum WindowCompositionAttribute
    {
        AccentPolicy = 19
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
}
