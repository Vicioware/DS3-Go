namespace DS3Go.Models;

public sealed record DeviceSignature(
    string Vid,
    string Pid,
    string Manufacturer,
    string Model,
    ControllerType Type
);
