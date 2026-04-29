using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Calculator.Avalonia.Models;

namespace Calculator.Avalonia.Controls;

public sealed class GraphPlot : Control
{
    private Point? _cursorPosition;

    public static readonly StyledProperty<string> ExpressionsProperty =
        AvaloniaProperty.Register<GraphPlot, string>(nameof(Expressions), "x*sin(x)");

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<GraphPlot, double>(nameof(Zoom), 38);

    public static readonly StyledProperty<double> ParameterProperty =
        AvaloniaProperty.Register<GraphPlot, double>(nameof(Parameter), 2.7);

    public static readonly StyledProperty<string> VariablesProperty =
        AvaloniaProperty.Register<GraphPlot, string>(nameof(Variables), string.Empty);

    public static readonly StyledProperty<double> XMinProperty =
        AvaloniaProperty.Register<GraphPlot, double>(nameof(XMin), -10);

    public static readonly StyledProperty<double> XMaxProperty =
        AvaloniaProperty.Register<GraphPlot, double>(nameof(XMax), 10);

    public static readonly StyledProperty<double> YMinProperty =
        AvaloniaProperty.Register<GraphPlot, double>(nameof(YMin), -10);

    public static readonly StyledProperty<double> YMaxProperty =
        AvaloniaProperty.Register<GraphPlot, double>(nameof(YMax), 10);

    public static readonly StyledProperty<string> AngleUnitProperty =
        AvaloniaProperty.Register<GraphPlot, string>(nameof(AngleUnit), "Radians");

    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<GraphPlot, double>(nameof(LineThickness), 2.4);

