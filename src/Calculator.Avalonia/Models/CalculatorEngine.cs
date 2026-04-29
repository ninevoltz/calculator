using System;
using System.Globalization;

namespace Calculator.Avalonia.Models;

public sealed record CalculatorState(string Display, string Expression);

public sealed class CalculatorEngine
{
    private decimal? _leftOperand;
    private string? _pendingOperator;
    private string _display = "0";
    private string _expression = string.Empty;
    private bool _replaceDisplay;

    public CalculatorState Reset()
    {
        _leftOperand = null;
        _pendingOperator = null;
        _display = "0";
        _expression = string.Empty;
        _replaceDisplay = false;
        return State;
    }

    public CalculatorState Press(string key)
    {
        if (key.Length == 1 && char.IsDigit(key[0]))
        {
            AppendDigit(key);
        }
        else
        {
            HandleCommand(key);
        }

        return State;
    }

    private CalculatorState State => new(_display, _expression);

    private decimal CurrentValue => decimal.Parse(_display, CultureInfo.InvariantCulture);

    private void AppendDigit(string digit)
    {
        if (_replaceDisplay || _display == "0")
        {
            _display = digit;
            _replaceDisplay = false;
            return;
        }

        if (_display is "-0")
        {
            _display = "-" + digit;
            return;
        }

        _display += digit;
    }

    private void HandleCommand(string key)
    {
        switch (key)
        {
            case ".":
                if (_replaceDisplay)
                {
                    _display = "0";
                    _replaceDisplay = false;
                }

                if (!_display.Contains('.', StringComparison.Ordinal))
                {
                    _display += ".";
                }
                break;
            case "C":
                Reset();
                break;
            case "CE":
                _display = "0";
                _replaceDisplay = false;
                break;
            case "Back":
                Backspace();
                break;
            case "Sign":
                ToggleSign();
                break;
            case "%":
                ApplyPercent();
                break;
            case "Square":
                ApplyUnary("sqr", value => value * value);
                break;
            case "Sqrt":
                ApplyUnary("sqrt", value => value < 0 ? null : (decimal?)Math.Sqrt((double)value));
                break;
            case "Reciprocal":
                ApplyUnary("1/", value => value == 0 ? null : 1 / value);
                break;
            case "Abs":
                ApplyUnary("abs", value => Math.Abs(value));
                break;
            case "Factorial":
                ApplyFactorial();
                break;
            case "Pi":
                _display = Format(Math.PI);
                _expression = "pi";
                _replaceDisplay = true;
                break;
            case "E":
                _display = Format(Math.E);
                _expression = "e";
                _replaceDisplay = true;
                break;
            case "Sin":
                ApplyUnaryDouble("sin", Math.Sin);
                break;
            case "Cos":
                ApplyUnaryDouble("cos", Math.Cos);
                break;
            case "Tan":
                ApplyUnaryDouble("tan", Math.Tan);
                break;
            case "Log":
                ApplyUnaryDouble("log", value => value <= 0 ? double.NaN : Math.Log10(value));
                break;
            case "Ln":
                ApplyUnaryDouble("ln", value => value <= 0 ? double.NaN : Math.Log(value));
                break;
            case "Exp":
                ApplyUnaryDouble("exp", Math.Exp);
                break;
            case "TenPow":
                ApplyUnaryDouble("10^", value => Math.Pow(10, value));
                break;
            case "+":
            case "-":
            case "*":
            case "/":
            case "Mod":
            case "Pow":
                ApplyOperator(key);
                break;
            case "=":
                ApplyEquals();
                break;
        }
    }

    private void Backspace()
    {
        if (_replaceDisplay || _display is "0")
        {
            return;
        }

        _display = _display.Length <= 1 || (_display.Length == 2 && _display[0] == '-')
            ? "0"
            : _display[..^1];
    }

