using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Calculator.Avalonia.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Calculator.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly CalculatorEngine _engine = new();
    private readonly NativeCalculatorEngine _nativeEngine = new();
    private readonly ProgrammerEngine _programmerEngine = new();
    private const int MaximumHistoryItems = 50;

    public MainWindowViewModel()
    {
        AddGraphFunction("x*sin(x)");
        AddGraphFunction();
        ActiveGraphFunction = GraphFunctions.First();
        PlotGraph();
    }

    [ObservableProperty]
    private string _display = "0";

    [ObservableProperty]
    private string _expression = string.Empty;

    [ObservableProperty]
    private string _mode = "Standard";

    [ObservableProperty]
    private bool _isScientific;

    [ObservableProperty]
    private bool _isStandard = true;

    [ObservableProperty]
    private bool _isProgrammer;

    [ObservableProperty]
    private bool _isGraphing;

    [ObservableProperty]
    private bool _isCalculatorMode = true;

    [ObservableProperty]
    private string _programmerHex = "0";

    [ObservableProperty]
    private string _programmerDec = "0";

    [ObservableProperty]
    private string _programmerOct = "0";

    [ObservableProperty]
    private string _programmerBin = "0";

    [ObservableProperty]
    private string _plottedExpressions = "x*sin(x)";

    [ObservableProperty]
    private double _graphZoom = 38;

    [ObservableProperty]
    private double _graphParameter = 2.7;

    [ObservableProperty]
    private ObservableCollection<GraphFunction> _graphFunctions = new();

    [ObservableProperty]
    private GraphFunction? _activeGraphFunction;

    [ObservableProperty]
    private ObservableCollection<GraphVariable> _graphVariables = new();

    [ObservableProperty]
    private string _graphVariablesText = string.Empty;

    [ObservableProperty]
    private bool _isGraphOptionsOpen;

    [ObservableProperty]
    private double _graphXMin = -10;

    [ObservableProperty]
    private double _graphXMax = 10;

    [ObservableProperty]
    private double _graphYMin = -10;

    [ObservableProperty]
    private double _graphYMax = 10;

    [ObservableProperty]
    private string _angleUnit = "Radians";

    [ObservableProperty]
    private double _graphLineThickness = 2.4;

    public ObservableCollection<CalculationHistoryItem> HistoryItems { get; } = new();

    public bool HasHistory => HistoryItems.Count > 0;

    [RelayCommand]
    private void Press(string key)
    {
        if (IsProgrammer)
        {
            var programmerState = _programmerEngine.Press(key);
            ApplyProgrammerState(programmerState);
            AddHistoryIfCompleted(key, programmerState.Expression, programmerState.Display);
            return;
        }

        CalculatorState state;
        if (_nativeEngine.IsAvailable && _nativeEngine.TryPress(key, out var nativeState))
        {
            state = nativeState;
        }
        else
        {
            state = _engine.Press(key);
        }

        Display = state.Display;
        Expression = state.Expression;
        AddHistoryIfCompleted(key, state.Expression, state.Display);
    }

    [RelayCommand]
    private void SetMode(string mode)
    {
        Mode = mode;
        IsScientific = mode == "Scientific";
        IsProgrammer = mode == "Programmer";
        IsGraphing = mode == "Graphing";
        IsStandard = mode == "Standard";
        IsCalculatorMode = !IsGraphing;

        if (IsProgrammer)
        {
            ApplyProgrammerState(_programmerEngine.Reset());
        }
        else if (IsGraphing)
        {
            PlotGraph();
            GraphZoom = 38;
            Expression = "Graphing";
        }
        else
        {
            var useScientific = mode == "Scientific";
            _nativeEngine.SetScientificMode(useScientific);
            var state = _nativeEngine.IsAvailable ? _nativeEngine.Reset() : _engine.Reset();
            Display = state.Display;
            Expression = state.Expression;
        }
    }

    [RelayCommand]
    private void PlotGraph()
    {
        SyncGraphVariables();
        PlottedExpressions = string.Join(
            "\n",
            GraphFunctions
                .Select(function => function.Expression)
                .Where(expression => !string.IsNullOrWhiteSpace(expression)));
        Display = "y = " + (ActiveGraphFunction?.Expression ?? string.Empty);
        Expression = "Graphing";
    }

    [RelayCommand]
    private void GraphKey(string key)
    {
        var activeFunction = ActiveGraphFunction ?? GraphFunctions.FirstOrDefault();
        if (activeFunction is null)
        {
            activeFunction = AddGraphFunction();
            ActiveGraphFunction = activeFunction;
        }

        var currentExpression = activeFunction.Expression;
        var insertion = key switch
        {
            "Pi" => "pi",
            "Square" => "^2",
            "Power" => "^",
            "Sqrt" => "sqrt(",
            "TenPow" => "10^",
            "Reciprocal" => "1/",
            "Back" => string.Empty,
            _ => key,
        };

        if (key == "Back" && currentExpression.Length > 0)
        {
            currentExpression = currentExpression[..^1];
        }
        else
        {
            currentExpression += insertion;
        }

        activeFunction.Expression = currentExpression;

        PlotGraph();
    }

    [RelayCommand]
    private void AddGraphExpression()
    {
        ActiveGraphFunction = AddGraphFunction();
    }

    [RelayCommand]
    private void RemoveGraphExpression(GraphFunction function)
    {
        if (GraphFunctions.Count <= 1)
        {
            function.Expression = string.Empty;
            return;
        }

        GraphFunctions.Remove(function);
        ActiveGraphFunction ??= GraphFunctions.FirstOrDefault();
        PlotGraph();
    }

    partial void OnActiveGraphFunctionChanged(GraphFunction? value)
    {
        if (IsGraphing)
        {
            PlotGraph();
        }
    }

    partial void OnGraphParameterChanged(double value)
    {
        if (IsGraphing)
        {
            PlotGraph();
        }
    }

    partial void OnGraphZoomChanged(double value)
    {
        PlotGraph();
    }

    [RelayCommand]
    private void ClearGraph()
    {
        foreach (var function in GraphFunctions)
        {
            function.Expression = string.Empty;
        }
        GraphVariables.Clear();
        GraphVariablesText = string.Empty;
        PlotGraph();
    }

    [RelayCommand]
    private void ZoomGraph(string direction)
    {
        var factor = direction switch
        {
            "In" => 0.8,
            "Out" => 1.25,
            _ => 0,
        };

        if (factor == 0)
        {
            ResetGraphView();
            return;
        }

        var xCenter = (GraphXMin + GraphXMax) / 2;
        var yCenter = (GraphYMin + GraphYMax) / 2;
        var xHalfRange = (GraphXMax - GraphXMin) * factor / 2;
        var yHalfRange = (GraphYMax - GraphYMin) * factor / 2;
        GraphXMin = xCenter - xHalfRange;
        GraphXMax = xCenter + xHalfRange;
        GraphYMin = yCenter - yHalfRange;
        GraphYMax = yCenter + yHalfRange;
    }

    [RelayCommand]
    private void ToggleGraphOptions()
    {
        IsGraphOptionsOpen = !IsGraphOptionsOpen;
    }

    [RelayCommand]
    private void SetAngleUnit(string unit)
    {
        AngleUnit = unit;
        PlotGraph();
    }

    [RelayCommand]
    private void ResetGraphView()
    {
        GraphXMin = -10;
        GraphXMax = 10;
        GraphYMin = -10;
        GraphYMax = 10;
        GraphZoom = 38;
    }

    partial void OnGraphXMinChanged(double value) => PlotIfGraphing();
    partial void OnGraphXMaxChanged(double value) => PlotIfGraphing();
    partial void OnGraphYMinChanged(double value) => PlotIfGraphing();
    partial void OnGraphYMaxChanged(double value) => PlotIfGraphing();
    partial void OnGraphLineThicknessChanged(double value) => PlotIfGraphing();

    private void PlotIfGraphing()
    {
        if (IsGraphing)
        {
            PlotGraph();
        }
    }

    private void SyncGraphVariables()
    {
        var variableNames = GraphFunctions
            .SelectMany(function => ExtractVariables(function.Expression))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var stale in GraphVariables.Where(variable => !variableNames.Contains(variable.Name, StringComparer.OrdinalIgnoreCase)).ToArray())
        {
            GraphVariables.Remove(stale);
        }

        foreach (var name in variableNames)
        {
            if (GraphVariables.All(variable => !string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                var variable = new GraphVariable(name, name == "f" ? 2.7 : 1);
                variable.PropertyChanged += (_, _) => UpdateGraphVariablesText();
                GraphVariables.Add(variable);
            }
        }

        UpdateGraphVariablesText();
    }

    private void UpdateGraphVariablesText()
    {
        GraphVariablesText = string.Join(
            ";",
            GraphVariables.Select(variable => variable.Name + "=" + variable.Value.ToString(CultureInfo.InvariantCulture)));
    }

    private static IEnumerable<string> ExtractVariables(string expression)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "x", "pi", "e", "sin", "cos", "tan", "log", "ln", "sqrt", "abs"
        };

        return Regex.Matches(expression, "[a-zA-Z]+")
            .Select(match => match.Value)
            .Where(name => name.Length == 1 && !reserved.Contains(name));
    }

    private GraphFunction AddGraphFunction(string expression = "")
    {
        var function = new GraphFunction(GraphFunctions.Count + 1, expression);
        function.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(GraphFunction.Expression) && IsGraphing)
            {
                PlotGraph();
            }
        };
        GraphFunctions.Add(function);
        return function;
    }

    private void ApplyProgrammerState(ProgrammerState state)
    {
        Display = state.Display;
        Expression = state.Expression;
        ProgrammerHex = state.Hex;
        ProgrammerDec = state.Dec;
        ProgrammerOct = state.Oct;
        ProgrammerBin = state.Bin;
    }

    [RelayCommand]
    private void ClearHistory()
    {
        HistoryItems.Clear();
        OnPropertyChanged(nameof(HasHistory));
    }

    [RelayCommand]
    private void UseHistoryItem(CalculationHistoryItem item)
    {
        Display = item.Result;
        Expression = item.Expression;
    }

    private void AddHistoryIfCompleted(string key, string expression, string result)
    {
        if (key != "=" || string.IsNullOrWhiteSpace(expression) || !IsHistoryResult(result))
        {
            return;
        }

        var normalizedExpression = expression.Trim();
        if (!normalizedExpression.EndsWith("=", StringComparison.Ordinal))
        {
            normalizedExpression += " =";
        }

        HistoryItems.Insert(0, new CalculationHistoryItem(normalizedExpression, result, Mode));
        while (HistoryItems.Count > MaximumHistoryItems)
        {
            HistoryItems.RemoveAt(HistoryItems.Count - 1);
        }

        OnPropertyChanged(nameof(HasHistory));
    }

    private static bool IsHistoryResult(string result)
    {
        return !string.IsNullOrWhiteSpace(result)
            && !result.Contains("Invalid", StringComparison.OrdinalIgnoreCase)
            && !result.Contains("Cannot", StringComparison.OrdinalIgnoreCase)
            && !result.Contains("Error", StringComparison.OrdinalIgnoreCase);
    }
}
