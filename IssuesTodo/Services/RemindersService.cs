using System.IO;
using System.Text.Json;
using IssuesTodo.Models;

namespace IssuesTodo.Services;

public class RemindersService
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public List<Reminder> Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<List<Reminder>>(File.ReadAllText(path), Opts) ?? [];
        }
        catch { return []; }
    }

    public void Save(string path, IEnumerable<Reminder> reminders)
        => File.WriteAllText(path, JsonSerializer.Serialize(reminders.ToList(), Opts));

    public Reminder Add(string path, string text, DateTime dueAt)
    {
        var list = Load(path);
        var r = new Reminder { Text = text, DueAt = dueAt };
        list.Add(r);
        Save(path, list);
        return r;
    }

    public void Remove(string path, string id)
    {
        var list = Load(path);
        if (list.RemoveAll(r => r.Id == id) > 0)
            Save(path, list);
    }

    /// Returns and removes all reminders whose DueAt has passed.
    public List<Reminder> PopDue(string path)
    {
        var list = Load(path);
        var due = list.Where(r => r.DueAt <= DateTime.Now).ToList();
        if (due.Count > 0)
        {
            list.RemoveAll(r => due.Any(d => d.Id == r.Id));
            Save(path, list);
        }
        return due;
    }
}
