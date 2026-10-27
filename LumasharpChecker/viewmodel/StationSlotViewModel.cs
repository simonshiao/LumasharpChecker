using CommunityToolkit.Mvvm.ComponentModel;

namespace LumasharpChecker.viewmodel;

public sealed class StationSlotViewModel : ObservableObject
{
    public int Number { get; }
    public string ButtonText => $"{Number:00} {(IsConfigured ? "✓" : "+")}";

    private bool _isConfigured;
    public bool IsConfigured
    {
        get => _isConfigured;
        set
        {
            if (SetProperty(ref _isConfigured, value)) OnPropertyChanged(nameof(ButtonText));
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public StationSlotViewModel(int number, bool isConfigured, bool isSelected)
    {
        Number = number;
        _isConfigured = isConfigured;
        _isSelected = isSelected;
    }
}
