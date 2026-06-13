namespace IssuesTodo.Models;

public record ThemePreset(string Name, string BaseTheme, string PrimaryColor, string SecondaryColor, string Background, string CardBackground);

public static class ThemePresets
{
    public static readonly ThemePreset[] All =
    [
        new("Midnight", "Dark", "Indigo",     "Cyan",   "#FF12141A", "#FF1B1E27"),
        new("Graphite", "Dark", "BlueGrey",   "Teal",   "#FF131415", "#FF1C1D1F"),
        new("Velvet",   "Dark", "DeepPurple", "Pink",   "#FF151219", "#FF1F1A28"),
        new("Ember",    "Dark", "DeepOrange", "Amber",  "#FF161311", "#FF231D19"),
    ];

    public static ThemePreset Default => All[0];

    public static ThemePreset Find(string? name) => All.FirstOrDefault(t => t.Name == name) ?? Default;
}
