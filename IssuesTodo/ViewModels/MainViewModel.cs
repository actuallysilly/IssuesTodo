using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using IssuesTodo.Models;
using IssuesTodo.Services;

namespace IssuesTodo.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IssuesFileService _issues;
    private readonly FileWatcherService _watcher;
    private readonly ProjectService _projects;
    private readonly RemindersService _reminders;

    [ObservableProperty] private ObservableCollection<CategoryViewModel> _categories = [];
    [ObservableProperty] private ProjectViewModel? _selectedProject;
    [ObservableProperty] private bool _isLoading = true;

    [ObservableProperty] private ObservableCollection<TaskViewModel> _generalTodos = [];
    [ObservableProperty] private string _newGeneralTodoText = "";
    [ObservableProperty] private TaskPriority _newGeneralTodoPriority = TaskPriority.Normal;

    [ObservableProperty] private bool _nagVisible = false;
    [ObservableProperty] private string _nagMessage = "";

    [ObservableProperty] private ObservableCollection<Reminder> _pendingReminders = [];
    public bool HasPendingReminders => PendingReminders.Count > 0;

    partial void OnPendingRemindersChanged(ObservableCollection<Reminder> value)
    {
        value.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPendingReminders));
        OnPropertyChanged(nameof(HasPendingReminders));
    }

    public bool ShowMaybeProjects => _settings.Current.ShowMaybeProjects;

    public IEnumerable<TaskPriority> PriorityOptions { get; } = Enum.GetValues<TaskPriority>();

    // Carries in-session done/undone toggles across reloads until persisted on exit.
    private readonly Dictionary<string, bool> _pendingToggles = new();
    private bool _nagDismissed = false;
    private List<CategoryGroup> _allParsedCategories = [];

    public bool HasSelectedProject => SelectedProject != null;
    public AppSettings Settings => _settings.Current;

    public MainViewModel(SettingsService settings, IssuesFileService issues,
                         FileWatcherService watcher, ProjectService projects,
                         RemindersService reminders)
    {
        _settings = settings;
        _issues = issues;
        _watcher = watcher;
        _projects = projects;
        _reminders = reminders;

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
        LoadPendingReminders();
        StartReminderTimer();
    }

    private void StartReminderTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += (_, _) => CheckReminders();
        timer.Start();
    }

    private void CheckReminders()
    {
        var due = _reminders.PopDue(_settings.Current.RemindersFilePath);
        foreach (var r in due)
        {
            Application.Current.MainWindow?.Activate();
            MessageBox.Show(r.Text, "Reminder", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        if (due.Count > 0) LoadPendingReminders();
    }

    private void LoadPendingReminders()
    {
        var all = _reminders.Load(_settings.Current.RemindersFilePath);
        PendingReminders = new ObservableCollection<Reminder>(all.OrderBy(r => r.DueAt));
    }

    public void AddReminder(string text, DateTime dueAt)
    {
        _reminders.Add(_settings.Current.RemindersFilePath, text, dueAt);
        LoadPendingReminders();
    }

    [RelayCommand]
    private void DismissReminder(Reminder r)
    {
        _reminders.Remove(_settings.Current.RemindersFilePath, r.Id);
        PendingReminders.Remove(r);
    }

    private void Reload()
    {
        var prevSelected = SelectedProject?.Name;

        foreach (var task in Categories.SelectMany(c => c.Projects).SelectMany(p => p.Tasks))
        {
            if (task.IsDone != task.WasAlreadyDone) _pendingToggles[task.Model.Id] = task.IsDone;
            else _pendingToggles.Remove(task.Model.Id);
        }

        List<CategoryGroup> cats;
        try { cats = _issues.ParseIssues(_settings.Current.IssuesFilePath); }
        catch { cats = []; }
        _allParsedCategories = cats;

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
                    if (proj.IsMaybe && !_settings.Current.ShowMaybeProjects) continue;

                    var key = FolderKey(cat.Name, proj.Name);

                    proj.FolderPath =
                        _settings.Current.ProjectFolders.TryGetValue(key, out var overridePath)
                        && Directory.Exists(overridePath)
                            ? overridePath
                            : _projects.FindFolderForProject(devRoot, cat.Name, proj.Name);

                    proj.RepoUrl = _settings.Current.ProjectRepos.TryGetValue(key, out var repoUrl) ? repoUrl : null;

                    var capturedPvm = default(ProjectViewModel)!;
                    capturedPvm = new ProjectViewModel(proj,
                        p => { if (p.FolderPath != null) _projects.OpenInVSCode(p.FolderPath); },
                        p => { if (p.RepoUrl != null) _projects.OpenUrl(p.RepoUrl); },
                        p => { if (p.FolderPath != null) _projects.OpenFolder(p.FolderPath); },
                        p => ArchiveProject(p),
                        p => AddTask(p));

                    capturedPvm.OnEditComment = (tvm, comment) => EditTaskComment(capturedPvm, tvm, comment);
                    capturedPvm.ReorderCallback = orderedTexts => ReorderTasks(capturedPvm, orderedTexts);

                    foreach (var task in proj.Tasks)
                    {
                        var capturedTask = capturedPvm;
                        var wasAlreadyDone = doneSet.Contains(task.Id);
                        var isDone = _pendingToggles.TryGetValue(task.Id, out var pending) ? pending : wasAlreadyDone;

                        capturedPvm.Tasks.Add(new TaskViewModel(task, isDone, wasAlreadyDone,
                            (t, isDoneNow) =>
                            {
                                if (isDoneNow)
                                {
                                    _issues.MarkDone(_settings.Current.DoneFilePath, t.Model);
                                    _issues.RemoveTask(_settings.Current.IssuesFilePath, capturedTask.Category, capturedTask.Name, t.Model.Text);
                                }
                                capturedTask.Refresh();
                            },
                            (t, newText, newPriority, newType) => EditTask(capturedTask, t, newText, newPriority, newType),
                            t => ShowCommentEditor(capturedTask, t)));
                    }

                    cvm.Projects.Add(capturedPvm);
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
                onEdit: (t, newText, newPriority, newType) => EditGeneralTodo(t, newText, newPriority)))
        );

        IsLoading = false;

        if (prevSelected != null)
            SelectedProject = Categories.SelectMany(c => c.Projects).FirstOrDefault(p => p.Name == prevSelected);
        SelectedProject ??= Categories.FirstOrDefault()?.Projects.FirstOrDefault();

        if (!_nagDismissed) UpdateNag();
    }

    private void UpdateNag()
    {
        var hpByProject = Categories
            .SelectMany(c => c.Projects)
            .SelectMany(p => p.OpenTasks.Select(t => (task: t, proj: p)))
            .Where(x => x.task.Model.Priority == TaskPriority.High && !x.task.IsDone)
            .GroupBy(x => x.proj.Name)
            .ToList();

        if (hpByProject.Count > 0)
        {
            var total = hpByProject.Sum(g => g.Count());
            var projNames = string.Join(", ", hpByProject.Select(g => g.Key));
            var taskWord = total == 1 ? "task" : "tasks";
            NagMessage = $"{total} high priority {taskWord} need attention — {projNames}";
            NagVisible = true;
        }
        else
        {
            NagVisible = false;
        }
    }

    [RelayCommand]
    private void ToggleMaybeProjects()
    {
        _settings.Current.ShowMaybeProjects = !_settings.Current.ShowMaybeProjects;
        _settings.Save();
        OnPropertyChanged(nameof(ShowMaybeProjects));
        Reload();
    }

    [RelayCommand]
    private void DismissNag()
    {
        _nagDismissed = true;
        NagVisible = false;
    }

    private void ShowCommentEditor(ProjectViewModel pvm, TaskViewModel tvm)
    {
        var window = Application.Current.MainWindow;
        var dialog = new Views.CommentDialog(tvm.Model.Text, tvm.Model.Comment) { Owner = window };
        if (dialog.ShowDialog() == true)
            EditTaskComment(pvm, tvm, dialog.Comment);
    }

    private void AddTask(ProjectViewModel pvm)
    {
        var text = pvm.NewTaskText.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        _issues.AddTask(_settings.Current.IssuesFilePath, pvm.Category, pvm.Name, text, pvm.NewTaskPriority, pvm.NewTaskType);
        pvm.NewTaskText = "";
        pvm.NewTaskPriority = TaskPriority.Normal;
        pvm.NewTaskType = TaskType.Dev;
    }

    private void EditTask(ProjectViewModel pvm, TaskViewModel task, string newText, TaskPriority newPriority, TaskType newType)
    {
        _issues.EditTask(_settings.Current.IssuesFilePath, pvm.Category, pvm.Name, task.Model.Text, newText, newPriority, newType);
    }

    private void EditTaskComment(ProjectViewModel pvm, TaskViewModel task, string? comment)
    {
        _issues.EditComment(_settings.Current.IssuesFilePath, pvm.Category, pvm.Name, task.Model.Text, comment);
    }

    private void ReorderTasks(ProjectViewModel pvm, IList<string> orderedTexts)
    {
        _issues.ReorderTasks(_settings.Current.IssuesFilePath, pvm.Category, pvm.Name, orderedTexts);
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

    public void ToggleMaybeProject(ProjectViewModel pvm)
    {
        _issues.ToggleMaybeProject(_settings.Current.IssuesFilePath, pvm.Category, pvm.Name);
        // FileWatcher triggers Reload
    }

    public void PromoteMaybeProject(string projectName)
    {
        var proj = _allParsedCategories.SelectMany(c => c.Projects).FirstOrDefault(p => p.Name == projectName && p.IsMaybe);
        if (proj != null)
            _issues.ToggleMaybeProject(_settings.Current.IssuesFilePath, proj.Category, projectName);
    }

    public void ShowReviewReminderIfDue(System.Windows.Window owner)
    {
        var freq = _settings.Current.ReviewFrequency;
        if (freq == "never") return;

        int days = freq switch { "1w" => 7, "2w" => 14, _ => 30 };

        if (!string.IsNullOrEmpty(_settings.Current.LastReviewReminder) &&
            DateTime.TryParse(_settings.Current.LastReviewReminder, out var last) &&
            (DateTime.UtcNow - last).TotalDays < days)
            return;

        var archived = _settings.Current.ArchivedProjects.ToList();
        var maybes   = _allParsedCategories.SelectMany(c => c.Projects)
                           .Where(p => p.IsMaybe).Select(p => p.Name).ToList();

        if (archived.Count == 0 && maybes.Count == 0) return;

        _settings.Current.LastReviewReminder = DateTime.UtcNow.ToString("O");
        _settings.Save();

        var dialog = new Views.ReviewReminderDialog(this, archived, maybes) { Owner = owner };
        dialog.ShowDialog();
    }

    public void DeleteProject(ProjectViewModel pvm, string? alsoDeleteFolder = null)
    {
        _issues.DeleteProject(_settings.Current.IssuesFilePath, pvm.Category, pvm.Name);

        var key = FolderKey(pvm.Category, pvm.Name);
        _settings.Current.ProjectFolders.Remove(key);
        _settings.Current.ProjectRepos.Remove(key);
        _settings.Current.ArchivedProjects.Remove(pvm.Name);
        _settings.Save();

        if (!string.IsNullOrEmpty(alsoDeleteFolder) && Directory.Exists(alsoDeleteFolder))
            Directory.Delete(alsoDeleteFolder, recursive: true);
        // FileWatcher triggers Reload
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

    public void RenameProject(ProjectViewModel pvm, string newName)
    {
        _issues.RenameProject(_settings.Current.IssuesFilePath, pvm.Category, pvm.Name, newName);

        var oldKey = FolderKey(pvm.Category, pvm.Name);
        var newKey = FolderKey(pvm.Category, newName);

        if (_settings.Current.ProjectFolders.Remove(oldKey, out var folder))
            _settings.Current.ProjectFolders[newKey] = folder;

        if (_settings.Current.ProjectRepos.Remove(oldKey, out var repo))
            _settings.Current.ProjectRepos[newKey] = repo;

        if (_settings.Current.ArchivedProjects.Remove(pvm.Name))
            _settings.Current.ArchivedProjects.Add(newName);

        _settings.Save();
        // FileWatcher triggers Reload
    }

    public void CreateProject(string category, string name)
    {
        _projects.CreateProject(_settings.Current.DevRoot, category, name, _settings.Current.IssuesFilePath);
        _issues.AddProject(_settings.Current.IssuesFilePath, category, name);
    }

    public void LinkExistingProject(string category, string name, string? folderPath)
    {
        _issues.AddProject(_settings.Current.IssuesFilePath, category, name);
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            _settings.Current.ProjectFolders[FolderKey(category, name)] = folderPath;
            _settings.Save();
        }
        // FileWatcher triggers Reload
    }

    public void ApplySettings()
    {
        _settings.Save();
        _watcher.Watch(_settings.Current.IssuesFilePath);
        Reload();
    }

    public IEnumerable<string> ExistingCategories => Categories.Select(c => c.Name).Distinct();

    private static string FolderKey(string category, string projectName) => $"{category}|{projectName}";
}
