using System;

namespace Calculator.Avalonia.Models;

public sealed record ProgrammerState(string Display, string Hex, string Dec, string Oct, string Bin, string Expression);

public sealed class ProgrammerEngine
{
    private long? _leftOperand;
    private string? _pendingOperator;
    private long _value;
    private int _radix = 10;
    private bool _replaceDisplay = true;
    private string _expression = string.Empty;

    public ProgrammerState Reset()
    {
        _leftOperand = null;
        _pendingOperator = null;
        _value = 0;
        _radix = 10;
        _replaceDisplay = true;
        _expression = string.Empty;
        return State;
    }

    public ProgrammerState Press(string key)
    {
        if (IsDigitForRadix(key))
        {
            AppendDigit(key);
        }
        else
        {
            HandleCommand(key);
        }

        return State;
    }

    private ProgrammerState State => new(Format(_value, _radix), Format(_value, 16), Format(_value, 10), Format(_value, 8), Format(_value, 2), _expression);

    private void AppendDigit(string digit)
    {
        var current = _replaceDisplay ? string.Empty : Format(_value, _radix);
        var next = current == "0" ? digit : current + digit;
        _value = Convert.ToInt64(next, _radix);
        _replaceDisplay = false;
    }

    private void HandleCommand(string key)
    {
        switch (key)
        {
            case "Hex":
                _radix = 16;
                break;
            case "Dec":
                _radix = 10;
                break;
            case "Oct":
                _radix = 8;
                break;
            case "Bin":
                _radix = 2;
                break;
            case "Clear":
            case "CE":
                _leftOperand = null;
                _pendingOperator = null;
                _value = 0;
                _expression = string.Empty;
                _replaceDisplay = true;
                break;
            case "Back":
                Backspace();
                break;
            case "Not":
                _value = ~_value;
                _expression = "not";
                _replaceDisplay = true;
                break;
            case "Lsh":
            case "Rsh":
            case "And":
            case "Or":
            case "Xor":
            case "+":
            case "-":
            case "*":
            case "/":
            case "Mod":
                ApplyOperator(key);
                break;
            case "=":
                ApplyEquals();
                break;
        }
    }

    private void Backspace()
    {
        if (_replaceDisplay)
        {
            return;
        }

        var current = Format(_value, _radix);
        if (current.Length <= 1)
        {
            _value = 0;
        }
        else
        {
            _value = Convert.ToInt64(current[..^1], _radix);
        }
    }

    private void ApplyOperator(string op)
    {
        if (_leftOperand is not null && _pendingOperator is not null && !_replaceDisplay)
        {
            _value = Evaluate(_leftOperand.Value, _value, _pendingOperator);
            _leftOperand = _value;
        }
        else
        {
            _leftOperand = _value;
        }

        _pendingOperator = op;
        _expression = $"{Format(_leftOperand.Value, _radix)} {DisplayOperator(op)}";
        _replaceDisplay = true;
    }

    private void ApplyEquals()
    {
        if (_leftOperand is null || _pendingOperator is null)
        {
            return;
        }

        var right = _value;
        _value = Evaluate(_leftOperand.Value, right, _pendingOperator);
        _expression = $"{Format(_leftOperand.Value, _radix)} {DisplayOperator(_pendingOperator)} {Format(right, _radix)} =";
        _leftOperand = null;
        _pendingOperator = null;
        _replaceDisplay = true;
    }

    private static long Evaluate(long left, long right, string op) => op switch
    {
        "+" => left + right,
        "-" => left - right,
        "*" => left * right,
        "/" => right == 0 ? 0 : left / right,
        "Mod" => right == 0 ? 0 : left % right,
        "And" => left & right,
        "Or" => left | right,
        "Xor" => left ^ right,
        "Lsh" => left << ClampShift(right),
        "Rsh" => left >> ClampShift(right),
        _ => right,
    };

    private bool IsDigitForRadix(string key)
    {
        if (key.Length != 1)
        {
            return false;
        }

        var digit = key[0] switch
        {
            >= '0' and <= '9' => key[0] - '0',
            >= 'A' and <= 'F' => key[0] - 'A' + 10,
            _ => -1,
        };
        return digit >= 0 && digit < _radix;
    }

    private static int ClampShift(long value) => (int)Math.Clamp(value, 0, 63);

    private static string DisplayOperator(string op) => op switch
    {
        "*" => "x",
        "/" => "÷",
        "Mod" => "mod",
        _ => op.ToLowerInvariant(),
    };

    private static string Format(long value, int radix) => radix switch
    {
        2 => Convert.ToString(value, 2),
        8 => Convert.ToString(value, 8),
        10 => value.ToString(),
        16 => Convert.ToString(value, 16).ToUpperInvariant(),
        _ => value.ToString(),
    };
}
