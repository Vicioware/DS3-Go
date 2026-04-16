using DS3Go.Models;

namespace DS3Go.Services.Interfaces;

public interface IRemappingEngine
{
    ControllerInput ApplyRemapping(int portNumber, ControllerInput rawInput);
    Dictionary<DS3Button, DS3Button> GetMapping(int portNumber);
    void SetMapping(int portNumber, DS3Button physicalButton, DS3Button virtualButton);
    void ResetMapping(int portNumber);
    void ResetAllMappings();
}
