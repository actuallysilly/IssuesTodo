using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private uint _capturedMods;
    private uint _capturedVk;
    private bool _capturing;

    private record ReviewFreqOption(string Label, string Value);
    private static readonly ReviewFreqOption[] FreqOptions =
    [
        new("Never",        "never"),
        new("Every week",   "1w"),
        new("Every 2 weeks","2w"),
        new("Monthly",      "1month"),
    ];

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

        _capturedMods = vm.Settings.HotkeyModifiers;
        _capturedVk   = vm.Settings.HotkeyVirtualKey;
        HotkeyBox.Text = FormatHotkey(_capturedMods, _capturedVk);

        ReviewFreqBox.ItemsSource  = FreqOptions;
        ReviewFreqBox.SelectedItem = FreqOptions.FirstOrDefault(o => o.Value == vm.Settings.ReviewFrequency)
                                     ?? FreqOptions.Last();

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

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _capturing = true;
        HotkeyBox.Text = "Press a key combination...";
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_capturing)
        {
            _capturing = false;
            HotkeyBox.Text = FormatHotkey(_capturedMods, _capturedVk);
        }
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_capturing) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            _capturing = false;
            HotkeyBox.Text = FormatHotkey(_capturedMods, _capturedVk);
            Keyboard.ClearFocus();
            e.Handled = true;
            return;
        }

        // Ignore bare modifier presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return;

        uint mods = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= 0x0002;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))   mods |= 0x0004;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))     mods |= 0x0001;

        _capturedMods = mods;
        _capturedVk   = (uint)KeyInterop.VirtualKeyFromKey(key);
        _capturing = false;
        HotkeyBox.Text = FormatHotkey(_capturedMods, _capturedVk);
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void ClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        _capturedMods = 0;
        _capturedVk   = 0;
        _capturing    = false;
        HotkeyBox.Text = FormatHotkey(0, 0);
    }

    private static string FormatHotkey(uint mods, uint vk)
    {
        if (vk == 0) return "(none)";
        var parts = new List<string>();
        if ((mods & 0x0001) != 0) parts.Add("Alt");
        if ((mods & 0x0002) != 0) parts.Add("Ctrl");
        if ((mods & 0x0004) != 0) parts.Add("Shift");
        parts.Add(KeyInterop.KeyFromVirtualKey((int)vk).ToString());
        return string.Join("+", parts);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.Settings.DevRoot = DevRootBox.Text.Trim();
        _vm.Settings.IssuesFilePath = IssuesPathBox.Text.Trim();
        _vm.Settings.DoneFilePath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(IssuesPathBox.Text.Trim()) ?? "",
            "issues.done.json");
        if (ThemeBox.SelectedItem is ThemePreset preset) _vm.Settings.Theme = preset.Name;
        _vm.Settings.HotkeyModifiers  = _capturedMods;
        _vm.Settings.HotkeyVirtualKey = _capturedVk;
        if (ReviewFreqBox.SelectedItem is ReviewFreqOption freq)
            _vm.Settings.ReviewFrequency = freq.Value;
        _vm.ApplySettings();
        ((MainWindow)Application.Current.MainWindow!).ReregisterHotkey(_capturedMods, _capturedVk);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
