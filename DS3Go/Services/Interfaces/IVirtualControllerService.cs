using DS3Go.Models;

namespace DS3Go.Services.Interfaces;

public interface IVirtualControllerService : IDisposable
{
    bool IsAvailable { get; }
    void CreateVirtualController(int portNumber);
    void RemoveVirtualController(int portNumber);
    void UpdateVirtualController(int portNumber, ControllerInput input);

    /// <summary>
    /// Disconnects ALL virtual controllers and recreates them in the given port order.
    /// The first port in the list gets the lowest XInput index, etc.
    /// This is how port reassignment affects the actual XInput player order.
    /// </summary>
    void RebuildVirtualControllers(int[] connectedPortNumbers);
}
