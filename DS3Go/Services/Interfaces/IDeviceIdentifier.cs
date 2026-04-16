using DS3Go.Models;

namespace DS3Go.Services.Interfaces;

public interface IDeviceIdentifier
{
    ControllerDevice? Identify(string vid, string pid, string devicePath, string name, string description);
}
