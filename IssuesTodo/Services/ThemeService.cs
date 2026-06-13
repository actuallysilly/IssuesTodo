using System.Windows;
using System.Windows.Media;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using IssuesTodo.Models;

namespace IssuesTodo.Services;

public class ThemeService
{
    public void Apply(ThemePreset preset)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.OfType<BundledTheme>().FirstOrDefault();
        var index = existing != null ? dictionaries.IndexOf(existing) : 0;
        if (existing != null) dictionaries.RemoveAt(index);

        dictionaries.Insert(index, new BundledTheme
        {
            BaseTheme = Enum.Parse<BaseTheme>(preset.BaseTheme),
            PrimaryColor = Enum.Parse<PrimaryColor>(preset.PrimaryColor),
            SecondaryColor = Enum.Parse<SecondaryColor>(preset.SecondaryColor)
        });

        // Replace Material's default flat-grey surfaces with a refined near-black palette.
        var helper = new PaletteHelper();
        var theme = helper.GetTheme();
        theme.Background = (Color)ColorConverter.ConvertFromString(preset.Background);
        theme.Cards.Background = (Color)ColorConverter.ConvertFromString(preset.CardBackground);
        helper.SetTheme(theme);
    }
}
