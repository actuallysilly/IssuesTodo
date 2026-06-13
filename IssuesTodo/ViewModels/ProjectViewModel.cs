using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using IssuesTodo.Models;

namespace IssuesTodo.ViewModels;

public partial class ProjectViewModel : ObservableObject
{
    public Project Model { get; }
    public ObservableCollection<TaskViewModel> Tasks { get; } = [];

    public string Name => Model.Name;
    public string Category => Model.Category;
    public string? FolderPath => Model.FolderPath;
    public bool HasFolder => FolderPath != null;
    public string? RepoUrl => Model.RepoUrl;
    public bool HasRepo => !string.IsNullOrEmpty(RepoUrl);

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _newTaskText = "";
    [ObservableProperty] private TaskPriority _newTaskPriority = TaskPriority.Normal;

    /// Tasks to display: anything not already-done at session start, including
    /// ones completed during this session (shown struck through until exit).
    public IEnumerable<TaskViewModel> OpenTasks => Tasks.Where(t => !t.WasAlreadyDone);
    public bool HasVisibleTasks => Tasks.Any(t => !t.WasAlreadyDone);

    public int OpenTaskCount => Tasks.Count(t => !t.IsDone);
    public bool HasOpenTasks => OpenTaskCount > 0;

    public IEnumerable<TaskPriority> PriorityOptions { get; } = Enum.GetValues<TaskPriority>();

    public IRelayCommand OpenInCodeCommand { get; }
    public IRelayCommand OpenRepoCommand { get; }
    public IRelayCommand ArchiveCommand { get; }
    public IRelayCommand AddTaskCommand { get; }

    public ProjectViewModel(Project model, Action<ProjectViewModel> openInCode, Action<ProjectViewModel> openRepo,
                            Action<ProjectViewModel> archive, Action<ProjectViewModel> addTask)
    {
        Model = model;
        OpenInCodeCommand = new RelayCommand(() => openInCode(this), () => HasFolder);
        OpenRepoCommand = new RelayCommand(() => openRepo(this), () => HasRepo);
        ArchiveCommand = new RelayCommand(() => archive(this));
        AddTaskCommand = new RelayCommand(() => addTask(this), () => !string.IsNullOrWhiteSpace(NewTaskText));
    }

    partial void OnNewTaskTextChanged(string value) => AddTaskCommand.NotifyCanExecuteChanged();

    public void Refresh()
    {
        OnPropertyChanged(nameof(OpenTasks));
        OnPropertyChanged(nameof(HasVisibleTasks));
        OnPropertyChanged(nameof(OpenTaskCount));
        OnPropertyChanged(nameof(HasOpenTasks));
    }
}
