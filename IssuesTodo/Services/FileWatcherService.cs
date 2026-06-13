using System.IO;

namespace IssuesTodo.Services;

public sealed class FileWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private const int DebounceMs = 250;

    public event Action? IssuesChanged;

    public void Watch(string filePath)
    {
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher
        {
            Path = Path.GetDirectoryName(filePath)!,
            Filter = Path.GetFileName(filePath),
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnChanged;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _debounce?.Dispose();
        _debounce = new Timer(_ => IssuesChanged?.Invoke(), null, DebounceMs, Timeout.Infinite);
    }

    public void Dispose()
    {
        _debounce?.Dispose();
        _watcher?.Dispose();
    }
}
