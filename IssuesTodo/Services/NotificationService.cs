using System.Media;

namespace IssuesTodo.Services;

public class NotificationService
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    public void SetTrayIcon(System.Windows.Forms.NotifyIcon icon) => _trayIcon = icon;

    public void ShowUrgent(string title, string body)
    {
        SystemSounds.Exclamation.Play();
        _trayIcon?.ShowBalloonTip(8000, title, body, System.Windows.Forms.ToolTipIcon.Error);
    }
}
