using DS3Go.Models;

namespace DS3Go.Services.Interfaces;

public interface IVirtualControllerService : IDisposable
{
    bool IsAvailable { get; }
    void CreateVirtualController(int portNumber);
    void RemoveVirtualController(int portNumber);
    void UpdateVirtualController(int portNumber, ControllerInput input);
}
