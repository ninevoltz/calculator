# Linux Port Summary

This document summarizes the initial work done to start porting the Windows Calculator codebase to Linux.

## Environment Setup

- Installed the .NET 8 SDK from the Ubuntu package repositories.
- Installed the Avalonia project templates with:

```bash
dotnet new install Avalonia.Templates
```

- Used Avalonia as the Linux desktop UI framework.

## Native Calculator Engine Port

- Added a root `CMakeLists.txt` that builds the existing `src/CalcManager` C++ calculator engine as a Linux static library.
- Adjusted a few `CalcManager` files so the portable C++ model can compile with GCC on Linux:
  - `src/CalcManager/pch.h`
  - `src/CalcManager/CalculatorHistory.h`
  - `src/CalcManager/Header Files/IHistoryDisplay.h`
  - `src/CalcManager/Ratpack/support.cpp`

The native engine currently builds with:

```bash
cmake -S . -B build/linux
cmake --build build/linux -j2
```

## Avalonia Application

- Created a new Avalonia desktop app at:

```text
src/Calculator.Avalonia
```

- Retargeted the generated Avalonia project from `net10.0` to `net8.0`.
- Added an Avalonia UI that supports:
  - Standard mode
  - Scientific mode
  - Programmer mode
  - Graphing mode

The app builds and runs with:

```bash
dotnet build src/Calculator.Avalonia/Calculator.Avalonia.csproj
dotnet run --project src/Calculator.Avalonia/Calculator.Avalonia.csproj
```

## Current UI Functionality

### Standard and Scientific

- Added working calculator keypads.
- Added common scientific operations such as `sin`, `cos`, `tan`, `log`, `ln`, `x^y`, `10^x`, `n!`, `mod`, constants, and unary operations.

### Programmer

- Added integer/base mode UI with:
  - HEX, DEC, OCT, BIN display rows
  - Base switching
  - Hex digit input
  - Bitwise operations such as AND, OR, XOR, NOT
  - Shift operations
  - Basic arithmetic

### Graphing

- Added a Windows Calculator-inspired graphing layout with:
  - Large plotting canvas
  - Expression cards
  - Graph keypad
  - Zoom controls
  - Graph options panel
  - Grid axes and numeric labels
  - Cursor tracking dot and coordinate tooltip
  - Auto-generated variable sliders
  - Expandable slider range controls for min, step, and max

- The graph parser supports expressions using:
  - `x`
  - one-letter slider variables such as `a`, `b`, `f`, `p`, `r`
  - `pi`, `e`
  - `sin`, `cos`, `tan`, `log`, `ln`, `sqrt`, `abs`
  - powers, arithmetic, parentheses, and implicit multiplication

## Current State

- The Avalonia app has a functional Linux UI and can be launched locally.
- The existing native `CalcManager` engine builds on Linux through CMake.
- A native bridge library now exposes the `CalcManager` model through a small C ABI in `src/Calculator.NativeBridge`.
- The Avalonia app calls the native bridge through P/Invoke for Standard and Scientific calculator modes, with the managed implementation kept as a fallback.
- Programmer and Graphing modes still use managed C# logic.
- The next major integration step is to either extend the native bridge for Programmer mode or port the remaining Windows Calculator view-model behavior that is still represented by interim managed logic.
