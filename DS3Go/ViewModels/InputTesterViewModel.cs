using CommunityToolkit.Mvvm.ComponentModel;
using DS3Go.Models;

namespace DS3Go.ViewModels;

public partial class InputTesterViewModel : ObservableObject
{
    private const double StickRange = 58.0;

    [ObservableProperty] private bool _crossPressed;
    [ObservableProperty] private bool _circlePressed;
    [ObservableProperty] private bool _squarePressed;
    [ObservableProperty] private bool _trianglePressed;

    [ObservableProperty] private bool _l1Pressed;
    [ObservableProperty] private bool _r1Pressed;
    [ObservableProperty] private bool _l2Pressed;
    [ObservableProperty] private bool _r2Pressed;

    [ObservableProperty] private bool _selectPressed;
    [ObservableProperty] private bool _startPressed;
    [ObservableProperty] private bool _psPressed;
    [ObservableProperty] private bool _l3Pressed;
    [ObservableProperty] private bool _r3Pressed;

    [ObservableProperty] private bool _dpadUpPressed;
    [ObservableProperty] private bool _dpadDownPressed;
    [ObservableProperty] private bool _dpadLeftPressed;
    [ObservableProperty] private bool _dpadRightPressed;

    [ObservableProperty] private double _leftStickX;
    [ObservableProperty] private double _leftStickY;
    [ObservableProperty] private double _rightStickX;
    [ObservableProperty] private double _rightStickY;

    [ObservableProperty] private double _l2Value;
    [ObservableProperty] private double _r2Value;

    [ObservableProperty] private bool _isConnected;

    public double LeftStickOffsetX => LeftStickX * StickRange;
    public double LeftStickOffsetY => -LeftStickY * StickRange;
    public double RightStickOffsetX => RightStickX * StickRange;
    public double RightStickOffsetY => -RightStickY * StickRange;

    public double L2BarHeight => L2Value * 100.0;
    public double R2BarHeight => R2Value * 100.0;

    public string LeftStickCoords => $"({LeftStickX:+0.00;-0.00}, {LeftStickY:+0.00;-0.00})";
    public string RightStickCoords => $"({RightStickX:+0.00;-0.00}, {RightStickY:+0.00;-0.00})";
    public string L2Percent => $"{(int)(L2Value * 100)}%";
    public string R2Percent => $"{(int)(R2Value * 100)}%";

    partial void OnLeftStickXChanged(double value)
    {
        OnPropertyChanged(nameof(LeftStickOffsetX));
        OnPropertyChanged(nameof(LeftStickCoords));
    }

    partial void OnLeftStickYChanged(double value)
    {
        OnPropertyChanged(nameof(LeftStickOffsetY));
        OnPropertyChanged(nameof(LeftStickCoords));
    }

    partial void OnRightStickXChanged(double value)
    {
        OnPropertyChanged(nameof(RightStickOffsetX));
        OnPropertyChanged(nameof(RightStickCoords));
    }

    partial void OnRightStickYChanged(double value)
    {
        OnPropertyChanged(nameof(RightStickOffsetY));
        OnPropertyChanged(nameof(RightStickCoords));
    }

    partial void OnL2ValueChanged(double value)
    {
        OnPropertyChanged(nameof(L2BarHeight));
        OnPropertyChanged(nameof(L2Percent));
    }

    partial void OnR2ValueChanged(double value)
    {
        OnPropertyChanged(nameof(R2BarHeight));
        OnPropertyChanged(nameof(R2Percent));
    }

    public void UpdateInput(ControllerInput input)
    {
        IsConnected = input.IsConnected;

        CrossPressed = input.IsButtonPressed(DS3Button.Cross);
        CirclePressed = input.IsButtonPressed(DS3Button.Circle);
        SquarePressed = input.IsButtonPressed(DS3Button.Square);
        TrianglePressed = input.IsButtonPressed(DS3Button.Triangle);

        L1Pressed = input.IsButtonPressed(DS3Button.L1);
        R1Pressed = input.IsButtonPressed(DS3Button.R1);
        L2Pressed = input.IsButtonPressed(DS3Button.L2);
        R2Pressed = input.IsButtonPressed(DS3Button.R2);

        SelectPressed = input.IsButtonPressed(DS3Button.Select);
        StartPressed = input.IsButtonPressed(DS3Button.Start);
        PsPressed = input.IsButtonPressed(DS3Button.PS);
        L3Pressed = input.IsButtonPressed(DS3Button.L3);
        R3Pressed = input.IsButtonPressed(DS3Button.R3);

        DpadUpPressed = input.IsButtonPressed(DS3Button.DPadUp);
        DpadDownPressed = input.IsButtonPressed(DS3Button.DPadDown);
        DpadLeftPressed = input.IsButtonPressed(DS3Button.DPadLeft);
        DpadRightPressed = input.IsButtonPressed(DS3Button.DPadRight);

        LeftStickX = input.LeftStickX;
        LeftStickY = input.LeftStickY;
        RightStickX = input.RightStickX;
        RightStickY = input.RightStickY;

        L2Value = input.L2Pressure;
        R2Value = input.R2Pressure;
    }
}
