using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using IssuesTodo.Models;
using IssuesTodo.Services;
using IssuesTodo.ViewModels;

namespace IssuesTodo.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly ThemeService _themes;
    private readonly ThemePreset _originalTheme;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
        _vm = vm;
        _themes = App.Services.GetRequiredService<ThemeService>();
        _originalTheme = ThemePresets.Find(vm.Settings.Theme);

        DevRootBox.Text = vm.Settings.DevRoot;
        IssuesPathBox.Text = vm.Settings.IssuesFilePath;

        ThemeBox.ItemsSource = ThemePresets.All;
        ThemeBox.SelectedItem = _originalTheme;

        RefreshArchivedList();

        // Revert the live preview if the dialog is dismissed without saving
        Closing += (_, _) => { if (DialogResult != true) _themes.Apply(_originalTheme); };
    }

    private void RefreshArchivedList()
    {
        var archived = _vm.Settings.ArchivedProjects.OrderBy(n => n).ToList();
        ArchivedList.ItemsSource = archived;
        EmptyArchivedText.Visibility = archived.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Unarchive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string projectName })
        {
            _vm.UnarchiveProject(projectName);
            RefreshArchivedList();
        }
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeBox.SelectedItem is ThemePreset preset) _themes.Apply(preset);
    }

    private void BrowseDevRoot_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select dev root folder" };
        if (dialog.ShowDialog() == true)
            DevRootBox.Text = dialog.FolderName;
    }

    private void BrowseIssues_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select issues.md",
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            FileName = IssuesPathBox.Text
        };
        if (dialog.ShowDialog() == true)
            IssuesPathBox.Text = dialog.FileName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.Settings.DevRoot = DevRootBox.Text.Trim();
        _vm.Settings.IssuesFilePath = IssuesPathBox.Text.Trim();
        _vm.Settings.DoneFilePath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(IssuesPathBox.Text.Trim()) ?? "",
            "issues.done.json");
        if (ThemeBox.SelectedItem is ThemePreset preset) _vm.Settings.Theme = preset.Name;
        _vm.ApplySettings();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
