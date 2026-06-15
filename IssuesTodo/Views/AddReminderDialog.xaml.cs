using System.Windows;
using IssuesTodo.Services;

namespace IssuesTodo.Views;

public partial class AddReminderDialog : Window
{
    public string ReminderText { get; private set; } = "";
    public DateTime DueAt { get; private set; }

    public AddReminderDialog()
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
        DatePicker.SelectedDate = DateTime.Today;
        TimeBox.Text = DateTime.Now.AddHours(1).ToString("HH:00");
        Loaded += (_, _) => TextBox.Focus();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(TextBox.Text))
        {
            ErrorText.Text = "Enter a reminder message.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (DatePicker.SelectedDate is not DateTime date)
        {
            ErrorText.Text = "Pick a date.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (!TimeSpan.TryParse(TimeBox.Text, out var time))
        {
            ErrorText.Text = "Time must be HH:mm (e.g. 14:30).";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var due = date.Date + time;
        if (due <= DateTime.Now)
        {
            ErrorText.Text = "Due time must be in the future.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        ReminderText = TextBox.Text.Trim();
        DueAt = due;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
