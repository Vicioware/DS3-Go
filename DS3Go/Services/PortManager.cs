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

    private readonly HashSet<int> _claimedXInputIndices = new();

    public IReadOnlyList<PortSlot> Ports => _ports;
    public event Action<int>? PortStateChanged;

    public PortManager(IPersistenceService persistence, ILogger<PortManager> logger)
    {
        _persistence = persistence;
        _logger = logger;

        _ports = new PortSlot[MaxPorts];
        for (int i = 0; i < MaxPorts; i++)
            _ports[i] = new PortSlot { PortNumber = i + 1 };

        LoadState();
    }

    public void OnControllerConnected(ControllerDevice device)
    {
        lock (_lock)
        {
            int xIndex = FindNextFreeXInputIndex();

            if (xIndex < 0)
            {
                _logger.LogDebug("Dispositivo {Name} ignorado: sin slot XInput.", device.Name);
                return;
            }

            if (_claimedXInputIndices.Contains(xIndex))
            {
                _logger.LogDebug("XInput[{Index}] ya asignado, ignorando {Name}.", xIndex, device.Name);
                return;
            }

            // 1. Exact path match
            var existingPort = _ports.FirstOrDefault(
                p => p.AssignedDevicePath != null &&
                     p.AssignedDevicePath.Equals(device.DeviceInstancePath, StringComparison.OrdinalIgnoreCase));

            if (existingPort != null)
            {
                existingPort.State = PortState.Connected;
                existingPort.Controller = device;
                existingPort.XInputIndex = xIndex;
                _claimedXInputIndices.Add(xIndex);
                _logger.LogInformation("Reconocido en Puerto {Port} (XInput {XI}).",
                    existingPort.PortNumber, xIndex);
                PortStateChanged?.Invoke(existingPort.PortNumber);
                return;
            }

            // 2. VID/PID match on Assigned port
            var deviceVidPid = ExtractVidPid(device.DeviceInstancePath);
            if (!string.IsNullOrEmpty(deviceVidPid))
            {
                var assignedMatch = _ports.FirstOrDefault(
                    p => p.State == PortState.Assigned &&
                         p.AssignedDevicePath != null &&
                         ExtractVidPid(p.AssignedDevicePath)
                             .Equals(deviceVidPid, StringComparison.OrdinalIgnoreCase));

                if (assignedMatch != null)
                {
                    assignedMatch.State = PortState.Connected;
                    assignedMatch.AssignedDevicePath = device.DeviceInstancePath;
                    assignedMatch.Controller = device;
                    assignedMatch.XInputIndex = xIndex;
                    _claimedXInputIndices.Add(xIndex);
                    _logger.LogInformation("VID/PID reconocido en Puerto {Port} (XInput {XI}).",
                        assignedMatch.PortNumber, xIndex);
                    PortStateChanged?.Invoke(assignedMatch.PortNumber);
                    SaveState();
                    return;
                }
            }

            // 3. First empty port
            var emptyPort = _ports.FirstOrDefault(p => p.State == PortState.Empty);
            if (emptyPort != null)
            {
                emptyPort.State = PortState.Connected;
                emptyPort.AssignedDevicePath = device.DeviceInstancePath;
                emptyPort.Controller = device;
                emptyPort.XInputIndex = xIndex;
                _claimedXInputIndices.Add(xIndex);
                _logger.LogInformation("Asignado a Puerto {Port} (XInput {XI}).",
                    emptyPort.PortNumber, xIndex);
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
                             ExtractVidPid(p.AssignedDevicePath)
                                 .Equals(disconnVidPid, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (port != null && port.State == PortState.Connected)
            {
                _claimedXInputIndices.Remove(port.XInputIndex);
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

            _claimedXInputIndices.Remove(port.XInputIndex);
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
        try { _persistence.SavePortAssignments(_ports); }
        catch (Exception ex) { _logger.LogError(ex, "Error al guardar estado."); }
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
        catch (Exception ex) { _logger.LogError(ex, "Error al cargar estado."); }
    }

    /// <summary>
    /// Finds the next XInput index (0-3) that is connected but not claimed.
    /// </summary>
    private int FindNextFreeXInputIndex()
    {
        var state = new Interop.XInputNative.XINPUT_STATE();
        for (int i = 0; i < MaxPorts; i++)
        {
            if (_claimedXInputIndices.Contains(i))
                continue;
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
        return match.Success
            ? $"VID_{match.Groups[1].Value}&PID_{match.Groups[2].Value}".ToUpperInvariant()
            : "";
    }
}
