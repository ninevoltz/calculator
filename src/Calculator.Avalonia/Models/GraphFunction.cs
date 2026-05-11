using CommunityToolkit.Mvvm.ComponentModel;

namespace Calculator.Avalonia.Models;

public partial class GraphFunction : ObservableObject
{
    public GraphFunction(int index, string expression = "")
    {
        Index = index;
        _expression = expression;
    }

    public int Index { get; }

    public string Label => $"f{Index}";

    [ObservableProperty]
    private string _expression;
}