    public string Expressions
    {
        get => GetValue(ExpressionsProperty);
        set => SetValue(ExpressionsProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double Parameter
    {
        get => GetValue(ParameterProperty);
        set => SetValue(ParameterProperty, value);
    }

    public string Variables
    {
        get => GetValue(VariablesProperty);
        set => SetValue(VariablesProperty, value);
    }

    public double XMin
    {
        get => GetValue(XMinProperty);
        set => SetValue(XMinProperty, value);
    }

    public double XMax
    {
        get => GetValue(XMaxProperty);
        set => SetValue(XMaxProperty, value);
    }

    public double YMin
    {
        get => GetValue(YMinProperty);
        set => SetValue(YMinProperty, value);
    }

    public double YMax
    {
        get => GetValue(YMaxProperty);
        set => SetValue(YMaxProperty, value);
    }

    public string AngleUnit
    {
        get => GetValue(AngleUnitProperty);
        set => SetValue(AngleUnitProperty, value);
    }

    public double LineThickness
    {
        get => GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    static GraphPlot()
    {
        AffectsRender<GraphPlot>(
            ExpressionsProperty,
            ZoomProperty,
            ParameterProperty,
            VariablesProperty,
            XMinProperty,
            XMaxProperty,
            YMinProperty,
            YMaxProperty,
            AngleUnitProperty,
            LineThicknessProperty);
    }

    public GraphPlot()
    {
        ClipToBounds = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _cursorPosition = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _cursorPosition = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.DrawRectangle(Brushes.White, new Pen(Brush.Parse("#d8d8d8")), bounds, 6, 6);

        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        var xMin = Math.Min(XMin, XMax - 0.0001);
        var xMax = Math.Max(XMax, XMin + 0.0001);
        var yMin = Math.Min(YMin, YMax - 0.0001);
        var yMax = Math.Max(YMax, YMin + 0.0001);
        var axisPen = new Pen(Brush.Parse("#202020"), 1);
        var minorGridPen = new Pen(Brush.Parse("#ececec"), 1);
        var majorGridPen = new Pen(Brush.Parse("#d3d3d3"), 1);
        var labelBrush = Brush.Parse("#303030");
        var typeface = new Typeface("Inter");
        var xScale = bounds.Width / (xMax - xMin);
        var yScale = bounds.Height / (yMax - yMin);
        var xStep = ChooseGridStep(xMax - xMin);
        var yStep = ChooseGridStep(yMax - yMin);

        for (var x = Math.Ceiling(xMin / (xStep / 2)) * (xStep / 2); x <= xMax; x += xStep / 2)
        {
            var px = ToPixelX(x, bounds, xMin, xScale);
            context.DrawLine(minorGridPen, new Point(px, 0), new Point(px, bounds.Height));
        }

        for (var y = Math.Ceiling(yMin / (yStep / 2)) * (yStep / 2); y <= yMax; y += yStep / 2)
        {
            var py = ToPixelY(y, bounds, yMin, yScale);
            context.DrawLine(minorGridPen, new Point(0, py), new Point(bounds.Width, py));
        }

        for (var x = Math.Ceiling(xMin / xStep) * xStep; x <= xMax; x += xStep)
        {
            var px = ToPixelX(x, bounds, xMin, xScale);
            context.DrawLine(majorGridPen, new Point(px, 0), new Point(px, bounds.Height));
            DrawLabel(context, FormatAxisValue(x), new Point(px + 3, ToPixelY(0, bounds, yMin, yScale) + 3), typeface, labelBrush);
        }

        for (var y = Math.Ceiling(yMin / yStep) * yStep; y <= yMax; y += yStep)
        {
            var py = ToPixelY(y, bounds, yMin, yScale);
            context.DrawLine(majorGridPen, new Point(0, py), new Point(bounds.Width, py));
            if (Math.Abs(y) > yStep / 100)
            {
                DrawLabel(context, FormatAxisValue(y), new Point(ToPixelX(0, bounds, xMin, xScale) + 4, py - 16), typeface, labelBrush);
            }
        }

        var yAxis = ToPixelX(0, bounds, xMin, xScale);
        var xAxis = ToPixelY(0, bounds, yMin, yScale);
        context.DrawLine(axisPen, new Point(0, xAxis), new Point(bounds.Width, xAxis));
        context.DrawLine(axisPen, new Point(yAxis, 0), new Point(yAxis, bounds.Height));

        var colors = new[] { "#0067c0", "#c42b1c", "#107c10", "#8764b8" };
        var evaluator = new MathExpressionEvaluator();
        var variables = ParseVariables();
        var expressions = Expressions.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        (Point point, double x, double y, IBrush brush)? cursorValue = null;
        for (var expressionIndex = 0; expressionIndex < expressions.Length; expressionIndex++)
        {
            var plotBrush = Brush.Parse(colors[expressionIndex % colors.Length]);
            var plotPen = new Pen(plotBrush, LineThickness);
            Point? previous = null;
            for (var px = 0; px < bounds.Width; px++)
            {
                var x = xMin + px / xScale;
                if (!evaluator.TryEvaluate(expressions[expressionIndex], x, variables, AngleUnit, out var y) || y < -1_000_000 || y > 1_000_000)
                {
                    previous = null;
                    continue;
                }

                var point = new Point(px, ToPixelY(y, bounds, yMin, yScale));
                if (point.Y < -bounds.Height || point.Y > bounds.Height * 2)
                {
                    previous = null;
                    continue;
                }

                if (previous is { } p)
                {
                    context.DrawLine(plotPen, p, point);
                }

                previous = point;
            }

            if (_cursorPosition is { } cursor)
            {
                var cursorX = xMin + cursor.X / xScale;
                if (evaluator.TryEvaluate(expressions[expressionIndex], cursorX, variables, AngleUnit, out var cursorY))
                {
                    var cursorPoint = new Point(cursor.X, ToPixelY(cursorY, bounds, yMin, yScale));
                    if (cursorPoint.Y >= 0 && cursorPoint.Y <= bounds.Height)
                    {
                        var distance = Math.Abs(cursorPoint.Y - cursor.Y);
                        if (cursorValue is null || distance < Math.Abs(cursorValue.Value.point.Y - cursor.Y))
                        {
                            cursorValue = (cursorPoint, cursorX, cursorY, plotBrush);
                        }
                    }
                }
            }
        }

        if (cursorValue is { } value)
        {
            DrawCursorValue(context, value.point, value.x, value.y, value.brush, bounds, typeface);
        }
    }

    private Dictionary<string, double> ParseVariables()
    {
        var variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["f"] = Parameter
        };

        foreach (var pair in Variables.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                variables[parts[0]] = value;
            }
        }

        return variables;
    }

    private static double ToPixelX(double x, Rect bounds, double xMin, double xScale) => (x - xMin) * xScale;
    private static double ToPixelY(double y, Rect bounds, double yMin, double yScale) => bounds.Height - (y - yMin) * yScale;

    private static double ChooseGridStep(double range)
    {
        var rough = range / 10;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var normalized = rough / magnitude;
        return (normalized < 2 ? 1 : normalized < 5 ? 2 : 5) * magnitude;
    }

    private static string FormatAxisValue(double value)
    {
        return Math.Abs(value) < 0.0000001 ? "0" : value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static void DrawLabel(DrawingContext context, string text, Point origin, Typeface typeface, IBrush brush)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, brush);
        context.DrawText(formatted, origin);
    }

    private static void DrawCursorValue(DrawingContext context, Point point, double x, double y, IBrush brush, Rect bounds, Typeface typeface)
    {
        var dotPen = new Pen(Brushes.White, 2);
        context.DrawEllipse(brush, dotPen, point, 4.5, 4.5);

        var text = $"({FormatCursorValue(x)}, {FormatCursorValue(y)})";
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, Brush.Parse("#202020"));
        var tooltipWidth = formatted.Width + 18;
        var tooltipHeight = formatted.Height + 12;
        var tooltipX = Math.Clamp(point.X + 12, 8, Math.Max(8, bounds.Width - tooltipWidth - 8));
        var tooltipY = Math.Clamp(point.Y - tooltipHeight - 12, 8, Math.Max(8, bounds.Height - tooltipHeight - 8));
        var tooltipRect = new Rect(tooltipX, tooltipY, tooltipWidth, tooltipHeight);

        context.DrawRectangle(Brush.Parse("#f7f7f7"), new Pen(Brush.Parse("#d8d8d8")), tooltipRect, 4, 4);
        context.DrawText(formatted, new Point(tooltipX + 9, tooltipY + 6));
    }

    private static string FormatCursorValue(double value)
    {
        return Math.Abs(value) >= 10000 || (Math.Abs(value) > 0 && Math.Abs(value) < 0.001)
            ? value.ToString("0.#####E+0", CultureInfo.InvariantCulture)
            : value.ToString("0.#####", CultureInfo.InvariantCulture);
    }
}
