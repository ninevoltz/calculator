using CommunityToolkit.Mvvm.ComponentModel;

namespace Calculator.Avalonia.Models;

public partial class GraphVariable : ObservableObject
{
    public GraphVariable(string name, double value = 1)
    {
        Name = name;
        _value = value;
    }

    public string Name { get; }

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private double _minimum = -10;

    [ObservableProperty]
    private double _maximum = 10;

    [ObservableProperty]
    private double _step = 1;

    [ObservableProperty]
    private bool _isExpanded;
}
