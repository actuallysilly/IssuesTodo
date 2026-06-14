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
        var dialog = new NewProjectDialog(VM?.ExistingCategories ?? [], linkMode: false)
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            VM?.CreateProject(dialog.SelectedCategory, dialog.ProjectName);
    }

    private void LinkExisting_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewProjectDialog(VM?.ExistingCategories ?? [], linkMode: true)
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            VM?.LinkExistingProject(dialog.SelectedCategory, dialog.ProjectName, dialog.FolderPath);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(VM!) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ProjectViewModel pvm || VM == null) return;

        var dialog = new TextInputDialog($"Rename — {pvm.Name}", "New name", pvm.Name, pvm.Name)
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Value) && dialog.Value != pvm.Name)
            VM.RenameProject(pvm, dialog.Value);
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

    private void SetState_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ProjectViewModel pvm) return;

        var folder = pvm.FolderPath;
        if (string.IsNullOrEmpty(folder))
        {
            System.Windows.MessageBox.Show(
                $"'{pvm.Name}' has no linked folder. Set a root folder first.",
                "No folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        var claudeDir = System.IO.Path.Combine(folder, ".claude");
        System.IO.Directory.CreateDirectory(claudeDir);
        var statePath = System.IO.Path.Combine(claudeDir, "STATE.md");
        var existing = System.IO.File.Exists(statePath)
            ? System.IO.File.ReadAllText(statePath, System.Text.Encoding.UTF8)
            : null;

        var dialog = new CommentDialog($"{pvm.Name} — current state", existing)
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;

        if (dialog.Comment == null)
            System.IO.File.Delete(statePath);
        else
            System.IO.File.WriteAllText(statePath, dialog.Comment, System.Text.Encoding.UTF8);
    }

    private void ToggleMaybe_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ProjectViewModel pvm && VM != null)
            VM.ToggleMaybeProject(pvm);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ProjectViewModel pvm || VM == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Delete '{pvm.Name}' and all its tasks?\nThis cannot be undone.",
            "Delete project",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.OK)
            VM.DeleteProject(pvm);
    }
}
