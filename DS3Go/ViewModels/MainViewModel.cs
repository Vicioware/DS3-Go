using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DS3Go.Models;
using DS3Go.Services;
using DS3Go.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DS3Go.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IPortManager _portManager;
    private readonly IInputReader _inputReader;
    private readonly IRemappingEngine _remappingEngine;
    private readonly IVirtualControllerService _virtualController;
    private readonly IDeviceDetector _deviceDetector;
    private readonly ILogger<MainViewModel> _logger;
    private readonly Dispatcher _dispatcher;

    private readonly Dictionary<DS3Button, bool> _prevButtonState = new();

    public ObservableCollection<PortViewModel> Ports { get; } = new();

    [ObservableProperty]
    private PortViewModel? _selectedPort;

    [ObservableProperty]
    private InputTesterViewModel _inputTester;

    [ObservableProperty]
    private RemappingViewModel _remapping;

    [ObservableProperty]
    private bool _isViGEmAvailable;

    [ObservableProperty]
    private string _statusText = "Iniciando...";

    public MainViewModel(
        IPortManager portManager,
        IInputReader inputReader,
        IRemappingEngine remappingEngine,
        IVirtualControllerService virtualController,
        IDeviceDetector deviceDetector,
        ILogger<MainViewModel> logger)
    {
        _portManager = portManager;
        _inputReader = inputReader;
        _remappingEngine = remappingEngine;
        _virtualController = virtualController;
        _deviceDetector = deviceDetector;
        _logger = logger;
        _dispatcher = Application.Current.Dispatcher;

        IsViGEmAvailable = virtualController.IsAvailable;

        _inputTester = new InputTesterViewModel();
        _remapping = new RemappingViewModel(remappingEngine);

        foreach (DS3Button btn in Enum.GetValues<DS3Button>())
            _prevButtonState[btn] = false;

        InitializePorts();
        SubscribeToEvents();
    }

    private void InitializePorts()
    {
        foreach (var slot in _portManager.Ports)
            Ports.Add(new PortViewModel(slot));

        if (Ports.Count > 0)
        {
            SelectedPort = Ports[0];
            Remapping.PortNumber = 1;
        }
    }

    partial void OnSelectedPortChanged(PortViewModel? value)
    {
        foreach (var port in Ports)
            port.IsSelected = port == value;

        if (value != null)
        {
            Remapping.PortNumber = value.PortNumber;
            Remapping.RefreshMappings();
        }
    }

    private void SubscribeToEvents()
    {
        _portManager.PortStateChanged += OnPortStateChanged;
        _deviceDetector.ControllerConnected += OnControllerConnected;
        _deviceDetector.ControllerDisconnected += OnControllerDisconnected;
        _inputReader.InputUpdated += OnInputUpdated;
    }

    public void InitializeHardwareMonitoring(IntPtr windowHandle)
    {
        _deviceDetector.Initialize(windowHandle);
        _inputReader.StartReading();
        StatusText = "Esperando mandos...";
    }

    private void OnControllerConnected(ControllerDevice device)
    {
        _dispatcher.Invoke(() =>
        {
            // If using HID reader, rescan so the new device is picked up
            if (_inputReader is HidInputReader hidReader)
            {
                hidReader.ScanDevices();
            }

            _portManager.OnControllerConnected(device);

            // Auto-create ViGEm virtual controller for this port
            if (_virtualController.IsAvailable)
            {
                RebuildAllVirtualControllers();
            }

            StatusText = $"Conectado: {device.Name}";
        });
    }

    private void OnControllerDisconnected(string devicePath)
    {
        _dispatcher.Invoke(() =>
        {
            _portManager.OnControllerDisconnected(devicePath);

            // Rebuild virtual controllers (removes the disconnected one)
            if (_virtualController.IsAvailable)
            {
                RebuildAllVirtualControllers();
            }

            StatusText = "Mando desconectado.";
        });
    }

    private void OnPortStateChanged(int portNumber)
    {
        _dispatcher.Invoke(() =>
        {
            var port = Ports.FirstOrDefault(p => p.PortNumber == portNumber);
            var slot = _portManager.Ports.FirstOrDefault(s => s.PortNumber == portNumber);
            if (port != null && slot != null)
            {
                port.UpdateFromSlot(slot);
            }
        });
    }

    /// <summary>
    /// Destroys all ViGEm virtual controllers and recreates them in port order.
    /// Port 1 gets the lowest XInput index, Port 2 gets the next, etc.
    /// This is how port assignment translates to actual player order in games.
    /// </summary>
    private void RebuildAllVirtualControllers()
    {
        var connectedPorts = _portManager.Ports
            .Where(s => s.State == PortState.Connected)
            .OrderBy(s => s.PortNumber)
            .Select(s => s.PortNumber)
            .ToArray();

        if (connectedPorts.Length == 0) return;

        _virtualController.RebuildVirtualControllers(connectedPorts);
        _logger.LogInformation("Virtual controllers reconstruidos: {Ports}",
            string.Join(", ", connectedPorts.Select(p => $"P{p}")));
    }

    private void OnInputUpdated(int xInputIndex, ControllerInput rawInput)
    {
        if (!rawInput.IsConnected) return;

        // Find which port owns this XInput index
        var ownerSlot = _portManager.Ports.FirstOrDefault(
            s => s.State == PortState.Connected && s.XInputIndex == xInputIndex);

        if (ownerSlot == null) return;

        int portNumber = ownerSlot.PortNumber;
        var remapped = _remappingEngine.ApplyRemapping(portNumber, rawInput);

        // Route to this port's virtual controller
        if (_virtualController.IsAvailable)
        {
            _virtualController.UpdateVirtualController(portNumber, remapped);
        }

        // UI updates only for selected port
        if (SelectedPort != null && portNumber == SelectedPort.PortNumber)
        {
            // Rising-edge detection for remapping capture
            if (Remapping.IsListening)
            {
                foreach (DS3Button btn in Enum.GetValues<DS3Button>())
                {
                    bool pressed = rawInput.IsButtonPressed(btn);
                    bool wasPrev = _prevButtonState.GetValueOrDefault(btn, false);

                    if (pressed && !wasPrev)
                    {
                        var capturedBtn = btn;
                        _dispatcher.BeginInvoke(() =>
                        {
                            Remapping.OnButtonPressedWhileListening(capturedBtn);
                        });
                        break;
                    }
                }
            }

            foreach (DS3Button btn in Enum.GetValues<DS3Button>())
                _prevButtonState[btn] = rawInput.IsButtonPressed(btn);

            _dispatcher.BeginInvoke(() =>
            {
                InputTester.UpdateInput(remapped);
            });
        }
    }

    [RelayCommand]
    private void SelectPort(PortViewModel port)
    {
        SelectedPort = port;
    }

    [RelayCommand]
    private void ForgetDevice(int portNumber)
    {
        _portManager.ForgetDevice(portNumber);
        _virtualController.RemoveVirtualController(portNumber);
    }

    [RelayCommand]
    private void ToggleVirtualController(int portNumber)
    {
        if (!_virtualController.IsAvailable) return;
        _virtualController.CreateVirtualController(portNumber);
    }

    [RelayCommand]
    private void SwapPorts(object? parameter)
    {
        if (parameter is int[] ports && ports.Length == 2)
        {
            _portManager.SwapPorts(ports[0], ports[1]);

            // Rebuild virtual controllers in new port order
            // so that XInput indices match the new port assignments
            if (_virtualController.IsAvailable)
            {
                RebuildAllVirtualControllers();
            }
        }
    }

    [RelayCommand]
    private void MovePortUp(int portNumber)
    {
        if (portNumber <= 1) return;
        _portManager.SwapPorts(portNumber, portNumber - 1);

        if (_virtualController.IsAvailable)
            RebuildAllVirtualControllers();
    }

    [RelayCommand]
    private void MovePortDown(int portNumber)
    {
        if (portNumber >= 4) return;
        _portManager.SwapPorts(portNumber, portNumber + 1);

        if (_virtualController.IsAvailable)
            RebuildAllVirtualControllers();
    }

    public void Dispose()
    {
        _portManager.PortStateChanged -= OnPortStateChanged;
        _inputReader.InputUpdated -= OnInputUpdated;
        _inputReader.Dispose();
        _deviceDetector.Dispose();
        _virtualController.Dispose();
    }
}
