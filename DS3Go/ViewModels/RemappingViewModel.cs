using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DS3Go.Models;
using DS3Go.Services.Interfaces;

namespace DS3Go.ViewModels;

public partial class RemappingViewModel : ObservableObject
{
    private readonly IRemappingEngine _engine;

    [ObservableProperty]
    private int _portNumber = 1;

    [ObservableProperty]
    private ButtonMappingEntry? _listeningEntry;

    [ObservableProperty]
    private bool _isListening;

    public ObservableCollection<ButtonMappingEntry> Entries { get; } = new();

    public RemappingViewModel(IRemappingEngine engine)
    {
        _engine = engine;
        RefreshMappings();
    }

    public void RefreshMappings()
    {
        Entries.Clear();
        var mapping = _engine.GetMapping(PortNumber);

        foreach (DS3Button btn in Enum.GetValues<DS3Button>())
        {
            var target = mapping.TryGetValue(btn, out var t) ? t : btn;
            var entry = new ButtonMappingEntry
            {
                PhysicalButton = btn,
                VirtualButton = target,
                IsIdentity = btn == target
            };
            Entries.Add(entry);
        }
    }

    /// <summary>
    /// Called from UI when user clicks a row to start listening for a button press.
    /// </summary>
    [RelayCommand]
    private void StartListening(ButtonMappingEntry entry)
    {
        // Cancel previous listening
        if (ListeningEntry != null)
            ListeningEntry.IsWaiting = false;

        ListeningEntry = entry;
        entry.IsWaiting = true;
        IsListening = true;
    }

    [RelayCommand]
    private void CancelListening()
    {
        if (ListeningEntry != null)
            ListeningEntry.IsWaiting = false;

        ListeningEntry = null;
        IsListening = false;
    }

    /// <summary>
    /// Called by MainViewModel when a button press is detected while listening.
    /// </summary>
    public void OnButtonPressedWhileListening(DS3Button pressedButton)
    {
        if (ListeningEntry == null) return;

        var physical = ListeningEntry.PhysicalButton;
        _engine.SetMapping(PortNumber, physical, pressedButton);

        ListeningEntry.VirtualButton = pressedButton;
        ListeningEntry.IsIdentity = physical == pressedButton;
        ListeningEntry.IsWaiting = false;

        ListeningEntry = null;
        IsListening = false;
    }

    [RelayCommand]
    private void ResetMappings()
    {
        _engine.ResetMapping(PortNumber);
        RefreshMappings();
    }

    [RelayCommand]
    private void ResetSingleMapping(ButtonMappingEntry entry)
    {
        _engine.SetMapping(PortNumber, entry.PhysicalButton, entry.PhysicalButton);
        entry.VirtualButton = entry.PhysicalButton;
        entry.IsIdentity = true;
    }
}

public partial class ButtonMappingEntry : ObservableObject
{
    public DS3Button PhysicalButton { get; set; }

    [ObservableProperty]
    private DS3Button _virtualButton;

    [ObservableProperty]
    private bool _isWaiting;

    [ObservableProperty]
    private bool _isIdentity = true;

    public string PhysicalButtonName => PhysicalButton.ToString();
    public string VirtualButtonName => VirtualButton.ToString();

    partial void OnVirtualButtonChanged(DS3Button value)
    {
        OnPropertyChanged(nameof(VirtualButtonName));
    }
}
