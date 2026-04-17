using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DS3Go.ViewModels;

namespace DS3Go.Views;

public partial class DashboardView : UserControl
{
    private Point _dragStartPoint;
    private PortViewModel? _dragSource;

    public DashboardView()
    {
        InitializeComponent();
    }

    private void PortCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);

        if (sender is FrameworkElement fe && fe.DataContext is PortViewModel port)
        {
            _dragSource = port;

            // Also select this port on click
            if (DataContext is MainViewModel vm)
                vm.SelectPortCommand.Execute(port);
        }
    }

    private void PortCard_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSource == null)
            return;

        var pos = e.GetPosition(null);
        var diff = pos - _dragStartPoint;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var data = new DataObject("PortVM", _dragSource);
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
            _dragSource = null;
        }
    }

    private void PortCard_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent("PortVM") ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void PortCard_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("PortVM")) return;

        var source = e.Data.GetData("PortVM") as PortViewModel;
        var target = (sender is FrameworkElement fe) ? fe.DataContext as PortViewModel : null;

        if (source == null || target == null || source == target) return;

        if (DataContext is MainViewModel vm)
        {
            vm.SwapPortsCommand.Execute(new int[] { source.PortNumber, target.PortNumber });
        }

        e.Handled = true;
    }
}
