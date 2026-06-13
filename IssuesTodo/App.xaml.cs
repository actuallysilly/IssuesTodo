using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using IssuesTodo.Models;
using IssuesTodo.Services;
using IssuesTodo.ViewModels;

namespace IssuesTodo;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<SettingsService>();
        collection.AddSingleton<IssuesFileService>();
        collection.AddSingleton<FileWatcherService>();
        collection.AddSingleton<ProjectService>();
        collection.AddSingleton<ThemeService>();
        collection.AddSingleton<MainViewModel>();
        collection.AddTransient<MainWindow>();

        Services = collection.BuildServiceProvider();

        var vm = Services.GetRequiredService<MainViewModel>();
        vm.Initialize();

        Services.GetRequiredService<ThemeService>().Apply(ThemePresets.Find(vm.Settings.Theme));

        var window = Services.GetRequiredService<MainWindow>();
        window.DataContext = vm;
        window.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services.GetRequiredService<MainViewModel>().FlushPendingCompletions();
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
