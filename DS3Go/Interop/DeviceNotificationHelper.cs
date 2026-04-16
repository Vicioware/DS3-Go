using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DS3Go.Interop;

internal sealed class DeviceNotificationHelper : IDisposable
{
    private IntPtr _notificationHandle;
    private HwndSource? _hwndSource;

    public event Action<string>? DeviceArrived;
    public event Action<string>? DeviceRemoved;

    public void RegisterForNotifications(IntPtr hwnd)
    {
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);

        var filter = new NativeMethods.DEV_BROADCAST_DEVICEINTERFACE
        {
            dbcc_devicetype = NativeMethods.DBT_DEVTYP_DEVICEINTERFACE,
            dbcc_classguid = NativeMethods.GUID_DEVINTERFACE_USB_DEVICE,
            dbcc_name = string.Empty
        };
        filter.dbcc_size = Marshal.SizeOf(filter);

        _notificationHandle = NativeMethods.RegisterDeviceNotificationW(
            hwnd,
            ref filter,
            NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_DEVICECHANGE || lParam == IntPtr.Zero)
            return IntPtr.Zero;

        int eventType = wParam.ToInt32();
        var header = Marshal.PtrToStructure<NativeMethods.DEV_BROADCAST_DEVICEINTERFACE>(lParam);

        if (header.dbcc_devicetype != NativeMethods.DBT_DEVTYP_DEVICEINTERFACE)
            return IntPtr.Zero;

        string devicePath = header.dbcc_name ?? string.Empty;

        if (eventType == NativeMethods.DBT_DEVICEARRIVAL)
        {
            DeviceArrived?.Invoke(devicePath);
        }
        else if (eventType == NativeMethods.DBT_DEVICEREMOVECOMPLETE)
        {
            DeviceRemoved?.Invoke(devicePath);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _hwndSource?.RemoveHook(WndProc);
        if (_notificationHandle != IntPtr.Zero)
        {
            NativeMethods.UnregisterDeviceNotification(_notificationHandle);
            _notificationHandle = IntPtr.Zero;
        }
    }
}
