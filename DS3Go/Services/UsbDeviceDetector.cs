using System.Management;
using System.Text.RegularExpressions;
using DS3Go.Interop;
using DS3Go.Models;
using DS3Go.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DS3Go.Services;

public sealed class UsbDeviceDetector : IDeviceDetector
{
    private static readonly Regex VidPidRegex = new(
        @"VID_([0-9A-Fa-f]{4}).*?PID_([0-9A-Fa-f]{4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IDeviceIdentifier _identifier;
    private readonly ILogger<UsbDeviceDetector> _logger;
    private DeviceNotificationHelper? _notificationHelper;

    public event Action<ControllerDevice>? ControllerConnected;
    public event Action<string>? ControllerDisconnected;

    public UsbDeviceDetector(IDeviceIdentifier identifier, ILogger<UsbDeviceDetector> logger)
    {
        _identifier = identifier;
        _logger = logger;
    }

    public void Initialize(IntPtr windowHandle)
    {
        _notificationHelper = new DeviceNotificationHelper();
        _notificationHelper.DeviceArrived += OnDeviceArrived;
        _notificationHelper.DeviceRemoved += OnDeviceRemoved;
        _notificationHelper.RegisterForNotifications(windowHandle);

        _logger.LogInformation("Monitoreo USB iniciado.");
        ScanExistingDevices();
    }

    private void ScanExistingDevices()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE 'USB\\VID_%'");

            int count = 0;
            foreach (var obj in searcher.Get())
            {
                var deviceId = obj["DeviceID"]?.ToString() ?? "";
                var name = obj["Name"]?.ToString() ?? "Desconocido";
                var description = obj["Description"]?.ToString() ?? "";

                if (ProcessDevicePath(deviceId, name, description))
                    count++;
            }

            _logger.LogInformation("Escaneo inicial: {Count} mando(s) detectado(s).", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en escaneo inicial de dispositivos.");
        }
    }

    private void OnDeviceArrived(string devicePath)
    {
        _logger.LogDebug("USB conectado: {Path}", devicePath);
        ProcessDevicePath(devicePath, "", "");
    }

    private void OnDeviceRemoved(string devicePath)
    {
        _logger.LogDebug("USB desconectado: {Path}", devicePath);
        ControllerDisconnected?.Invoke(devicePath);
    }

    private bool ProcessDevicePath(string devicePath, string name, string description)
    {
        var match = VidPidRegex.Match(devicePath);
        if (!match.Success) return false;

        var vid = match.Groups[1].Value.ToUpperInvariant();
        var pid = match.Groups[2].Value.ToUpperInvariant();

        var controller = _identifier.Identify(vid, pid, devicePath, name, description);
        if (controller == null) return false;

        _logger.LogInformation("Mando detectado: {Name} [{Vid}:{Pid}]",
            controller.Name, controller.Vid, controller.Pid);
        ControllerConnected?.Invoke(controller);
        return true;
    }

    public void Dispose()
    {
        _notificationHelper?.Dispose();
    }
}
