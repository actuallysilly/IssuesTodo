using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace IssuesTodo.Services;

public static class WindowTheme
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// Asks Windows to draw this window's native title bar using the dark theme,
    /// since Windows 10 doesn't allow apps to set a custom caption bar color (that's Windows 11+ only).
    public static void UseDarkTitleBar(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            int enabled = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
        };
    }
}
