using CommunityToolkit.Mvvm.ComponentModel;
using DS3Go.Models;

namespace DS3Go.ViewModels;

public partial class PortViewModel : ObservableObject
{
    [ObservableProperty]
    private int _portNumber;

    [ObservableProperty]
    private PortState _state = PortState.Empty;

    [ObservableProperty]
    private string _deviceName = "";

    [ObservableProperty]
    private string _deviceType = "";

    [ObservableProperty]
    private bool _isSelected;

    public string StateDisplayName => State switch
    {
        PortState.Empty => "Vacío",
        PortState.Assigned => "Asignado",
        PortState.Connected => "Conectado",
        _ => ""
    };

    public bool HasDevice => State != PortState.Empty;

    public PortViewModel(PortSlot slot)
    {
        PortNumber = slot.PortNumber;
        UpdateFromSlot(slot);
    }

    public void UpdateFromSlot(PortSlot slot)
    {
        State = slot.State;
        DeviceName = slot.Controller?.Name
                     ?? (slot.AssignedDevicePath != null ? "Mando recordado" : "");
        DeviceType = slot.Controller?.Type.DisplayName() ?? "";
        OnPropertyChanged(nameof(StateDisplayName));
        OnPropertyChanged(nameof(HasDevice));
    }
}
