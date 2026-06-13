using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IssuesTodo.ViewModels;
using Microsoft.Win32;

namespace IssuesTodo.Views;

public partial class Sidebar : UserControl
{
    public Sidebar()
    {
        InitializeComponent();
    }

    private MainViewModel? VM => DataContext as MainViewModel;

    private void ProjectRow_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ProjectViewModel pvm && VM != null)
            VM.SelectedProject = pvm;
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewProjectDialog(VM?.ExistingCategories ?? []) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            VM?.CreateProject(dialog.SelectedCategory, dialog.ProjectName);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(VM!) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
    }

    private void SetRootFolder_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ProjectViewModel pvm || VM == null) return;

        var dialog = new OpenFolderDialog
        {
            Title = $"Select root folder for '{pvm.Name}'",
            FolderName = pvm.FolderPath
        };
        if (dialog.ShowDialog() == true)
            VM.SetProjectFolder(pvm, dialog.FolderName);
    }

    private void ResetRootFolder_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ProjectViewModel pvm && VM != null)
            VM.SetProjectFolder(pvm, null);
    }

    private void SetRepoLink_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ProjectViewModel pvm || VM == null) return;

        var dialog = new TextInputDialog($"Repository link — {pvm.Name}", "Repository link",
            "https://github.com/user/repo", pvm.RepoUrl) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            VM.SetProjectRepo(pvm, dialog.Value);
    }

    private void RemoveRepoLink_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ProjectViewModel pvm && VM != null)
            VM.SetProjectRepo(pvm, null);
    }
}
