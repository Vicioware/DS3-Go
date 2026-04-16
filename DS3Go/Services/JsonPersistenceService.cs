using System.Text.Json;
using System.Text.Json.Serialization;
using DS3Go.Models;
using DS3Go.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO;

namespace DS3Go.Services;

public sealed class JsonPersistenceService : IPersistenceService
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DS3Go");

    private static readonly string PortsFile = Path.Combine(DataDir, "port_assignments.json");
    private static readonly string RemappingsFile = Path.Combine(DataDir, "remapping_profiles.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger<JsonPersistenceService> _logger;

    public JsonPersistenceService(ILogger<JsonPersistenceService> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(DataDir);
    }

    public void SavePortAssignments(IReadOnlyList<PortSlot> ports)
    {
        var data = ports.Select(p => new PortAssignmentData
        {
            PortNumber = p.PortNumber,
            DevicePath = p.AssignedDevicePath
        }).ToList();

        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(PortsFile, json);
        _logger.LogDebug("Asignaciones de puertos guardadas.");
    }

    public List<PortAssignmentData> LoadPortAssignments()
    {
        if (!File.Exists(PortsFile))
            return new List<PortAssignmentData>();

        var json = File.ReadAllText(PortsFile);
        return JsonSerializer.Deserialize<List<PortAssignmentData>>(json, JsonOptions)
               ?? new List<PortAssignmentData>();
    }

    public void SaveRemappings(Dictionary<int, Dictionary<DS3Button, DS3Button>> mappings)
    {
        var serializable = mappings.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value.ToDictionary(
                b => b.Key.ToString(),
                b => b.Value.ToString()));

        var json = JsonSerializer.Serialize(serializable, JsonOptions);
        File.WriteAllText(RemappingsFile, json);
        _logger.LogDebug("Perfiles de remapeo guardados.");
    }

    public Dictionary<int, Dictionary<DS3Button, DS3Button>> LoadRemappings()
    {
        if (!File.Exists(RemappingsFile))
            return new Dictionary<int, Dictionary<DS3Button, DS3Button>>();

        var json = File.ReadAllText(RemappingsFile);
        var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, JsonOptions);

        if (raw == null)
            return new Dictionary<int, Dictionary<DS3Button, DS3Button>>();

        var result = new Dictionary<int, Dictionary<DS3Button, DS3Button>>();
        foreach (var (portStr, mapping) in raw)
        {
            if (!int.TryParse(portStr, out var port)) continue;

            var buttonMap = new Dictionary<DS3Button, DS3Button>();
            foreach (var (fromStr, toStr) in mapping)
            {
                if (Enum.TryParse<DS3Button>(fromStr, out var from) &&
                    Enum.TryParse<DS3Button>(toStr, out var to))
                {
                    buttonMap[from] = to;
                }
            }
            result[port] = buttonMap;
        }

        return result;
    }
}
