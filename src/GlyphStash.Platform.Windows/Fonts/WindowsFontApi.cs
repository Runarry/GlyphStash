using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GlyphStash.Platform.Windows.Fonts;

public interface IWindowsFontApi
{
    int AddFontResourceEx(string fileName, int flags);

    bool RemoveFontResourceEx(string fileName, int flags);

    void BroadcastFontChange();
}

[SupportedOSPlatform("windows")]
public sealed class WindowsFontApi : IWindowsFontApi
{
    private const int WmFontChange = 0x001D;
    private const int HwndBroadcast = 0xFFFF;
    private const int SmtoAbortIfHung = 0x0002;

    public int AddFontResourceEx(string fileName, int flags) => AddFontResourceExW(fileName, flags, IntPtr.Zero);

    public bool RemoveFontResourceEx(string fileName, int flags) => RemoveFontResourceExW(fileName, flags, IntPtr.Zero);

    public void BroadcastFontChange()
    {
        _ = SendMessageTimeoutW(
            (IntPtr)HwndBroadcast,
            WmFontChange,
            IntPtr.Zero,
            IntPtr.Zero,
            SmtoAbortIfHung,
            3000,
            out _);
    }

    [DllImport("gdi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int AddFontResourceExW(string lpszFilename, int fl, IntPtr pdv);

    [DllImport("gdi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool RemoveFontResourceExW(string lpFileName, int fl, IntPtr pdv);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeoutW(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);
}
