namespace DS3Go.Models;

public sealed class ControllerInput
{
    public Dictionary<DS3Button, bool> Buttons { get; } = new();
    public float LeftStickX { get; set; }
    public float LeftStickY { get; set; }
    public float RightStickX { get; set; }
    public float RightStickY { get; set; }
    public float L2Pressure { get; set; }
    public float R2Pressure { get; set; }
    public bool IsConnected { get; set; }

    public ControllerInput()
    {
        foreach (DS3Button btn in Enum.GetValues<DS3Button>())
        {
            Buttons[btn] = false;
        }
    }

    public bool IsButtonPressed(DS3Button button) =>
        Buttons.TryGetValue(button, out var pressed) && pressed;
}
