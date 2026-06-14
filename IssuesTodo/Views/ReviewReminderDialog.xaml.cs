using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using IssuesTodo.Services;
using IssuesTodo.ViewModels;

namespace IssuesTodo.Views;

public partial class ReviewReminderDialog : Window
{
    private readonly MainViewModel _vm;
    private readonly ObservableCollection<string> _archived;
    private readonly ObservableCollection<string> _maybes;

    public ReviewReminderDialog(MainViewModel vm, IEnumerable<string> archived, IEnumerable<string> maybes)
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
        _vm = vm;
        _archived = new ObservableCollection<string>(archived);
        _maybes   = new ObservableCollection<string>(maybes);

        ArchivedList.ItemsSource = _archived;
        MaybeList.ItemsSource    = _maybes;

        ArchivedHeader.Visibility = _archived.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ArchivedList.Visibility   = _archived.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        MaybeHeader.Visibility    = _maybes.Count > 0   ? Visibility.Visible : Visibility.Collapsed;
        MaybeList.Visibility      = _maybes.Count > 0   ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string name)
        {
            _vm.UnarchiveProject(name);
            _archived.Remove(name);
        }
    }

    private void Promote_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string name)
        {
            _vm.PromoteMaybeProject(name);
            _maybes.Remove(name);
        }
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e) => Close();
}
