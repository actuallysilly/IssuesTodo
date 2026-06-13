using System.Windows;
using System.Windows.Input;
using IssuesTodo.Services;
using MaterialDesignThemes.Wpf;

namespace IssuesTodo.Views;

public partial class TextInputDialog : Window
{
    public string Value => ValueBox.Text.Trim();

    public TextInputDialog(string title, string heading, string hint, string? initialValue = null)
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);

        Title = title;
        HeadingText.Text = heading;
        ValueBox.Text = initialValue ?? "";
        HintAssist.SetHint(ValueBox, hint);

        Loaded += (_, _) => { ValueBox.Focus(); ValueBox.SelectAll(); };
    }

    private void ValueBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return) Accept();
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Accept();
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Accept() => DialogResult = true;
}
