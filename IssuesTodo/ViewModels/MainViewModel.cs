using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using IssuesTodo.Models;
using IssuesTodo.Services;

namespace IssuesTodo.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IssuesFileService _issues;
    private readonly FileWatcherService _watcher;
    private readonly ProjectService _projects;

    [ObservableProperty] private ObservableCollection<CategoryViewModel> _categories = [];
    [ObservableProperty] private ProjectViewModel? _selectedProject;
    [ObservableProperty] private bool _isLoading = true;

    [ObservableProperty] private ObservableCollection<TaskViewModel> _generalTodos = [];
    [ObservableProperty] private string _newGeneralTodoText = "";
    [ObservableProperty] private TaskPriority _newGeneralTodoPriority = TaskPriority.Normal;

    public IEnumerable<TaskPriority> PriorityOptions { get; } = Enum.GetValues<TaskPriority>();

    // Carries in-session done/undone toggles across reloads (e.g. triggered by adding a task)
    // until they're persisted on exit, keyed by task id.
    private readonly Dictionary<string, bool> _pendingToggles = new();

    public bool HasSelectedProject => SelectedProject != null;
    public AppSettings Settings => _settings.Current;

    public MainViewModel(SettingsService settings, IssuesFileService issues,
                         FileWatcherService watcher, ProjectService projects)
    {
        _settings = settings;
        _issues = issues;
        _watcher = watcher;
        _projects = projects;

        _watcher.IssuesChanged += () => Application.Current.Dispatcher.Invoke(Reload);
    }

    partial void OnSelectedProjectChanged(ProjectViewModel? oldValue, ProjectViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
        OnPropertyChanged(nameof(HasSelectedProject));
    }

    public void Initialize()
    {
        _settings.Load();
        _watcher.Watch(_settings.Current.IssuesFilePath);
        Reload();
    }

    private void Reload()
    {
        var prevSelected = SelectedProject?.Name;

        // Capture any in-session toggles before the view models get rebuilt
        foreach (var task in Categories.SelectMany(c => c.Projects).SelectMany(p => p.Tasks))
        {
            if (task.IsDone != task.WasAlreadyDone) _pendingToggles[task.Model.Id] = task.IsDone;
            else _pendingToggles.Remove(task.Model.Id);
        }

        List<CategoryGroup> cats;
        try { cats = _issues.ParseIssues(_settings.Current.IssuesFilePath); }
        catch { cats = []; }

        var done = _issues.ReadDone(_settings.Current.DoneFilePath);
        var doneSet = done.Done.ToHashSet();
        var devRoot = _settings.Current.DevRoot;
        var archived = _settings.Current.ArchivedProjects;

        Categories = new ObservableCollection<CategoryViewModel>(
            cats.Select(cat =>
            {
                var cvm = new CategoryViewModel(cat.Name);
                foreach (var proj in cat.Projects)
                {
                    if (archived.Contains(proj.Name)) continue;

                    var key = FolderKey(cat.Name, proj.Name);

                    proj.FolderPath =
                        _settings.Current.ProjectFolders.TryGetValue(key, out var overridePath)
                        && Directory.Exists(overridePath)
                            ? overridePath
                            : _projects.FindFolderForProject(devRoot, cat.Name, proj.Name);

                    proj.RepoUrl = _settings.Current.ProjectRepos.TryGetValue(key, out var repoUrl) ? repoUrl : null;

                    var pvm = new ProjectViewModel(proj,
                        p => { if (p.FolderPath != null) _projects.OpenInVSCode(p.FolderPath); },
                        p => { if (p.RepoUrl != null) _projects.OpenUrl(p.RepoUrl); },
                        p => ArchiveProject(p),
                        p => AddTask(p));

                    foreach (var task in proj.Tasks)
                    {
                        var capturedPvm = pvm;
                        var wasAlreadyDone = doneSet.Contains(task.Id);
                        var isDone = _pendingToggles.TryGetValue(task.Id, out var pending) ? pending : wasAlreadyDone;

                        // Persisting to done.json is deferred until exit (FlushPendingCompletions),
                        // so completed tasks stay visible with a strikethrough for the rest of the session.
                        pvm.Tasks.Add(new TaskViewModel(task, isDone, wasAlreadyDone,
                            (t, _) => capturedPvm.Refresh(),
                            (t, newText, newPriority) => EditTask(capturedPvm, t, newText, newPriority)));
                    }

                    cvm.Projects.Add(pvm);
                }
                return cvm;
            })
        );

        List<TaskItem> generalTodoItems;
        try { generalTodoItems = _issues.ParseGeneralTodos(_settings.Current.GeneralTodoFilePath); }
        catch { generalTodoItems = []; }

        GeneralTodos = new ObservableCollection<TaskViewModel>(
            generalTodoItems.Select(item => new TaskViewModel(item, isDone: false, wasAlreadyDone: false,
                onDoneChanged: (t, isDone) => { if (isDone) RemoveGeneralTodo(t); },
                onEdit: (t, newText, newPriority) => EditGeneralTodo(t, newText, newPriority)))
        );

        IsLoading = false;

        if (prevSelected != null)
            SelectedProject = Categories.SelectMany(c => c.Projects).FirstOrDefault(p => p.Name == prevSelected);
        SelectedProject ??= Categories.FirstOrDefault()?.Projects.FirstOrDefault();
    }

    private void AddTask(ProjectViewModel pvm)
    {
        var text = pvm.NewTaskText.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        _issues.AddTask(_settings.Current.IssuesFilePath, pvm.Category, pvm.Name, text, pvm.NewTaskPriority);
        pvm.NewTaskText = "";
        pvm.NewTaskPriority = TaskPriority.Normal;
        // FileWatcher triggers Reload automatically
    }

    private void EditTask(ProjectViewModel pvm, TaskViewModel task, string newText, TaskPriority newPriority)
    {
        _issues.EditTask(_settings.Current.IssuesFilePath, pvm.Category, pvm.Name, task.Model.Text, newText, newPriority);
        // FileWatcher triggers Reload automatically
    }

    [RelayCommand]
    private void AddGeneralTodo()
    {
        var text = NewGeneralTodoText.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        _issues.AddGeneralTodo(_settings.Current.GeneralTodoFilePath, text, NewGeneralTodoPriority);
        NewGeneralTodoText = "";
        NewGeneralTodoPriority = TaskPriority.Normal;
        Reload();
    }

    private void RemoveGeneralTodo(TaskViewModel task)
    {
        _issues.RemoveGeneralTodo(_settings.Current.GeneralTodoFilePath, task.Model.Text);
        GeneralTodos.Remove(task);
    }

    private void EditGeneralTodo(TaskViewModel task, string newText, TaskPriority newPriority)
    {
        _issues.EditGeneralTodo(_settings.Current.GeneralTodoFilePath, task.Model.Text, newText, newPriority);
        Reload();
    }

    /// Persists every done/undone toggle made during the session to done.json.
    /// Called on application exit — until then, completed tasks just show struck through.
    public void FlushPendingCompletions()
    {
        foreach (var pvm in Categories.SelectMany(c => c.Projects))
        {
            foreach (var task in pvm.Tasks)
            {
                if (task.IsDone == task.WasAlreadyDone) continue;
                if (task.IsDone)
                {
                    _issues.MarkDone(_settings.Current.DoneFilePath, task.Model);
                    _issues.RemoveTask(_settings.Current.IssuesFilePath, pvm.Category, pvm.Name, task.Model.Text);
                }
                else
                {
                    _issues.UnmarkDone(_settings.Current.DoneFilePath, task.Model.Id);
                }
            }
        }
    }

    private void ArchiveProject(ProjectViewModel pvm)
    {
        _settings.Current.ArchivedProjects.Add(pvm.Name);
        _settings.Save();
        Reload();
    }

    public void UnarchiveProject(string projectName)
    {
        _settings.Current.ArchivedProjects.Remove(projectName);
        _settings.Save();
        Reload();
    }

    public void SetProjectFolder(ProjectViewModel pvm, string? path)
    {
        var key = FolderKey(pvm.Category, pvm.Name);
        if (string.IsNullOrWhiteSpace(path))
            _settings.Current.ProjectFolders.Remove(key);
        else
            _settings.Current.ProjectFolders[key] = path;
        _settings.Save();
        Reload();
    }

    public void SetProjectRepo(ProjectViewModel pvm, string? url)
    {
        var key = FolderKey(pvm.Category, pvm.Name);
        if (string.IsNullOrWhiteSpace(url))
            _settings.Current.ProjectRepos.Remove(key);
        else
            _settings.Current.ProjectRepos[key] = url;
        _settings.Save();
        Reload();
    }

    private static string FolderKey(string category, string projectName) => $"{category}|{projectName}";

    public void CreateProject(string category, string name)
    {
        _projects.CreateProject(_settings.Current.DevRoot, category, name, _settings.Current.IssuesFilePath);
        _issues.AddProject(_settings.Current.IssuesFilePath, category, name);
        // FileWatcher triggers Reload automatically
    }

    public void ApplySettings()
    {
        _settings.Save();
        _watcher.Watch(_settings.Current.IssuesFilePath);
        Reload();
    }

    public IEnumerable<string> ExistingCategories => Categories.Select(c => c.Name).Distinct();
}
