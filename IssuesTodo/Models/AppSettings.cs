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
}
