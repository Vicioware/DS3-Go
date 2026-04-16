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

    public ObservableCollection<ButtonMappingEntry> Entries { get; } = new();
    public IReadOnlyList<DS3Button> AvailableButtons { get; } = Enum.GetValues<DS3Button>().ToList();

    public RemappingViewModel(IRemappingEngine engine)
    {
        _engine = engine;
        RefreshMappings();
    }

    public void RefreshMappings()
    {
        foreach (var entry in Entries)
            entry.MappingChanged -= OnMappingChanged;

        Entries.Clear();
        var mapping = _engine.GetMapping(PortNumber);

        foreach (DS3Button btn in Enum.GetValues<DS3Button>())
        {
            var target = mapping.TryGetValue(btn, out var t) ? t : btn;
            var entry = new ButtonMappingEntry
            {
                PhysicalButton = btn,
                VirtualButton = target,
                AvailableButtons = AvailableButtons
            };
            entry.MappingChanged += OnMappingChanged;
            Entries.Add(entry);
        }
    }

    private void OnMappingChanged(DS3Button physical, DS3Button virtual_)
    {
        _engine.SetMapping(PortNumber, physical, virtual_);
    }

    [RelayCommand]
    private void ResetMappings()
    {
        _engine.ResetMapping(PortNumber);
        RefreshMappings();
    }
}

public partial class ButtonMappingEntry : ObservableObject
{
    public DS3Button PhysicalButton { get; set; }

    [ObservableProperty]
    private DS3Button _virtualButton;

    public IReadOnlyList<DS3Button> AvailableButtons { get; set; } = Array.Empty<DS3Button>();

    public event Action<DS3Button, DS3Button>? MappingChanged;

    partial void OnVirtualButtonChanged(DS3Button value)
    {
        MappingChanged?.Invoke(PhysicalButton, value);
    }

    public string PhysicalButtonName => PhysicalButton.ToString();
}
