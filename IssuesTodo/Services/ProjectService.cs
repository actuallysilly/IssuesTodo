using System.Diagnostics;
using System.IO;

namespace IssuesTodo.Services;

public class ProjectService
{
    public List<string> ScanDevRoot(string devRoot)
    {
        var found = new List<string>();
        if (!Directory.Exists(devRoot)) return found;

        foreach (var catDir in Directory.GetDirectories(devRoot))
        foreach (var projDir in Directory.GetDirectories(catDir))
            found.Add(projDir);

        return found;
    }

    public void CreateProject(string devRoot, string category, string projectName, string issuesFilePath,
        Dictionary<string, string> templates)
    {
        var projectPath = Path.Combine(devRoot, category, projectName);
        Directory.CreateDirectory(projectPath);

        foreach (var (relativePath, content) in templates)
        {
            var fullPath = Path.Combine(projectPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null) Directory.CreateDirectory(dir);
            var rendered = content
                .Replace("{{ProjectName}}", projectName)
                .Replace("{{IssuesFilePath}}", issuesFilePath);
            File.WriteAllText(fullPath, rendered, System.Text.Encoding.UTF8);
        }
    }

    public void CreateGitHubRepo(string projectPath, string projectName)
    {
        void Run(string exe, string args)
        {
            var p = Process.Start(new ProcessStartInfo(exe, args)
            {
                WorkingDirectory = projectPath,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            p?.WaitForExit();
        }
        Run("git", "init");
        Run("git", "add .");
        Run("git", "commit -m \"Initial commit\"");
        Run("gh", $"repo create {projectName} --private --source=. --push");
    }

    public void OpenInVSCode(string projectPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo("code", $"\"{projectPath}\"") { UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo("explorer", projectPath) { UseShellExecute = true });
        }
    }

    public void OpenFolder(string projectPath)
    {
        try { Process.Start(new ProcessStartInfo("explorer", $"\"{projectPath}\"") { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    public void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ignore — bad/unreachable link */ }
    }

    private static readonly HashSet<string> SkipDirNames = new(StringComparer.OrdinalIgnoreCase)
        { "node_modules", ".git", "bin", "obj", ".vs", "dist", "build", "packages" };

    public string? FindFolderForProject(string devRoot, string category, string projectName)
    {
        var direct = Path.Combine(devRoot, category, projectName);
        if (Directory.Exists(direct)) return direct;

        if (!Directory.Exists(devRoot)) return null;

        string? fallback = null;
        foreach (var dir in EnumerateProjectDirs(devRoot, depth: 4))
        {
            if (!string.Equals(Path.GetFileName(dir), projectName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (File.Exists(Path.Combine(dir, "REQS.md")) ||
                File.Exists(Path.Combine(dir, ".claude", "REQS.md")))
                return dir;

            fallback ??= dir;
        }

        return fallback;
    }

    private static IEnumerable<string> EnumerateProjectDirs(string root, int depth)
    {
        if (depth <= 0) yield break;

        string[] children;
        try { children = Directory.GetDirectories(root); }
        catch { yield break; }

        foreach (var dir in children)
        {
            if (SkipDirNames.Contains(Path.GetFileName(dir))) continue;

            yield return dir;
            foreach (var nested in EnumerateProjectDirs(dir, depth - 1))
                yield return nested;
        }
    }
}
