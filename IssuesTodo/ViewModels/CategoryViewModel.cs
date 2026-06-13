using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace IssuesTodo.ViewModels;

public partial class CategoryViewModel : ObservableObject
{
    [ObservableProperty] private bool _isExpanded = true;

    public string Name { get; }
    public ObservableCollection<ProjectViewModel> Projects { get; } = [];

    public CategoryViewModel(string name) => Name = name;
}
