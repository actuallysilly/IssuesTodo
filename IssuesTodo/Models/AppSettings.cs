namespace IssuesTodo.Models;

public class AppSettings
{
    public string DevRoot { get; set; } = @"D:\dev";
    public string IssuesFilePath { get; set; } = @"D:\dev\Smaller\IssuesTodo\issues.md";
    public string GeneralTodoFilePath { get; set; } = @"D:\dev\Smaller\IssuesTodo\todo.md";
    public string DoneFilePath { get; set; } = @"D:\dev\Smaller\IssuesTodo\issues.done.json";
    public string Theme { get; set; } = ThemePresets.Default.Name;
    public HashSet<string> ArchivedProjects { get; set; } = [];

    /// Manual root-folder overrides, keyed by "Category|ProjectName".
    /// Takes priority over auto-detection in ProjectService.FindFolderForProject.
    public Dictionary<string, string> ProjectFolders { get; set; } = [];

    /// Repository links, keyed by "Category|ProjectName".
    public Dictionary<string, string> ProjectRepos { get; set; } = [];

    public bool ShowMaybeProjects { get; set; } = false;

    // Win32 MOD_* flags: 0x0001=Alt, 0x0002=Ctrl, 0x0004=Shift, 0x0008=Win
    public uint HotkeyModifiers { get; set; } = 0x0002 | 0x0004; // Ctrl+Shift
    public uint HotkeyVirtualKey { get; set; } = 0x49;           // I

    // "never", "1w", "2w", "1month"
    public string ReviewFrequency { get; set; } = "1month";
    public string LastReviewReminder { get; set; } = "";

    public string RemindersFilePath { get; set; } = @"D:\dev\Smaller\IssuesTodo\reminders.json";
}
