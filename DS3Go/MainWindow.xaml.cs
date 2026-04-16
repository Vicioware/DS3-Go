using System.Windows;
using System.Windows.Interop;
using DS3Go.ViewModels;

namespace DS3Go;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (DataContext is MainViewModel vm)
        {
            vm.InitializeHardwareMonitoring(hwnd);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Dispose();
        }
        base.OnClosed(e);
    }
}
