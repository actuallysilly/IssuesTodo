namespace IssuesTodo.Models;

public class Project
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public bool IsMaybe { get; init; }
    public List<TaskItem> Tasks { get; init; } = [];
    public string? FolderPath { get; set; }
    public string? RepoUrl { get; set; }
}
