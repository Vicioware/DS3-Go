using DS3Go.Models;

namespace DS3Go.Services.Interfaces;

public interface IPortManager
{
    IReadOnlyList<PortSlot> Ports { get; }
    event Action<int>? PortStateChanged;
    void OnControllerConnected(ControllerDevice device);
    void OnControllerDisconnected(string devicePath);
    void ForgetDevice(int portNumber);
    void SwapPorts(int portA, int portB);
    void SaveState();
    void LoadState();
}
