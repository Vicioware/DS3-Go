using DS3Go.Models;

namespace DS3Go.Services.Interfaces;

public interface IDeviceDetector : IDisposable
{
    event Action<ControllerDevice>? ControllerConnected;
    event Action<string>? ControllerDisconnected;
    void Initialize(IntPtr windowHandle);
}
