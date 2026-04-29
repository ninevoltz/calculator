using System;
using System.Collections.Generic;
using System.Globalization;

namespace Calculator.Avalonia.Models;

public sealed class MathExpressionEvaluator
{
    private string _text = string.Empty;
    private int _position;
    private double _x;
    private Dictionary<string, double> _variables = new(StringComparer.OrdinalIgnoreCase);
    private string _angleUnit = "Radians";

    public bool TryEvaluate(string expression, double x, out double result)
    {
        return TryEvaluate(expression, x, 2.7, out result);
    }

    public bool TryEvaluate(string expression, double x, double f, out double result)
    {
        return TryEvaluate(expression, x, new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["f"] = f }, "Radians", out result);
    }

    public bool TryEvaluate(string expression, double x, Dictionary<string, double> variables, string angleUnit, out double result)
    {
        try
        {
            _text = expression.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            _position = 0;
            _x = x;
            _variables = variables;
            _angleUnit = angleUnit;
            result = ParseExpression();
            return _position == _text.Length && !double.IsNaN(result) && !double.IsInfinity(result);
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    private double ParseExpression()
    {
        var value = ParseTerm();
        while (Match('+') || Match('-'))
        {
            var op = _text[_position - 1];
            var right = ParseTerm();
            value = op == '+' ? value + right : value - right;
        }

        return value;
    }

    private double ParseTerm()
    {
        var value = ParsePower();
        while (Match('*') || Match('/') || IsImplicitMultiplicationStart())
        {
            var op = _text[_position - 1];
            var right = ParsePower();
            value = op == '/' ? value / right : value * right;
        }

        return value;
    }

    private double ParsePower()
    {
        var value = ParseUnary();
        if (Match('^'))
        {
            value = Math.Pow(value, ParsePower());
        }

        return value;
    }

    private double ParseUnary()
    {
        if (Match('+'))
        {
            return ParseUnary();
        }

        if (Match('-'))
        {
            return -ParseUnary();
        }

        return ParsePrimary();
    }

    private double ParsePrimary()
    {
        if (Match('('))
        {
            var value = ParseExpression();
            Require(')');
            return value;
        }

        if (PeekLetter())
        {
            var name = ParseName();
            return name switch
            {
                "x" => _x,
                "pi" => Math.PI,
                "e" => Math.E,
                "sin" => ApplyTrigFunction(Math.Sin),
                "cos" => ApplyTrigFunction(Math.Cos),
                "tan" => ApplyTrigFunction(Math.Tan),
                "log" => ApplyFunction(Math.Log10),
                "ln" => ApplyFunction(Math.Log),
                "sqrt" => ApplyFunction(Math.Sqrt),
                "abs" => ApplyFunction(Math.Abs),
                _ when _variables.TryGetValue(name, out var value) => value,
                _ => throw new InvalidOperationException("Unknown function"),
            };
        }

        return ParseNumber();
    }

    private double ApplyFunction(Func<double, double> function)
    {
        Require('(');
        var argument = ParseExpression();
        Require(')');
        return function(argument);
    }

    private double ApplyTrigFunction(Func<double, double> function)
    {
        Require('(');
        var argument = ParseExpression();
        Require(')');
        return function(ConvertAngleToRadians(argument));
    }

    private double ConvertAngleToRadians(double value) => _angleUnit switch
    {
        "Degrees" => value * Math.PI / 180,
        "Gradians" => value * Math.PI / 200,
        _ => value,
    };

    private string ParseName()
    {
        var start = _position;
        while (_position < _text.Length && char.IsLetter(_text[_position]))
        {
            _position++;
        }

        return _text[start.._position];
    }

    private double ParseNumber()
    {
        var start = _position;
        while (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] == '.'))
        {
            _position++;
        }

        if (start == _position)
        {
            throw new InvalidOperationException("Expected number");
        }

        return double.Parse(_text[start.._position], CultureInfo.InvariantCulture);
    }

    private void Require(char character)
    {
        if (!Match(character))
        {
            throw new InvalidOperationException("Unexpected expression");
        }
    }

    private bool Match(char character)
    {
        if (_position >= _text.Length || _text[_position] != character)
        {
            return false;
        }

        _position++;
        return true;
    }

    private bool PeekLetter() => _position < _text.Length && char.IsLetter(_text[_position]);

    private bool IsImplicitMultiplicationStart()
    {
        return _position < _text.Length && (_text[_position] == '(' || _text[_position] == '.' || char.IsDigit(_text[_position]) || char.IsLetter(_text[_position]));
    }
}
