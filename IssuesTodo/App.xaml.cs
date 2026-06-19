using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using IssuesTodo.Models;
using IssuesTodo.Services;
using IssuesTodo.ViewModels;

namespace IssuesTodo;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private System.Windows.Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<SettingsService>();
        collection.AddSingleton<IssuesFileService>();
        collection.AddSingleton<FileWatcherService>();
        collection.AddSingleton<ProjectService>();
        collection.AddSingleton<ThemeService>();
        collection.AddSingleton<RemindersService>();
        collection.AddSingleton<NotificationService>();
        collection.AddSingleton<MainViewModel>();
        collection.AddTransient<MainWindow>();

        Services = collection.BuildServiceProvider();

        var vm = Services.GetRequiredService<MainViewModel>();
        vm.Initialize();

        Services.GetRequiredService<ThemeService>().Apply(ThemePresets.Find(vm.Settings.Theme));

        var window = Services.GetRequiredService<MainWindow>();
        window.DataContext = vm;
        window.Show();

        SetupTrayIcon(vm, window);
        vm.ShowReviewReminderIfDue(window);

        base.OnStartup(e);
    }

    private void SetupTrayIcon(MainViewModel vm, MainWindow window)
    {
        var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/issues-todo.ico"))?.Stream;
        if (iconStream == null) return;

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream),
            Visible = true,
            Text = "Issues.TODO"
        };

        Services.GetRequiredService<NotificationService>().SetTrayIcon(_trayIcon);
        _trayIcon.DoubleClick += (_, _) => window.BringToFront();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => window.BringToFront());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            Services.GetRequiredService<MainViewModel>().FlushPendingCompletions();
            Current.Shutdown();
        });
        _trayIcon.ContextMenuStrip = menu;

        var hpCount = vm.Categories
            .SelectMany(c => c.Projects)
            .SelectMany(p => p.Tasks)
            .Count(t => t.Model.Priority == TaskPriority.High && !t.IsDone);

        if (hpCount > 0)
        {
            var word = hpCount == 1 ? "task" : "tasks";
            _trayIcon.Text = $"Issues.TODO — {hpCount} HP {word}";
            _trayIcon.BalloonTipTitle = "Issues.TODO — High Priority";
            _trayIcon.BalloonTipText = $"{hpCount} high priority {word} need attention";
            _trayIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Warning;
            _trayIcon.ShowBalloonTip(6000);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services.GetRequiredService<MainViewModel>().FlushPendingCompletions();
        _trayIcon?.Dispose();
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
