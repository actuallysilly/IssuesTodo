using System.Windows;
using System.Windows.Input;
using IssuesTodo.Services;

namespace IssuesTodo.Views;

public partial class NewProjectDialog : Window
{
    public string SelectedCategory => CategoryBox.Text.Trim();
    public string ProjectName => NameBox.Text.Trim();

    public NewProjectDialog(IEnumerable<string> existingCategories)
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
        foreach (var cat in existingCategories)
            CategoryBox.Items.Add(cat);
        Loaded += (_, _) => CategoryBox.Focus();
    }

    private void Create_Click(object sender, RoutedEventArgs e) => TryAccept();
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Name_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return) TryAccept();
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(SelectedCategory) || string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show("Please fill in both category and project name.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }
}
