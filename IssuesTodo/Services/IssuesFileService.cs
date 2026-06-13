using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IssuesTodo.Models;

namespace IssuesTodo.Services;

public class CompletionData
{
    [JsonPropertyName("done")]
    public List<string> Done { get; set; } = [];

    [JsonPropertyName("history")]
    public List<HistoryEntry> History { get; set; } = [];
}

public class HistoryEntry
{
    [JsonPropertyName("id")]    public string Id { get; set; } = "";
    [JsonPropertyName("text")]  public string Text { get; set; } = "";
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("doneAt")] public string DoneAt { get; set; } = "";
}

public class IssuesFileService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public List<CategoryGroup> ParseIssues(string path)
    {
        var content = File.ReadAllText(path, Encoding.UTF8);
        return ParseContent(content);
    }

    private static List<CategoryGroup> ParseContent(string content)
    {
        var categories = new List<CategoryGroup>();
        CategoryGroup? currentCat = null;
        Project? currentProj = null;

        void FlushProject()
        {
            if (currentProj != null) currentCat?.Projects.Add(currentProj);
            currentProj = null;
        }
        void FlushCategory()
        {
            FlushProject();
            if (currentCat != null) categories.Add(currentCat);
            currentCat = null;
        }

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd();

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                FlushCategory();
                currentCat = new CategoryGroup { Name = line[2..].Trim() };
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushProject();
                currentProj = new Project
                {
                    Name = line[3..].Trim(),
                    Category = currentCat?.Name ?? ""
                };
                continue;
            }

            if (currentProj == null || string.IsNullOrWhiteSpace(line)) continue;

            var trimmed = line.TrimStart();
            TaskPriority priority = TaskPriority.Normal;
            string text;

            if (trimmed.StartsWith("-hp ", StringComparison.Ordinal))      { priority = TaskPriority.High; text = trimmed[4..]; }
            else if (trimmed.StartsWith("-lp ", StringComparison.Ordinal)) { priority = TaskPriority.Low;  text = trimmed[4..]; }
            else if (trimmed.StartsWith("* ", StringComparison.Ordinal))   { text = trimmed[2..]; }
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal))   { text = trimmed[2..]; }
            else continue;

            currentProj.Tasks.Add(new TaskItem
            {
                Id = ComputeId(currentProj.Name, text),
                Text = text,
                ProjectName = currentProj.Name,
                Priority = priority
            });
        }

        FlushCategory();
        return categories;
    }

    // Replicates the server.js MD5: MD5(sectionName + "|" + text).slice(0, 8)
    private static string ComputeId(string sectionName, string text)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(sectionName + "|" + text));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }

    public CompletionData ReadDone(string donePath)
    {
        try
        {
            var json = File.ReadAllText(donePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<CompletionData>(json, JsonOpts) ?? new CompletionData();
        }
        catch { return new CompletionData(); }
    }

    public void MarkDone(string donePath, TaskItem task)
    {
        var data = ReadDone(donePath);
        if (data.Done.Contains(task.Id)) return;
        data.Done.Add(task.Id);
        data.History.Add(new HistoryEntry
        {
            Id = task.Id,
            Text = task.Text,
            Project = task.ProjectName,
            DoneAt = DateTime.UtcNow.ToString("O")
        });
        WriteDone(donePath, data);
    }

    public void UnmarkDone(string donePath, string taskId)
    {
        var data = ReadDone(donePath);
        data.Done.Remove(taskId);
        WriteDone(donePath, data);
    }

    private static void WriteDone(string donePath, CompletionData data)
    {
        File.WriteAllText(donePath, JsonSerializer.Serialize(data, JsonOpts), Encoding.UTF8);
    }

    public void AddTask(string issuesPath, string category, string projectName, string text, TaskPriority priority)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0) return;

        int projIndex = -1;
        for (int i = catIndex + 1; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("# ", StringComparison.Ordinal)) break;
            if (lines[i].TrimEnd() == $"## {projectName}") { projIndex = i; break; }
        }
        if (projIndex < 0) return;

        int sectionEnd = projIndex + 1;
        while (sectionEnd < lines.Count
               && !lines[sectionEnd].StartsWith("# ", StringComparison.Ordinal)
               && !lines[sectionEnd].StartsWith("## ", StringComparison.Ordinal))
            sectionEnd++;

        int insertAt = sectionEnd;
        while (insertAt > projIndex + 1 && string.IsNullOrWhiteSpace(lines[insertAt - 1]))
            insertAt--;

        var prefix = priority switch
        {
            TaskPriority.High => "-hp ",
            TaskPriority.Low => "-lp ",
            _ => "- "
        };
        lines.Insert(insertAt, prefix + text);

        File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
    }

    public void EditTask(string issuesPath, string category, string projectName, string oldText, string newText, TaskPriority newPriority)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0) return;

        int projIndex = -1;
        for (int i = catIndex + 1; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("# ", StringComparison.Ordinal)) break;
            if (lines[i].TrimEnd() == $"## {projectName}") { projIndex = i; break; }
        }
        if (projIndex < 0) return;

        var prefix = newPriority switch
        {
            TaskPriority.High => "-hp ",
            TaskPriority.Low => "-lp ",
            _ => "- "
        };

        for (int i = projIndex + 1; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("# ", StringComparison.Ordinal) || lines[i].StartsWith("## ", StringComparison.Ordinal))
                break;

            var trimmed = lines[i].TrimStart();
            string text;
            if (trimmed.StartsWith("-hp ", StringComparison.Ordinal)) text = trimmed[4..];
            else if (trimmed.StartsWith("-lp ", StringComparison.Ordinal)) text = trimmed[4..];
            else if (trimmed.StartsWith("* ", StringComparison.Ordinal)) text = trimmed[2..];
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal)) text = trimmed[2..];
            else continue;

            if (text != oldText) continue;

            lines[i] = prefix + newText;
            File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
            return;
        }
    }

    public void RemoveTask(string issuesPath, string category, string projectName, string text)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0) return;

        int projIndex = -1;
        for (int i = catIndex + 1; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("# ", StringComparison.Ordinal)) break;
            if (lines[i].TrimEnd() == $"## {projectName}") { projIndex = i; break; }
        }
        if (projIndex < 0) return;

        for (int i = projIndex + 1; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("# ", StringComparison.Ordinal) || lines[i].StartsWith("## ", StringComparison.Ordinal))
                break;

            var trimmed = lines[i].TrimStart();
            string lineText;
            if (trimmed.StartsWith("-hp ", StringComparison.Ordinal)) lineText = trimmed[4..];
            else if (trimmed.StartsWith("-lp ", StringComparison.Ordinal)) lineText = trimmed[4..];
            else if (trimmed.StartsWith("* ", StringComparison.Ordinal)) lineText = trimmed[2..];
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal)) lineText = trimmed[2..];
            else continue;

            if (lineText != text) continue;

            lines.RemoveAt(i);
            File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
            return;
        }
    }

    public List<TaskItem> ParseGeneralTodos(string path)
    {
        if (!File.Exists(path)) return [];

        var tasks = new List<TaskItem>();
        foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            if (!TryParseTaskLine(raw, out var priority, out var text)) continue;
            tasks.Add(new TaskItem
            {
                Id = ComputeId("__general__", text),
                Text = text,
                ProjectName = "General",
                Priority = priority
            });
        }
        return tasks;
    }

    public void AddGeneralTodo(string path, string text, TaskPriority priority)
    {
        File.AppendAllLines(path, new[] { PriorityPrefix(priority) + text }, Encoding.UTF8);
    }

    public void EditGeneralTodo(string path, string oldText, string newText, TaskPriority newPriority)
    {
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path, Encoding.UTF8).ToList();

        for (int i = 0; i < lines.Count; i++)
        {
            if (!TryParseTaskLine(lines[i], out _, out var text) || text != oldText) continue;
            lines[i] = PriorityPrefix(newPriority) + newText;
            File.WriteAllLines(path, lines, Encoding.UTF8);
            return;
        }
    }

    public void RemoveGeneralTodo(string path, string text)
    {
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path, Encoding.UTF8).ToList();

        for (int i = 0; i < lines.Count; i++)
        {
            if (!TryParseTaskLine(lines[i], out _, out var lineText) || lineText != text) continue;
            lines.RemoveAt(i);
            File.WriteAllLines(path, lines, Encoding.UTF8);
            return;
        }
    }

    private static string PriorityPrefix(TaskPriority priority) => priority switch
    {
        TaskPriority.High => "-hp ",
        TaskPriority.Low => "-lp ",
        _ => "- "
    };

    private static bool TryParseTaskLine(string line, out TaskPriority priority, out string text)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("-hp ", StringComparison.Ordinal))      { priority = TaskPriority.High;   text = trimmed[4..]; return true; }
        if (trimmed.StartsWith("-lp ", StringComparison.Ordinal))      { priority = TaskPriority.Low;    text = trimmed[4..]; return true; }
        if (trimmed.StartsWith("* ", StringComparison.Ordinal))        { priority = TaskPriority.Normal; text = trimmed[2..]; return true; }
        if (trimmed.StartsWith("- ", StringComparison.Ordinal))        { priority = TaskPriority.Normal; text = trimmed[2..]; return true; }
        priority = TaskPriority.Normal;
        text = "";
        return false;
    }

    public void AddProject(string issuesPath, string category, string projectName)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0)
        {
            lines.Add("");
            lines.Add($"# {category}");
            lines.Add("");
            lines.Add($"## {projectName}");
            lines.Add("");
        }
        else
        {
            // Insert before the next # heading or at end of file
            int insertAt = catIndex + 1;
            while (insertAt < lines.Count && !lines[insertAt].StartsWith("# ", StringComparison.Ordinal))
                insertAt++;
            lines.Insert(insertAt, "");
            lines.Insert(insertAt, $"## {projectName}");
        }

        File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
    }
}
