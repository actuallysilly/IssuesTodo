using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IssuesTodo.Models;

namespace IssuesTodo.ViewModels;

public partial class TaskViewModel : ObservableObject
{
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editText = "";
    [ObservableProperty] private TaskPriority _editPriority;
    [ObservableProperty] private TaskType _editType;

    public TaskItem Model { get; }

    /// Whether this task was already marked done when the session started — tasks
    /// completed during the current session stay visible (struck through) until exit.
    public bool WasAlreadyDone { get; }

    public string? Comment => Model.Comment;
    public bool HasComment => !string.IsNullOrEmpty(Model.Comment);
    public bool IsHuman => Model.Type == TaskType.Human;

    public IEnumerable<TaskPriority> PriorityOptions { get; } = Enum.GetValues<TaskPriority>();
    public IEnumerable<TaskType> TypeOptions { get; } = Enum.GetValues<TaskType>();

    public IRelayCommand BeginEditCommand { get; }
    public IRelayCommand CommitEditCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IRelayCommand EditCommentCommand { get; }

    private readonly Action<TaskViewModel, bool>? _onDoneChanged;
    private readonly Action<TaskViewModel, string, TaskPriority, TaskType>? _onEdit;
    private readonly Action<TaskViewModel>? _onEditComment;

    public TaskViewModel(TaskItem model, bool isDone, bool wasAlreadyDone,
                         Action<TaskViewModel, bool>? onDoneChanged = null,
                         Action<TaskViewModel, string, TaskPriority, TaskType>? onEdit = null,
                         Action<TaskViewModel>? onEditComment = null)
    {
        Model = model;
        _isDone = isDone;
        WasAlreadyDone = wasAlreadyDone;
        _onDoneChanged = onDoneChanged;
        _onEdit = onEdit;
        _onEditComment = onEditComment;

        BeginEditCommand = new RelayCommand(BeginEdit);
        CommitEditCommand = new RelayCommand(CommitEdit, () => !string.IsNullOrWhiteSpace(EditText));
        CancelEditCommand = new RelayCommand(() => IsEditing = false);
        EditCommentCommand = new RelayCommand(() => _onEditComment?.Invoke(this));
    }

    partial void OnIsDoneChanged(bool value) => _onDoneChanged?.Invoke(this, value);
    partial void OnEditTextChanged(string value) => CommitEditCommand.NotifyCanExecuteChanged();

    private void BeginEdit()
    {
        EditText = Model.Text;
        EditPriority = Model.Priority;
        EditType = Model.Type;
        IsEditing = true;
    }

    private void CommitEdit()
    {
        var text = EditText.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        IsEditing = false;
        if (text != Model.Text || EditPriority != Model.Priority || EditType != Model.Type)
            _onEdit?.Invoke(this, text, EditPriority, EditType);
    }
}
