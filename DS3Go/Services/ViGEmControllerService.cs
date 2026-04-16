using DS3Go.Models;
using DS3Go.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace DS3Go.Services;

public sealed class ViGEmControllerService : IVirtualControllerService
{
    private readonly ILogger<ViGEmControllerService> _logger;
    private ViGEmClient? _client;
    private readonly Dictionary<int, IXbox360Controller> _controllers = new();

    public bool IsAvailable { get; private set; }

    public ViGEmControllerService(ILogger<ViGEmControllerService> logger)
    {
        _logger = logger;
        TryInitialize();
    }

    private void TryInitialize()
    {
        try
        {
            _client = new ViGEmClient();
            IsAvailable = true;
            _logger.LogInformation("ViGEmBus disponible. Remapeo a nivel de sistema habilitado.");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            _logger.LogWarning(
                "ViGEmBus no disponible: {Msg}. Instala ViGEmBus para remapeo a nivel de sistema.",
                ex.Message);
        }
    }

    public void CreateVirtualController(int portNumber)
    {
        if (!IsAvailable || _client == null) return;
        if (_controllers.ContainsKey(portNumber)) return;

        try
        {
            var controller = _client.CreateXbox360Controller();
            controller.Connect();
            _controllers[portNumber] = controller;
            _logger.LogInformation("Controlador virtual creado para Puerto {Port}.", portNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear controlador virtual Puerto {Port}.", portNumber);
        }
    }

    public void RemoveVirtualController(int portNumber)
    {
        if (!_controllers.TryGetValue(portNumber, out var controller)) return;

        try
        {
            controller.Disconnect();
            _controllers.Remove(portNumber);
            _logger.LogInformation("Controlador virtual eliminado del Puerto {Port}.", portNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar controlador virtual Puerto {Port}.", portNumber);
        }
    }

    public void UpdateVirtualController(int portNumber, ControllerInput input)
    {
        if (!_controllers.TryGetValue(portNumber, out var controller)) return;

        try
        {
            controller.SetButtonState(Xbox360Button.A, input.IsButtonPressed(DS3Button.Cross));
            controller.SetButtonState(Xbox360Button.B, input.IsButtonPressed(DS3Button.Circle));
            controller.SetButtonState(Xbox360Button.X, input.IsButtonPressed(DS3Button.Square));
            controller.SetButtonState(Xbox360Button.Y, input.IsButtonPressed(DS3Button.Triangle));

            controller.SetButtonState(Xbox360Button.LeftShoulder, input.IsButtonPressed(DS3Button.L1));
            controller.SetButtonState(Xbox360Button.RightShoulder, input.IsButtonPressed(DS3Button.R1));

            controller.SetButtonState(Xbox360Button.Back, input.IsButtonPressed(DS3Button.Select));
            controller.SetButtonState(Xbox360Button.Start, input.IsButtonPressed(DS3Button.Start));
            controller.SetButtonState(Xbox360Button.Guide, input.IsButtonPressed(DS3Button.PS));

            controller.SetButtonState(Xbox360Button.LeftThumb, input.IsButtonPressed(DS3Button.L3));
            controller.SetButtonState(Xbox360Button.RightThumb, input.IsButtonPressed(DS3Button.R3));

            controller.SetButtonState(Xbox360Button.Up, input.IsButtonPressed(DS3Button.DPadUp));
            controller.SetButtonState(Xbox360Button.Down, input.IsButtonPressed(DS3Button.DPadDown));
            controller.SetButtonState(Xbox360Button.Left, input.IsButtonPressed(DS3Button.DPadLeft));
            controller.SetButtonState(Xbox360Button.Right, input.IsButtonPressed(DS3Button.DPadRight));

            controller.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(input.L2Pressure * 255));
            controller.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(input.R2Pressure * 255));

            controller.SetAxisValue(Xbox360Axis.LeftThumbX, (short)(input.LeftStickX * 32767));
            controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)(input.LeftStickY * 32767));
            controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)(input.RightStickX * 32767));
            controller.SetAxisValue(Xbox360Axis.RightThumbY, (short)(input.RightStickY * 32767));

            controller.SubmitReport();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar controlador virtual Puerto {Port}.", portNumber);
        }
    }

    public void Dispose()
    {
        foreach (var (_, controller) in _controllers)
        {
            try { controller.Disconnect(); } catch { }
        }
        _controllers.Clear();
        _client?.Dispose();
    }
}
