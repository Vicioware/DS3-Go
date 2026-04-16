using System.Runtime.InteropServices;

namespace DS3Go.Interop;

internal static class NativeMethods
{
    public const int WM_DEVICECHANGE = 0x0219;
    public const int DBT_DEVICEARRIVAL = 0x8000;
    public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
    public const int DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;
    public const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    public static readonly Guid GUID_DEVINTERFACE_USB_DEVICE =
        new("A5DCBF10-6530-11D2-901F-00C04FB951ED");

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr RegisterDeviceNotificationW(
        IntPtr hRecipient,
        ref DEV_BROADCAST_DEVICEINTERFACE notificationFilter,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterDeviceNotification(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEV_BROADCAST_DEVICEINTERFACE
    {
        public int dbcc_size;
        public int dbcc_devicetype;
        public int dbcc_reserved;
        public Guid dbcc_classguid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string dbcc_name;
    }
}
