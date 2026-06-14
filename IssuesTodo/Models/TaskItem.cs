namespace IssuesTodo.Models;

public enum TaskPriority { Normal, High, Low, Optional }
public enum TaskType { Dev, Human }

public class TaskItem
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required string ProjectName { get; init; }
    public TaskPriority Priority { get; init; }
    public TaskType Type { get; init; }
    public string? Comment { get; init; }
}
