using DS3Go.Models;

namespace DS3Go.Services.Interfaces;

public interface IInputReader : IDisposable
{
    event Action<int, ControllerInput>? InputUpdated;
    void StartReading();
    void StopReading();
    ControllerInput? GetCurrentState(int xInputIndex);
    bool IsControllerConnected(int xInputIndex);
}
