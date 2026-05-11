using System;
using System.Runtime.InteropServices;

namespace Calculator.Avalonia.Models;

public sealed class NativeCalculatorEngine : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public NativeCalculatorEngine()
    {
        try
        {
            _handle = calc_create();
            IsAvailable = _handle != IntPtr.Zero;
        }
        catch (DllNotFoundException)
        {
            IsAvailable = false;
        }
        catch (EntryPointNotFoundException)
        {
            IsAvailable = false;
        }
        catch (BadImageFormatException)
        {
            IsAvailable = false;
        }
    }

    ~NativeCalculatorEngine()
    {
        Dispose();
    }

    public bool IsAvailable { get; }

    public CalculatorState Reset()
    {
        if (!IsAvailable)
        {
            return new CalculatorState("0", string.Empty);
        }

        calc_reset(_handle);
        return State;
    }

    public void SetScientificMode(bool scientific)
    {
        if (IsAvailable)
        {
            calc_set_mode(_handle, scientific ? 1 : 0);
        }
    }

    public bool TryPress(string key, out CalculatorState state)
    {
        if (!IsAvailable)
        {
            state = new CalculatorState("0", string.Empty);
            return false;
        }

        var handled = calc_send_key(_handle, key) != 0;
        state = State;
        return handled;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            calc_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private CalculatorState State => new(ReadUtf8(calc_get_display(_handle), "0"), ReadUtf8(calc_get_expression(_handle), string.Empty));

    private static string ReadUtf8(IntPtr pointer, string fallback)
    {
        return pointer == IntPtr.Zero ? fallback : Marshal.PtrToStringUTF8(pointer) ?? fallback;
    }

    [DllImport("nativecalculator", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr calc_create();

    [DllImport("nativecalculator", CallingConvention = CallingConvention.Cdecl)]
    private static extern void calc_destroy(IntPtr calculator);

    [DllImport("nativecalculator", CallingConvention = CallingConvention.Cdecl)]
    private static extern void calc_reset(IntPtr calculator);

    [DllImport("nativecalculator", CallingConvention = CallingConvention.Cdecl)]
    private static extern void calc_set_mode(IntPtr calculator, int scientific);

    [DllImport("nativecalculator", CallingConvention = CallingConvention.Cdecl)]
    private static extern int calc_send_key(IntPtr calculator, string key);

    [DllImport("nativecalculator", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr calc_get_display(IntPtr calculator);

    [DllImport("nativecalculator", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr calc_get_expression(IntPtr calculator);
}
