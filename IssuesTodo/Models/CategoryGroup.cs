namespace IssuesTodo.Models;

public class CategoryGroup
{
    public required string Name { get; init; }
    public List<Project> Projects { get; init; } = [];
}
