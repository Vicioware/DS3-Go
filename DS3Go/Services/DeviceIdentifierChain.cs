using DS3Go.Models;
using DS3Go.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DS3Go.Services;

public sealed class DeviceIdentifierChain : IDeviceIdentifier
{
    private readonly IDeviceIdentifier[] _identifiers;
    private readonly ILogger<DeviceIdentifierChain> _logger;

    public DeviceIdentifierChain(IDeviceIdentifier[] identifiers, ILogger<DeviceIdentifierChain> logger)
    {
        _identifiers = identifiers;
        _logger = logger;
    }

    public ControllerDevice? Identify(string vid, string pid, string devicePath, string name, string description)
    {
        foreach (var identifier in _identifiers)
        {
            try
            {
                var result = identifier.Identify(vid, pid, devicePath, name, description);
                if (result != null)
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en identificador {Type}.", identifier.GetType().Name);
            }
        }
        return null;
    }
}
