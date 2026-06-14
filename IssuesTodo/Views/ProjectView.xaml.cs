using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IssuesTodo.ViewModels;

namespace IssuesTodo.Views;

public partial class ProjectView : UserControl
{
    private Point _dragStart;
    private TaskViewModel? _dragging;

    public ProjectView()
    {
        InitializeComponent();
    }

    private void TaskRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
    }

    private void TaskRow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragging != null) return;
        if ((sender as FrameworkElement)?.DataContext is not TaskViewModel tvm) return;

        var pos = e.GetPosition(null);
        var diff = _dragStart - pos;
        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance) return;

        _dragging = tvm;
        DragDrop.DoDragDrop((FrameworkElement)sender, tvm, DragDropEffects.Move);
        _dragging = null;
    }

    private void TaskList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void TaskList_Drop(object sender, DragEventArgs e)
    {
        if (_dragging == null || sender is not ItemsControl list) return;
        if (DataContext is not ProjectViewModel pvm) return;

        var openTasks = pvm.OpenTasks.ToList();
        int fromIdx = openTasks.IndexOf(_dragging);
        if (fromIdx < 0) return;

        // Find where the mouse landed
        var pos = e.GetPosition(list);
        int toIdx = openTasks.Count; // default: end

        for (int i = 0; i < list.Items.Count; i++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container) continue;
            var containerPos = container.TranslatePoint(new Point(0, 0), list);
            if (pos.Y < containerPos.Y + container.ActualHeight / 2.0)
            {
                toIdx = i;
                break;
            }
        }

        if (toIdx == fromIdx || toIdx == fromIdx + 1) return;

        var ordered = openTasks.Select(t => t.Model.Text).ToList();
        ordered.RemoveAt(fromIdx);
        int insert = toIdx > fromIdx ? toIdx - 1 : toIdx;
        ordered.Insert(insert, _dragging.Model.Text);

        pvm.ReorderCallback?.Invoke(ordered);
    }
}
