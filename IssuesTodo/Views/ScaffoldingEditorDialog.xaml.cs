using System.Windows;
using System.Windows.Controls;
using IssuesTodo.Models;
using IssuesTodo.Services;

namespace IssuesTodo.Views;

public partial class ScaffoldingEditorDialog : Window
{
    private Dictionary<string, string> _templates;
    private string? _currentFile;
    private bool _suppressChange;

    public Dictionary<string, string> Result => _templates;

    public ScaffoldingEditorDialog(Dictionary<string, string> templates)
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
        _templates = new Dictionary<string, string>(templates);
        RefreshList(null);
    }

    private void RefreshList(string? selectKey)
    {
        _suppressChange = true;
        FileList.Items.Clear();
        foreach (var key in _templates.Keys.Order())
            FileList.Items.Add(key);
        _suppressChange = false;

        if (selectKey != null && FileList.Items.Contains(selectKey))
            FileList.SelectedItem = selectKey;
        else if (FileList.Items.Count > 0)
            FileList.SelectedIndex = 0;
    }

    private void SaveCurrent()
    {
        if (_currentFile != null)
            _templates[_currentFile] = ContentBox.Text;
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressChange) return;
        SaveCurrent();
        _currentFile = FileList.SelectedItem as string;
        if (_currentFile != null)
        {
            _suppressChange = true;
            ContentBox.Text = _templates[_currentFile];
            _suppressChange = false;
            FilePathLabel.Text = _currentFile;
            ContentBox.IsEnabled = true;
        }
        else
        {
            ContentBox.IsEnabled = false;
            ContentBox.Text = "";
            FilePathLabel.Text = "Select a file to edit";
        }
    }

    private void ContentBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressChange || _currentFile == null) return;
        _templates[_currentFile] = ContentBox.Text;
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TextInputDialog("Add Scaffolding File", "File path", "e.g. src/template.cs") { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;
        var key = dialog.Value.Trim().Replace('\\', '/');
        if (!_templates.ContainsKey(key)) _templates[key] = "";
        RefreshList(key);
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile == null) return;
        _templates.Remove(_currentFile);
        _currentFile = null;
        RefreshList(null);
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Overwrite all templates with built-in defaults?", "Reset",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _templates = AppSettings.DefaultScaffolding();
        RefreshList(null);
    }

    private void Save_Click(object sender, RoutedEventArgs e) { SaveCurrent(); DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