    private void ToggleSign()
    {
        _display = _display.StartsWith("-", StringComparison.Ordinal)
            ? _display[1..]
            : "-" + _display;

        if (_display == "-0")
        {
            _display = "0";
        }
    }

    private void ApplyPercent()
    {
        var value = CurrentValue;
        if (_leftOperand is { } left)
        {
            value = left * value / 100;
        }
        else
        {
            value /= 100;
        }

        _display = Format(value);
        _replaceDisplay = true;
    }

    private void ApplyFactorial()
    {
        var input = CurrentValue;
        if (input < 0 || input != decimal.Truncate(input) || input > 27)
        {
            SetError("Invalid input");
            return;
        }

        decimal result = 1;
        for (var i = 2; i <= (int)input; i++)
        {
            result *= i;
        }

        _display = Format(result);
        _expression = $"fact({Format(input)})";
        _replaceDisplay = true;
    }

    private void ApplyUnaryDouble(string label, Func<double, double> operation)
    {
        var input = CurrentValue;
        var result = operation((double)input);
        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            SetError("Invalid input");
            return;
        }

        _display = Format(result);
        _expression = $"{label}({Format(input)})";
        _replaceDisplay = true;
    }

    private void ApplyUnary(string label, Func<decimal, decimal?> operation)
    {
        var input = CurrentValue;
        var result = operation(input);
        if (result is null)
        {
            SetError("Invalid input");
            return;
        }

        _display = Format(result.Value);
        _expression = $"{label}({Format(input)})";
        _replaceDisplay = true;
    }

    private void ApplyOperator(string op)
    {
        if (_leftOperand is not null && _pendingOperator is not null && !_replaceDisplay)
        {
            if (!TryEvaluate(CurrentValue, out var result))
            {
                return;
            }

            _leftOperand = result;
            _display = Format(result);
        }
        else
        {
            _leftOperand = CurrentValue;
        }

        _pendingOperator = op;
        _expression = $"{Format(_leftOperand.Value)} {DisplayOperator(op)}";
        _replaceDisplay = true;
    }

    private void ApplyEquals()
    {
        if (_leftOperand is null || _pendingOperator is null)
        {
            return;
        }

        var right = CurrentValue;
        if (!TryEvaluate(right, out var result))
        {
            return;
        }

        _expression = $"{Format(_leftOperand.Value)} {DisplayOperator(_pendingOperator)} {Format(right)} =";
        _display = Format(result);
        _leftOperand = null;
        _pendingOperator = null;
        _replaceDisplay = true;
    }

    private bool TryEvaluate(decimal right, out decimal result)
    {
        result = 0;
        if (_leftOperand is not { } left || _pendingOperator is null)
        {
            return false;
        }

        if ((_pendingOperator == "/" || _pendingOperator == "Mod") && right == 0)
        {
            SetError("Cannot divide by zero");
            return false;
        }

        if (_pendingOperator == "Pow")
        {
            return TryEvaluatePower(left, right, out result);
        }

        result = _pendingOperator switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => left / right,
            "Mod" => left % right,
            _ => right,
        };
        return true;
    }

    private bool TryEvaluatePower(decimal left, decimal right, out decimal result)
    {
        var power = Math.Pow((double)left, (double)right);
        if (double.IsNaN(power) || double.IsInfinity(power) || power > (double)decimal.MaxValue || power < (double)decimal.MinValue)
        {
            result = 0;
            SetError("Invalid input");
            return false;
        }

        result = (decimal)power;
        return true;
    }

    private void SetError(string message)
    {
        _display = message;
        _expression = string.Empty;
        _leftOperand = null;
        _pendingOperator = null;
        _replaceDisplay = true;
    }

    private static string DisplayOperator(string op) => op switch
    {
        "*" => "×",
        "/" => "÷",
        "Mod" => "mod",
        "Pow" => "^",
        _ => op,
    };

    private static string Format(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);

    private static string Format(double value) => value.ToString("G15", CultureInfo.InvariantCulture);
}
