using DS3Go.Interop;
using DS3Go.Models;
using DS3Go.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DS3Go.Services;

public sealed class XInputReader : IInputReader
{
    private const int PollIntervalMs = 8;
    private const float ThumbstickDeadzone = 0.05f;

    private readonly ILogger<XInputReader> _logger;
    private readonly ControllerInput[] _states = new ControllerInput[4];
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    public event Action<int, ControllerInput>? InputUpdated;

    public XInputReader(ILogger<XInputReader> logger)
    {
        _logger = logger;
        for (int i = 0; i < 4; i++)
            _states[i] = new ControllerInput();
    }

    public void StartReading()
    {
        if (_cts != null) return;

        _cts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoop(_cts.Token));
        _logger.LogInformation("Lectura XInput iniciada a ~{Hz}Hz.", 1000 / PollIntervalMs);
    }

    public void StopReading()
    {
        _cts?.Cancel();
        try { _pollTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts?.Dispose();
        _cts = null;
        _pollTask = null;
        _logger.LogInformation("Lectura XInput detenida.");
    }

    public ControllerInput? GetCurrentState(int xInputIndex)
    {
        if (xInputIndex < 0 || xInputIndex >= 4) return null;
        return _states[xInputIndex];
    }

    public bool IsControllerConnected(int xInputIndex)
    {
        if (xInputIndex < 0 || xInputIndex >= 4) return false;
        var state = new XInputNative.XINPUT_STATE();
        return XInputNative.XInputGetState((uint)xInputIndex, ref state) == XInputNative.ERROR_SUCCESS;
    }

    private void PollLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            for (int i = 0; i < 4; i++)
            {
                var xinputState = new XInputNative.XINPUT_STATE();
                uint result = XInputNative.XInputGetState((uint)i, ref xinputState);

                var input = _states[i];
                input.IsConnected = result == XInputNative.ERROR_SUCCESS;

                if (input.IsConnected)
                {
                    ParseGamepadState(ref xinputState.Gamepad, input);
                }

                InputUpdated?.Invoke(i, input);
            }

            Thread.Sleep(PollIntervalMs);
        }
    }

    private static void ParseGamepadState(ref XInputNative.XINPUT_GAMEPAD gp, ControllerInput input)
    {
        var buttons = gp.wButtons;

        input.Buttons[DS3Button.Cross] = buttons.HasFlag(XInputNative.GamepadButtons.A);
        input.Buttons[DS3Button.Circle] = buttons.HasFlag(XInputNative.GamepadButtons.B);
        input.Buttons[DS3Button.Square] = buttons.HasFlag(XInputNative.GamepadButtons.X);
        input.Buttons[DS3Button.Triangle] = buttons.HasFlag(XInputNative.GamepadButtons.Y);
        input.Buttons[DS3Button.L1] = buttons.HasFlag(XInputNative.GamepadButtons.LeftShoulder);
        input.Buttons[DS3Button.R1] = buttons.HasFlag(XInputNative.GamepadButtons.RightShoulder);
        input.Buttons[DS3Button.Select] = buttons.HasFlag(XInputNative.GamepadButtons.Back);
        input.Buttons[DS3Button.Start] = buttons.HasFlag(XInputNative.GamepadButtons.Start);
        input.Buttons[DS3Button.PS] = buttons.HasFlag(XInputNative.GamepadButtons.Guide);
        input.Buttons[DS3Button.L3] = buttons.HasFlag(XInputNative.GamepadButtons.LeftThumb);
        input.Buttons[DS3Button.R3] = buttons.HasFlag(XInputNative.GamepadButtons.RightThumb);
        input.Buttons[DS3Button.DPadUp] = buttons.HasFlag(XInputNative.GamepadButtons.DPadUp);
        input.Buttons[DS3Button.DPadDown] = buttons.HasFlag(XInputNative.GamepadButtons.DPadDown);
        input.Buttons[DS3Button.DPadLeft] = buttons.HasFlag(XInputNative.GamepadButtons.DPadLeft);
        input.Buttons[DS3Button.DPadRight] = buttons.HasFlag(XInputNative.GamepadButtons.DPadRight);

        input.Buttons[DS3Button.L2] = gp.bLeftTrigger > 30;
        input.Buttons[DS3Button.R2] = gp.bRightTrigger > 30;

        input.L2Pressure = gp.bLeftTrigger / 255f;
        input.R2Pressure = gp.bRightTrigger / 255f;

        input.LeftStickX = ApplyDeadzone(gp.sThumbLX / 32767f);
        input.LeftStickY = ApplyDeadzone(gp.sThumbLY / 32767f);
        input.RightStickX = ApplyDeadzone(gp.sThumbRX / 32767f);
        input.RightStickY = ApplyDeadzone(gp.sThumbRY / 32767f);
    }

    private static float ApplyDeadzone(float value)
    {
        if (Math.Abs(value) < ThumbstickDeadzone)
            return 0f;
        return Math.Clamp(value, -1f, 1f);
    }

    public void Dispose()
    {
        StopReading();
    }
}
