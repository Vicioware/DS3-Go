using DS3Go.Models;
using DS3Go.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DS3Go.Services;

public sealed class PortManager : IPortManager
{
    private const int MaxPorts = 4;
    private readonly PortSlot[] _ports;
    private readonly IPersistenceService _persistence;
    private readonly ILogger<PortManager> _logger;
    private readonly object _lock = new();

    public IReadOnlyList<PortSlot> Ports => _ports;
    public event Action<int>? PortStateChanged;

    public PortManager(IPersistenceService persistence, ILogger<PortManager> logger)
    {
        _persistence = persistence;
        _logger = logger;

        _ports = new PortSlot[MaxPorts];
        for (int i = 0; i < MaxPorts; i++)
        {
            _ports[i] = new PortSlot { PortNumber = i + 1 };
        }

        LoadState();
    }

    public void OnControllerConnected(ControllerDevice device)
    {
        lock (_lock)
        {
            var existingPort = _ports.FirstOrDefault(
                p => p.AssignedDevicePath != null &&
                     p.AssignedDevicePath.Equals(device.DeviceInstancePath, StringComparison.OrdinalIgnoreCase));

            if (existingPort != null)
            {
                existingPort.State = PortState.Connected;
                existingPort.Controller = device;
                _logger.LogInformation("Mando reconocido en Puerto {Port}.", existingPort.PortNumber);
                PortStateChanged?.Invoke(existingPort.PortNumber);
                return;
            }

            var emptyPort = _ports.FirstOrDefault(p => p.State == PortState.Empty);
            if (emptyPort != null)
            {
                emptyPort.State = PortState.Connected;
                emptyPort.AssignedDevicePath = device.DeviceInstancePath;
                emptyPort.Controller = device;
                emptyPort.XInputIndex = FindXInputIndex(device);
                _logger.LogInformation("Mando asignado a Puerto {Port}.", emptyPort.PortNumber);
                PortStateChanged?.Invoke(emptyPort.PortNumber);
                SaveState();
                return;
            }

            _logger.LogWarning("No hay puertos disponibles para: {Name}", device.Name);
        }
    }

    public void OnControllerDisconnected(string devicePath)
    {
        lock (_lock)
        {
            var port = _ports.FirstOrDefault(
                p => p.AssignedDevicePath != null &&
                     devicePath.Contains(p.AssignedDevicePath, StringComparison.OrdinalIgnoreCase));

            if (port == null)
            {
                port = _ports.FirstOrDefault(
                    p => p.AssignedDevicePath != null &&
                         p.AssignedDevicePath.Contains(
                             ExtractVidPid(devicePath),
                             StringComparison.OrdinalIgnoreCase));
            }

            if (port != null && port.State == PortState.Connected)
            {
                port.State = PortState.Assigned;
                port.Controller = null;
                _logger.LogInformation("Puerto {Port}: Desconectado -> Asignado.", port.PortNumber);
                PortStateChanged?.Invoke(port.PortNumber);
            }
        }
    }

    public void ForgetDevice(int portNumber)
    {
        lock (_lock)
        {
            var port = _ports.FirstOrDefault(p => p.PortNumber == portNumber);
            if (port == null) return;

            var wasConnected = port.State == PortState.Connected;
            var device = port.Controller;

            port.State = PortState.Empty;
            port.AssignedDevicePath = null;
            port.Controller = null;
            port.XInputIndex = -1;

            _logger.LogInformation("Puerto {Port} olvidado.", portNumber);
            PortStateChanged?.Invoke(portNumber);
            SaveState();

            if (wasConnected && device != null)
            {
                OnControllerConnected(device);
            }
        }
    }

    public void SwapPorts(int portA, int portB)
    {
        lock (_lock)
        {
            var a = _ports.FirstOrDefault(p => p.PortNumber == portA);
            var b = _ports.FirstOrDefault(p => p.PortNumber == portB);
            if (a == null || b == null) return;

            (a.State, b.State) = (b.State, a.State);
            (a.AssignedDevicePath, b.AssignedDevicePath) = (b.AssignedDevicePath, a.AssignedDevicePath);
            (a.Controller, b.Controller) = (b.Controller, a.Controller);
            (a.XInputIndex, b.XInputIndex) = (b.XInputIndex, a.XInputIndex);

            _logger.LogInformation("Puertos {A} y {B} intercambiados.", portA, portB);
            PortStateChanged?.Invoke(portA);
            PortStateChanged?.Invoke(portB);
            SaveState();
        }
    }

    public void SaveState()
    {
        try
        {
            _persistence.SavePortAssignments(_ports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar estado de puertos.");
        }
    }

    public void LoadState()
    {
        try
        {
            var assignments = _persistence.LoadPortAssignments();
            foreach (var assignment in assignments)
            {
                var port = _ports.FirstOrDefault(p => p.PortNumber == assignment.PortNumber);
                if (port != null && assignment.DevicePath != null)
                {
                    port.State = PortState.Assigned;
                    port.AssignedDevicePath = assignment.DevicePath;
                }
            }
            _logger.LogInformation("Estado de puertos cargado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar estado de puertos.");
        }
    }

    private static int FindXInputIndex(ControllerDevice device)
    {
        var state = new Interop.XInputNative.XINPUT_STATE();
        for (int i = 0; i < MaxPorts; i++)
        {
            if (Interop.XInputNative.XInputGetState((uint)i, ref state) == Interop.XInputNative.ERROR_SUCCESS)
                return i;
        }
        return -1;
    }

    private static string ExtractVidPid(string path)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            path, @"VID_([0-9A-Fa-f]{4}).*?PID_([0-9A-Fa-f]{4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? $"VID_{match.Groups[1].Value}&PID_{match.Groups[2].Value}" : "";
    }
}
