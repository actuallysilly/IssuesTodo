namespace IssuesTodo.Models;

public enum TaskPriority { Normal, High, Low }

public class TaskItem
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required string ProjectName { get; init; }
    public TaskPriority Priority { get; init; }
}
