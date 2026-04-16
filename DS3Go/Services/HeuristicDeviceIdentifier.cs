using DS3Go.Models;
using DS3Go.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DS3Go.Services;

public sealed class HeuristicDeviceIdentifier : IDeviceIdentifier
{
    private static readonly string[] GamepadKeywords =
    {
        "gamepad", "controller", "joystick", "game pad", "joypad",
        "dualshock", "sixaxis", "playstation", "ps3"
    };

    private readonly ILogger<HeuristicDeviceIdentifier> _logger;

    public HeuristicDeviceIdentifier(ILogger<HeuristicDeviceIdentifier> logger)
    {
        _logger = logger;
    }

    public ControllerDevice? Identify(string vid, string pid, string devicePath, string name, string description)
    {
        var combined = $"{name} {description}".ToLowerInvariant();

        if (!GamepadKeywords.Any(kw => combined.Contains(kw)))
            return null;

        _logger.LogDebug("Heuristica positiva para VID:{Vid} PID:{Pid}", vid, pid);

        return new ControllerDevice
        {
            Vid = vid,
            Pid = pid,
            Name = string.IsNullOrWhiteSpace(name) ? "Gamepad desconocido" : name,
            Type = ControllerType.PossibleClone,
            DeviceInstancePath = devicePath
        };
    }
}
