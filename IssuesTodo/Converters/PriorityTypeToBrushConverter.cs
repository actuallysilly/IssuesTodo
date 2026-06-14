using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using IssuesTodo.Models;

namespace IssuesTodo.Converters;

public class PriorityTypeToBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not TaskPriority p || values[1] is not TaskType t)
            return Binding.DoNothing;

        bool human = t == TaskType.Human;
        return p switch
        {
            TaskPriority.High     => Brush(human ? (183, 28,  28)  : (239, 83,  80)),   // dark-red / red
            TaskPriority.Low      => Brush(human ? (27,  94,  32)  : (102, 187, 106)),  // dark-green / green
            TaskPriority.Optional => Brush(human ? (245, 127, 23)  : (255, 213, 79)),   // dark-amber / yellow
            _                     => Brush(human ? (55,  71,  79)  : (120, 144, 156)),  // dark-grey / grey
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush Brush((int r, int g, int b) c) =>
        new(Color.FromRgb((byte)c.r, (byte)c.g, (byte)c.b));
}
