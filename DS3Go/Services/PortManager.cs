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

    // Track VID:PID pairs that have already been assigned in this session
    // to prevent double-assignment from multiple USB interfaces.
    private readonly HashSet<string> _assignedVidPids = new(StringComparer.OrdinalIgnoreCase);

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
            var vidPidKey = $"{device.Vid}:{device.Pid}".ToUpperInvariant();

            // 1. Exact path match — reconnect to same port
            var existingPort = _ports.FirstOrDefault(
                p => p.AssignedDevicePath != null &&
                     p.AssignedDevicePath.Equals(device.DeviceInstancePath, StringComparison.OrdinalIgnoreCase));

            if (existingPort != null)
            {
                existingPort.State = PortState.Connected;
                existingPort.Controller = device;
                existingPort.XInputIndex = FindXInputIndex();
                _assignedVidPids.Add(vidPidKey);
                _logger.LogInformation("Mando reconocido en Puerto {Port}.", existingPort.PortNumber);
                PortStateChanged?.Invoke(existingPort.PortNumber);
                return;
            }

            // 2. Dedup: same VID/PID already connected or being processed
            if (_assignedVidPids.Contains(vidPidKey))
            {
                var connPort = _ports.FirstOrDefault(
                    p => p.State == PortState.Connected &&
                         p.Controller != null &&
                         $"{p.Controller.Vid}:{p.Controller.Pid}".Equals(vidPidKey, StringComparison.OrdinalIgnoreCase));

                if (connPort != null)
                {
                    _logger.LogDebug("Duplicado ignorado: {VidPid} ya está en Puerto {Port}.",
                        vidPidKey, connPort.PortNumber);
                    return;
                }
            }

            // 3. VID/PID match on an Assigned (remembered) port
            var deviceVidPid = ExtractVidPid(device.DeviceInstancePath);
            if (!string.IsNullOrEmpty(deviceVidPid))
            {
                var assignedMatch = _ports.FirstOrDefault(
                    p => p.State == PortState.Assigned &&
                         p.AssignedDevicePath != null &&
                         ExtractVidPid(p.AssignedDevicePath).Equals(deviceVidPid, StringComparison.OrdinalIgnoreCase));

                if (assignedMatch != null)
                {
                    assignedMatch.State = PortState.Connected;
                    assignedMatch.AssignedDevicePath = device.DeviceInstancePath;
                    assignedMatch.Controller = device;
                    assignedMatch.XInputIndex = FindXInputIndex();
                    _assignedVidPids.Add(vidPidKey);
                    _logger.LogInformation("Mando VID/PID reconocido en Puerto {Port}.", assignedMatch.PortNumber);
                    PortStateChanged?.Invoke(assignedMatch.PortNumber);
                    SaveState();
                    return;
                }
            }

            // 4. Assign to first empty port
            var emptyPort = _ports.FirstOrDefault(p => p.State == PortState.Empty);
            if (emptyPort != null)
            {
                emptyPort.State = PortState.Connected;
                emptyPort.AssignedDevicePath = device.DeviceInstancePath;
                emptyPort.Controller = device;
                emptyPort.XInputIndex = FindXInputIndex();
                _assignedVidPids.Add(vidPidKey);
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
                     (devicePath.Contains(p.AssignedDevicePath, StringComparison.OrdinalIgnoreCase) ||
                      p.AssignedDevicePath.Contains(devicePath, StringComparison.OrdinalIgnoreCase)));

            if (port == null)
            {
                var disconnVidPid = ExtractVidPid(devicePath);
                if (!string.IsNullOrEmpty(disconnVidPid))
                {
                    port = _ports.FirstOrDefault(
                        p => p.AssignedDevicePath != null &&
                             ExtractVidPid(p.AssignedDevicePath).Equals(disconnVidPid, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (port != null && port.State == PortState.Connected)
            {
                // Remove from dedup set
                if (port.Controller != null)
                    _assignedVidPids.Remove($"{port.Controller.Vid}:{port.Controller.Pid}".ToUpperInvariant());

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
            if (port == null || port.State == PortState.Empty) return;

            if (port.Controller != null)
                _assignedVidPids.Remove($"{port.Controller.Vid}:{port.Controller.Pid}".ToUpperInvariant());

            port.State = PortState.Empty;
            port.AssignedDevicePath = null;
            port.Controller = null;
            port.XInputIndex = -1;

            _logger.LogInformation("Puerto {Port} olvidado.", portNumber);
            PortStateChanged?.Invoke(portNumber);
            SaveState();
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

    /// <summary>
    /// Returns the first XInput index that reports a connected gamepad.
    /// </summary>
    private static int FindXInputIndex()
    {
        var state = new Interop.XInputNative.XINPUT_STATE();
        for (int i = 0; i < MaxPorts; i++)
        {
            if (Interop.XInputNative.XInputGetState((uint)i, ref state) == Interop.XInputNative.ERROR_SUCCESS)
                return i;
        }
        return 0; // Default to 0 instead of -1
    }

    private static string ExtractVidPid(string path)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            path, @"VID_([0-9A-Fa-f]{4}).*?PID_([0-9A-Fa-f]{4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? $"VID_{match.Groups[1].Value}&PID_{match.Groups[2].Value}".ToUpperInvariant() : "";
    }
}
