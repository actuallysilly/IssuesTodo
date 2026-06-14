using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using IssuesTodo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IssuesTodo;

public partial class MainWindow : Window
{
    private const int HotkeyId = 9001;
    private uint _activeMods;
    private uint _activeVk;

    public MainWindow()
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var settings = App.Services.GetRequiredService<SettingsService>().Current;
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd).AddHook(WndProc);
        ApplyHotkey(hwnd, settings.HotkeyModifiers, settings.HotkeyVirtualKey);
    }

    public void ReregisterHotkey(uint mods, uint vk)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (_activeVk != 0) UnregisterHotKey(hwnd, HotkeyId);
        ApplyHotkey(hwnd, mods, vk);
    }

    private void ApplyHotkey(IntPtr hwnd, uint mods, uint vk)
    {
        _activeMods = mods;
        _activeVk = vk;
        if (vk != 0) RegisterHotKey(hwnd, HotkeyId, mods, vk);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == HotkeyId) // WM_HOTKEY
        {
            BringToFront();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void BringToFront()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    // Close to tray instead of exiting
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_activeVk != 0) UnregisterHotKey(new WindowInteropHelper(this).Handle, HotkeyId);
        base.OnClosed(e);
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
