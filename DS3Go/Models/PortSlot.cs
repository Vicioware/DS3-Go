namespace DS3Go.Models;

public sealed class PortSlot
{
    public int PortNumber { get; init; }
    public PortState State { get; set; } = PortState.Empty;
    public string? AssignedDevicePath { get; set; }
    public ControllerDevice? Controller { get; set; }
    public int XInputIndex { get; set; } = -1;
}
