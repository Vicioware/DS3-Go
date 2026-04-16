using System.Text.Json;
using DS3Go.Models;
using DS3Go.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DS3Go.Services;

public sealed class KnownDeviceIdentifier : IDeviceIdentifier
{
    private readonly Dictionary<(string Vid, string Pid), DeviceSignature> _index = new();
    private readonly ILogger<KnownDeviceIdentifier> _logger;

    public KnownDeviceIdentifier(string jsonFilePath, ILogger<KnownDeviceIdentifier> logger)
    {
        _logger = logger;
        LoadDatabase(jsonFilePath);
    }

    private void LoadDatabase(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                _logger.LogWarning("Archivo de dispositivos no encontrado: {Path}", path);
                return;
            }

            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var devices = doc.RootElement.GetProperty("devices");

            foreach (var entry in devices.EnumerateArray())
            {
                var vid = entry.GetProperty("vid").GetString()?.ToUpperInvariant() ?? "";
                var pid = entry.GetProperty("pid").GetString()?.ToUpperInvariant() ?? "";
                var manufacturer = entry.TryGetProperty("manufacturer", out var m) ? m.GetString() ?? "" : "";
                var model = entry.TryGetProperty("model", out var md) ? md.GetString() ?? "" : "";
                var typeStr = entry.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";

                var type = typeStr switch
                {
                    "original" => ControllerType.Original,
                    "known_clone" => ControllerType.KnownClone,
                    "possible_clone" => ControllerType.PossibleClone,
                    _ => ControllerType.Unknown
                };

                var sig = new DeviceSignature(vid, pid, manufacturer, model, type);
                _index[(vid, pid)] = sig;
            }

            _logger.LogInformation("Base de datos cargada: {Count} dispositivo(s) conocido(s).", _index.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar base de datos de dispositivos.");
        }
    }

    public ControllerDevice? Identify(string vid, string pid, string devicePath, string name, string description)
    {
        if (!_index.TryGetValue((vid.ToUpperInvariant(), pid.ToUpperInvariant()), out var sig))
            return null;

        return new ControllerDevice
        {
            Vid = vid,
            Pid = pid,
            Name = $"{sig.Manufacturer} {sig.Model}",
            Type = sig.Type,
            DeviceInstancePath = devicePath,
            Signature = sig
        };
    }
}
