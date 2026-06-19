using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using IssuesTodo.Models;

namespace IssuesTodo.Converters;

public class PriorityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is TaskPriority p ? p switch
        {
            TaskPriority.Urgent   => new SolidColorBrush(Color.FromRgb(240, 98, 146)),   // hot-pink
            TaskPriority.High     => new SolidColorBrush(Color.FromRgb(239, 83, 80)),   // red
            TaskPriority.Low      => new SolidColorBrush(Color.FromRgb(102, 187, 106)), // green
            TaskPriority.Optional => new SolidColorBrush(Color.FromRgb(255, 213, 79)),   // yellow
            _                     => new SolidColorBrush(Color.FromRgb(120, 144, 156))  // grey
        } : Binding.DoNothing;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
