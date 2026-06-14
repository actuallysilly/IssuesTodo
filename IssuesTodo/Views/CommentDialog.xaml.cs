using System.Windows;
using IssuesTodo.Services;

namespace IssuesTodo.Views;

public partial class CommentDialog : Window
{
    public string? Comment { get; private set; }

    public CommentDialog(string taskText, string? currentComment)
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
        TaskLabel.Text = taskText;
        CommentBox.Text = currentComment ?? "";
        Loaded += (_, _) => { CommentBox.Focus(); CommentBox.CaretIndex = CommentBox.Text.Length; };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Comment = string.IsNullOrWhiteSpace(CommentBox.Text) ? null : CommentBox.Text.Trim();
        DialogResult = true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Comment = null;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
