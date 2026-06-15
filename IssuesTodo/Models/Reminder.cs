namespace IssuesTodo.Models;

public class Reminder
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Text { get; set; } = "";
    public DateTime DueAt { get; set; }
}
