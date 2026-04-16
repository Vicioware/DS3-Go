using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DS3Go.Models;
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

        InitializePorts();
        SubscribeToEvents();
    }

    private void InitializePorts()
    {
        foreach (var slot in _portManager.Ports)
        {
            Ports.Add(new PortViewModel(slot));
        }

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
        StatusText = "Monitoreando dispositivos USB...";
    }

    private void OnControllerConnected(ControllerDevice device)
    {
        _dispatcher.Invoke(() =>
        {
            _portManager.OnControllerConnected(device);
            StatusText = $"Conectado: {device.Name}";
        });
    }

    private void OnControllerDisconnected(string devicePath)
    {
        _dispatcher.Invoke(() =>
        {
            _portManager.OnControllerDisconnected(devicePath);
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

    private void OnInputUpdated(int xInputIndex, ControllerInput rawInput)
    {
        if (SelectedPort == null || !rawInput.IsConnected) return;
        if (xInputIndex != SelectedPort.PortNumber - 1) return;

        var remapped = _remappingEngine.ApplyRemapping(SelectedPort.PortNumber, rawInput);

        _dispatcher.BeginInvoke(() =>
        {
            InputTester.UpdateInput(remapped);
        });

        if (_virtualController.IsAvailable)
        {
            _virtualController.UpdateVirtualController(SelectedPort.PortNumber, remapped);
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
        }
    }

    [RelayCommand]
    private void MovePortUp(int portNumber)
    {
        if (portNumber <= 1) return;
        _portManager.SwapPorts(portNumber, portNumber - 1);
    }

    [RelayCommand]
    private void MovePortDown(int portNumber)
    {
        if (portNumber >= 4) return;
        _portManager.SwapPorts(portNumber, portNumber + 1);
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
