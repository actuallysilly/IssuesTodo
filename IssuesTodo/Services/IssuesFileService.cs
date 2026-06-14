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

        // Buffered task: collect comment lines before flushing
        (string id, string text, TaskPriority priority, TaskType type)? pendingTask = null;
        var pendingComment = new List<string>();

        void FlushPendingTask()
        {
            if (pendingTask == null || currentProj == null) return;
            var comment = pendingComment.Count > 0
                ? string.Join("\n", pendingComment.Select(l => l.TrimStart())).Trim()
                : null;
            currentProj.Tasks.Add(new TaskItem
            {
                Id = pendingTask.Value.id,
                Text = pendingTask.Value.text,
                ProjectName = currentProj.Name,
                Priority = pendingTask.Value.priority,
                Type = pendingTask.Value.type,
                Comment = comment
            });
            pendingTask = null;
            pendingComment.Clear();
        }

        void FlushProject()
        {
            FlushPendingTask();
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
                var rawName = line[3..].Trim();
                var isMaybe = rawName.StartsWith("?", StringComparison.Ordinal);
                var projName = isMaybe ? rawName[1..].TrimStart() : rawName;
                currentProj = new Project { Name = projName, Category = currentCat?.Name ?? "", IsMaybe = isMaybe };
                continue;
            }

            if (currentProj == null) continue;

            if (string.IsNullOrWhiteSpace(line))
            {
                // Blank line ends any pending comment block
                FlushPendingTask();
                continue;
            }

            var trimmed = line.TrimStart();

            if (TryParseTaskPrefix(trimmed, out var priority, out var rawText))
            {
                FlushPendingTask();
                var (displayText, taskType) = ParseTaskText(rawText);
                pendingTask = (ComputeId(currentProj.Name, displayText), displayText, priority, taskType);
                continue;
            }

            // Non-task, non-header, non-blank line after a task = comment
            if (pendingTask != null)
            {
                pendingComment.Add(line);
                continue;
            }
        }

        FlushCategory();
        return categories;
    }

    private static bool TryParseTaskPrefix(string trimmed, out TaskPriority priority, out string text)
    {
        if (trimmed.StartsWith("-hp ", StringComparison.Ordinal)) { priority = TaskPriority.High;     text = trimmed[4..]; return true; }
        if (trimmed.StartsWith("-lp ", StringComparison.Ordinal)) { priority = TaskPriority.Low;      text = trimmed[4..]; return true; }
        if (trimmed.StartsWith("-ep ", StringComparison.Ordinal)) { priority = TaskPriority.Optional; text = trimmed[4..]; return true; }
        if (trimmed.StartsWith("* ", StringComparison.Ordinal))   { priority = TaskPriority.Normal;   text = trimmed[2..]; return true; }
        if (trimmed.StartsWith("- ", StringComparison.Ordinal))   { priority = TaskPriority.Normal;   text = trimmed[2..]; return true; }
        priority = TaskPriority.Normal;
        text = "";
        return false;
    }

    private const string HumanMarker = "[h] ";

    public static (string displayText, TaskType type) ParseTaskText(string raw)
    {
        if (raw.StartsWith(HumanMarker, StringComparison.Ordinal))
            return (raw[HumanMarker.Length..], TaskType.Human);
        return (raw, TaskType.Dev);
    }

    public static string EncodeTaskText(string displayText, TaskType type) =>
        type == TaskType.Human ? HumanMarker + displayText : displayText;

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

    public void AddTask(string issuesPath, string category, string projectName, string text, TaskPriority priority, TaskType type = TaskType.Dev)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0) return;

        int projIndex = FindProjectIndex(lines, catIndex, projectName);
        if (projIndex < 0) return;

        int sectionEnd = ProjectSectionEnd(lines, projIndex);
        int insertAt = sectionEnd;
        while (insertAt > projIndex + 1 && string.IsNullOrWhiteSpace(lines[insertAt - 1]))
            insertAt--;

        lines.Insert(insertAt, PriorityPrefix(priority) + EncodeTaskText(text, type));
        File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
    }

    public void EditTask(string issuesPath, string category, string projectName, string oldText, string newText, TaskPriority newPriority, TaskType newType = TaskType.Dev)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0) return;

        int projIndex = FindProjectIndex(lines, catIndex, projectName);
        if (projIndex < 0) return;

        var prefix = PriorityPrefix(newPriority);

        for (int i = projIndex + 1; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("# ", StringComparison.Ordinal) || lines[i].StartsWith("## ", StringComparison.Ordinal))
                break;

            var trimmed = lines[i].TrimStart();
            if (!TryParseTaskPrefix(trimmed, out _, out var rawText)) continue;
            var (displayText, _) = ParseTaskText(rawText);
            if (displayText != oldText) continue;

            lines[i] = prefix + EncodeTaskText(newText, newType);
            File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
            return;
        }
    }

    public void RemoveTask(string issuesPath, string category, string projectName, string text)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0) return;

        int projIndex = FindProjectIndex(lines, catIndex, projectName);
        if (projIndex < 0) return;

        int sectionEnd = ProjectSectionEnd(lines, projIndex);

        for (int i = projIndex + 1; i < sectionEnd; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!TryParseTaskPrefix(trimmed, out _, out var rawLineText)) continue;
            var (lineText, _) = ParseTaskText(rawLineText);
            if (lineText != text) continue;

            // Remove task line + its comment lines
            int removeCount = 1;
            while (i + removeCount < sectionEnd
                   && !TryParseTaskPrefix(lines[i + removeCount].TrimStart(), out _, out _)
                   && !string.IsNullOrWhiteSpace(lines[i + removeCount]))
                removeCount++;

            lines.RemoveRange(i, removeCount);
            File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
            return;
        }
    }

    public void EditComment(string issuesPath, string category, string projectName, string taskText, string? comment)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0) return;

        int projIndex = FindProjectIndex(lines, catIndex, projectName);
        if (projIndex < 0) return;

        int sectionEnd = ProjectSectionEnd(lines, projIndex);

        int taskLineIndex = -1;
        for (int i = projIndex + 1; i < sectionEnd; i++)
        {
            if (!TryParseTaskPrefix(lines[i].TrimStart(), out _, out var rawText)) continue;
            var (displayText, _) = ParseTaskText(rawText);
            if (displayText != taskText) continue;
            taskLineIndex = i;
            break;
        }
        if (taskLineIndex < 0) return;

        // Remove existing comment lines immediately after the task
        int commentStart = taskLineIndex + 1;
        int commentEnd = commentStart;
        while (commentEnd < sectionEnd
               && !TryParseTaskPrefix(lines[commentEnd].TrimStart(), out _, out _)
               && !string.IsNullOrWhiteSpace(lines[commentEnd]))
            commentEnd++;

        lines.RemoveRange(commentStart, commentEnd - commentStart);

        // Insert new comment if non-empty
        if (!string.IsNullOrWhiteSpace(comment))
        {
            var commentLines = comment.Split('\n')
                .Select(l => "  " + l.TrimEnd())
                .ToArray();
            for (int i = commentLines.Length - 1; i >= 0; i--)
                lines.Insert(commentStart, commentLines[i]);
        }

        File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
    }

    public void ReorderTasks(string issuesPath, string category, string projectName, IList<string> orderedTexts)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0) return;

        int projIndex = FindProjectIndex(lines, catIndex, projectName);
        if (projIndex < 0) return;

        int sectionStart = projIndex + 1;
        int sectionEnd = ProjectSectionEnd(lines, projIndex);

        // Collect task blocks: task line + its comment lines
        var blocks = new List<(string text, List<string> rawLines)>();
        int idx = sectionStart;
        while (idx < sectionEnd)
        {
            var line = lines[idx];
            if (!TryParseTaskPrefix(line.TrimStart(), out _, out var rawText)) { idx++; continue; }
            var (text, _) = ParseTaskText(rawText);

            var block = new List<string> { line };
            idx++;
            while (idx < sectionEnd
                   && !TryParseTaskPrefix(lines[idx].TrimStart(), out _, out _)
                   && !string.IsNullOrWhiteSpace(lines[idx]))
            {
                block.Add(lines[idx]);
                idx++;
            }
            blocks.Add((text, block));
        }

        // Build ordered output: requested order first, then any remaining (e.g. done tasks)
        var ordered = new List<(string text, List<string> rawLines)>();
        foreach (var t in orderedTexts)
        {
            var b = blocks.FirstOrDefault(x => x.text == t);
            if (b.rawLines != null) ordered.Add(b);
        }
        foreach (var b in blocks)
        {
            if (!orderedTexts.Contains(b.text)) ordered.Add(b);
        }

        lines.RemoveRange(sectionStart, sectionEnd - sectionStart);
        int insertAt = sectionStart;
        foreach (var b in ordered)
        {
            foreach (var bl in b.rawLines) { lines.Insert(insertAt, bl); insertAt++; }
        }

        File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
    }

    public void RenameProject(string issuesPath, string category, string oldName, string newName)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0) return;

        int projIndex = FindProjectIndex(lines, catIndex, oldName);
        if (projIndex < 0) return;

        var isMaybe = lines[projIndex][3..].Trim().StartsWith("?", StringComparison.Ordinal);
        lines[projIndex] = isMaybe ? $"## ?{newName}" : $"## {newName}";
        File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
    }

    public void ToggleMaybeProject(string issuesPath, string category, string projectName)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0) return;

        int projIndex = FindProjectIndex(lines, catIndex, projectName);
        if (projIndex < 0) return;

        var rawName = lines[projIndex][3..].Trim();
        lines[projIndex] = rawName.StartsWith("?", StringComparison.Ordinal)
            ? $"## {rawName[1..].TrimStart()}"
            : $"## ?{rawName}";

        File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
    }

    public void DeleteProject(string issuesPath, string category, string projectName)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0) return;

        int projIndex = FindProjectIndex(lines, catIndex, projectName);
        if (projIndex < 0) return;

        int sectionEnd = ProjectSectionEnd(lines, projIndex);
        // Trim trailing blank lines within the section
        while (sectionEnd > projIndex + 1 && string.IsNullOrWhiteSpace(lines[sectionEnd - 1]))
            sectionEnd--;

        lines.RemoveRange(projIndex, sectionEnd - projIndex);
        File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
    }

    public List<TaskItem> ParseGeneralTodos(string path)
    {
        if (!File.Exists(path)) return [];

        var tasks = new List<TaskItem>();
        foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            var trimmed = raw.TrimStart();
            if (!TryParseTaskPrefix(trimmed, out var priority, out var rawText)) continue;
            var (text, type) = ParseTaskText(rawText);
            tasks.Add(new TaskItem
            {
                Id = ComputeId("__general__", text),
                Text = text,
                ProjectName = "General",
                Priority = priority,
                Type = type
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
            var trimmed = lines[i].TrimStart();
            if (!TryParseTaskPrefix(trimmed, out _, out var text) || text != oldText) continue;
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
            var trimmed = lines[i].TrimStart();
            if (!TryParseTaskPrefix(trimmed, out _, out var lineText) || lineText != text) continue;
            lines.RemoveAt(i);
            File.WriteAllLines(path, lines, Encoding.UTF8);
            return;
        }
    }

    public void AddProject(string issuesPath, string category, string projectName, bool isMaybe = false)
    {
        var lines = File.ReadAllLines(issuesPath, Encoding.UTF8).ToList();
        var heading = isMaybe ? $"## ?{projectName}" : $"## {projectName}";

        var catIndex = lines.FindIndex(l => l.TrimEnd() == $"# {category}");
        if (catIndex < 0)
        {
            lines.Add("");
            lines.Add($"# {category}");
            lines.Add("");
            lines.Add(heading);
            lines.Add("");
        }
        else
        {
            int insertAt = catIndex + 1;
            while (insertAt < lines.Count && !lines[insertAt].StartsWith("# ", StringComparison.Ordinal))
                insertAt++;
            lines.Insert(insertAt, "");
            lines.Insert(insertAt, heading);
        }

        File.WriteAllLines(issuesPath, lines, Encoding.UTF8);
    }

    private static int FindProjectIndex(List<string> lines, int catIndex, string projectName)
    {
        for (int i = catIndex + 1; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("# ", StringComparison.Ordinal)) break;
            if (!lines[i].StartsWith("## ", StringComparison.Ordinal)) continue;
            var rawName = lines[i][3..].Trim();
            if (rawName.StartsWith("?", StringComparison.Ordinal)) rawName = rawName[1..].TrimStart();
            if (rawName == projectName) return i;
        }
        return -1;
    }

    private static string PriorityPrefix(TaskPriority priority) => priority switch
    {
        TaskPriority.High     => "-hp ",
        TaskPriority.Low      => "-lp ",
        TaskPriority.Optional => "-ep ",
        _                     => "- "
    };

    private static int ProjectSectionEnd(List<string> lines, int projIndex)
    {
        int end = projIndex + 1;
        while (end < lines.Count
               && !lines[end].StartsWith("# ", StringComparison.Ordinal)
               && !lines[end].StartsWith("## ", StringComparison.Ordinal))
            end++;
        return end;
    }

    private static bool TryParseTaskLine(string line, out TaskPriority priority, out string text)
        => TryParseTaskPrefix(line.TrimStart(), out priority, out text);
}
