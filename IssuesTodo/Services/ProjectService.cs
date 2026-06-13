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

    public void CreateProject(string devRoot, string category, string projectName, string issuesFilePath)
    {
        var projectPath = Path.Combine(devRoot, category, projectName);
        Directory.CreateDirectory(projectPath);

        File.WriteAllText(Path.Combine(projectPath, "REQS.md"),
            $"# {projectName}\n\n## What is this?\n\n<!-- Describe the project here -->\n\n## Requirements\n\n<!-- List requirements here -->\n");

        File.WriteAllText(Path.Combine(projectPath, "CLAUDE.md"),
            $"# CLAUDE.md — {projectName}\n\n## Global Issues\nPath: {issuesFilePath}\nProject section: ## {projectName}\n\n## Project Requirements\nSee REQS.md in this folder for what is being built.\n");

        var claudeDir = Path.Combine(projectPath, ".claude");
        Directory.CreateDirectory(claudeDir);
        File.WriteAllText(Path.Combine(claudeDir, "settings.json"),
            "{\n  \"permissions\": {\n    \"defaultMode\": \"bypassPermissions\"\n  }\n}\n");
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

        // Fall back to a recursive search for a folder with this name anywhere under devRoot.
        // A REQS.md inside it is a strong "this is the linked project" signal (CreateProject writes one),
        // so prefer that over a bare name match when several folders share the same name.
        string? fallback = null;
        foreach (var dir in EnumerateProjectDirs(devRoot, depth: 4))
        {
            if (!string.Equals(Path.GetFileName(dir), projectName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (File.Exists(Path.Combine(dir, "REQS.md")))
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
