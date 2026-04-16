using DS3Go.Models;
using DS3Go.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DS3Go.Services;

public sealed class RemappingEngine : IRemappingEngine
{
    private readonly Dictionary<int, Dictionary<DS3Button, DS3Button>> _mappings = new();
    private readonly IPersistenceService _persistence;
    private readonly ILogger<RemappingEngine> _logger;

    public RemappingEngine(IPersistenceService persistence, ILogger<RemappingEngine> logger)
    {
        _persistence = persistence;
        _logger = logger;

        for (int i = 1; i <= 4; i++)
            _mappings[i] = CreateIdentityMapping();

        LoadMappings();
    }

    public ControllerInput ApplyRemapping(int portNumber, ControllerInput rawInput)
    {
        if (!_mappings.TryGetValue(portNumber, out var mapping))
            return rawInput;

        bool isIdentity = mapping.All(kvp => kvp.Key == kvp.Value);
        if (isIdentity) return rawInput;

        var remapped = new ControllerInput
        {
            IsConnected = rawInput.IsConnected,
            LeftStickX = rawInput.LeftStickX,
            LeftStickY = rawInput.LeftStickY,
            RightStickX = rawInput.RightStickX,
            RightStickY = rawInput.RightStickY,
            L2Pressure = rawInput.L2Pressure,
            R2Pressure = rawInput.R2Pressure
        };

        foreach (var (physical, mapped) in mapping)
        {
            if (rawInput.Buttons.TryGetValue(physical, out var pressed))
            {
                remapped.Buttons[mapped] = pressed;
            }
        }

        if (mapping.TryGetValue(DS3Button.L2, out var l2Target) && l2Target == DS3Button.R2)
        {
            remapped.R2Pressure = rawInput.L2Pressure;
            remapped.L2Pressure = rawInput.R2Pressure;
        }
        else if (mapping.TryGetValue(DS3Button.R2, out var r2Target) && r2Target == DS3Button.L2)
        {
            remapped.L2Pressure = rawInput.R2Pressure;
            remapped.R2Pressure = rawInput.L2Pressure;
        }

        return remapped;
    }

    public Dictionary<DS3Button, DS3Button> GetMapping(int portNumber)
    {
        return _mappings.TryGetValue(portNumber, out var mapping)
            ? new Dictionary<DS3Button, DS3Button>(mapping)
            : CreateIdentityMapping();
    }

    public void SetMapping(int portNumber, DS3Button physicalButton, DS3Button virtualButton)
    {
        if (!_mappings.ContainsKey(portNumber))
            _mappings[portNumber] = CreateIdentityMapping();

        _mappings[portNumber][physicalButton] = virtualButton;
        SaveMappings();
        _logger.LogInformation("Remapeo Puerto {Port}: {Physical} -> {Virtual}",
            portNumber, physicalButton, virtualButton);
    }

    public void ResetMapping(int portNumber)
    {
        _mappings[portNumber] = CreateIdentityMapping();
        SaveMappings();
        _logger.LogInformation("Remapeo Puerto {Port} restaurado.", portNumber);
    }

    public void ResetAllMappings()
    {
        for (int i = 1; i <= 4; i++)
            _mappings[i] = CreateIdentityMapping();
        SaveMappings();
    }

    private static Dictionary<DS3Button, DS3Button> CreateIdentityMapping()
    {
        var mapping = new Dictionary<DS3Button, DS3Button>();
        foreach (DS3Button btn in Enum.GetValues<DS3Button>())
            mapping[btn] = btn;
        return mapping;
    }

    private void SaveMappings()
    {
        try { _persistence.SaveRemappings(_mappings); }
        catch (Exception ex) { _logger.LogError(ex, "Error al guardar remapeos."); }
    }

    private void LoadMappings()
    {
        try
        {
            var loaded = _persistence.LoadRemappings();
            foreach (var (port, mapping) in loaded)
                _mappings[port] = mapping;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error al cargar remapeos."); }
    }
}
