namespace DS3Go.Models;

public sealed class ControllerDevice
{
    public required string Vid { get; init; }
    public required string Pid { get; init; }
    public required string Name { get; init; }
    public required ControllerType Type { get; init; }
    public required string DeviceInstancePath { get; init; }
    public DeviceSignature? Signature { get; init; }
    public int XInputIndex { get; set; } = -1;

    public string DisplaySummary =>
        $"{Name} ({Type.DisplayName()}) [VID:{Vid} PID:{Pid}]";
}
