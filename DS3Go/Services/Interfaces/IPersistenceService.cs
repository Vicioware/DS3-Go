using DS3Go.Models;

namespace DS3Go.Services.Interfaces;

public interface IPersistenceService
{
    void SavePortAssignments(IReadOnlyList<PortSlot> ports);
    List<PortAssignmentData> LoadPortAssignments();
    void SaveRemappings(Dictionary<int, Dictionary<DS3Button, DS3Button>> mappings);
    Dictionary<int, Dictionary<DS3Button, DS3Button>> LoadRemappings();
}

public sealed class PortAssignmentData
{
    public int PortNumber { get; set; }
    public string? DevicePath { get; set; }
}
