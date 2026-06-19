using System.Windows;
using System.Windows.Input;
using IssuesTodo.Services;
using Microsoft.Win32;

namespace IssuesTodo.Views;

public partial class NewProjectDialog : Window
{
    public string SelectedCategory => CategoryBox.Text.Trim();
    public string ProjectName => NameBox.Text.Trim();
    public string? FolderPath => string.IsNullOrWhiteSpace(FolderBox.Text) ? null : FolderBox.Text.Trim();
    public bool CreateRepo => CreateRepoBox.IsChecked == true;

    private readonly bool _linkMode;

    public NewProjectDialog(IEnumerable<string> existingCategories, bool linkMode = false)
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
        _linkMode = linkMode;

        HeadingText.Text = linkMode ? "Link Existing Project" : "New Project";
        AcceptButton.Content = linkMode ? "LINK" : "CREATE";
        FolderPanel.Visibility = linkMode ? Visibility.Visible : Visibility.Collapsed;
        CreateRepoBox.Visibility = linkMode ? Visibility.Collapsed : Visibility.Visible;

        foreach (var cat in existingCategories)
            CategoryBox.Items.Add(cat);
        Loaded += (_, _) => CategoryBox.Focus();
    }

    private void Accept_Click(object sender, RoutedEventArgs e) => TryAccept();
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Name_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return) TryAccept();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select project folder" };
        if (dialog.ShowDialog() == true)
        {
            FolderBox.Text = dialog.FolderName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
                NameBox.Text = System.IO.Path.GetFileName(dialog.FolderName);
        }
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(SelectedCategory) || string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show("Please fill in both category and project name.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_linkMode && string.IsNullOrWhiteSpace(FolderBox.Text))
        {
            MessageBox.Show("Please select a project folder.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }
}
