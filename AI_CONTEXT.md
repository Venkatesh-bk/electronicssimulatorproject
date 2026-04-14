# AI Context & Project State

> **AI INSTRUCTION:** If you are an AI assistant reading this file, treat this as the **ultimate source of truth** for the project. **Whenever you successfully complete a task or modify the codebase, you MUST append a concise summary of your changes to the `Change Log` section at the bottom of this file.**

---

## 1. Project Goal

Build a **professional-grade, all-in-one Electronic Design Automation (EDA) and Engineering Simulation platform for Windows**, targeting the capability level of:

| Reference Software | Capabilities We Are Matching |
|-------------------|------------------------------|
| **Proteus Professional** | Schematic capture, PCB layout, mixed-signal simulation, microcontroller simulation, virtual instruments |
| **MATLAB / Simulink** | Matrix math engine, signal processing toolbox, block-diagram system modeling, scripting automation, control systems analysis |
| **ANSYS** | Finite Element Analysis (FEA), electromagnetic field simulation, thermal analysis, RF/microwave tools, 3D visualization |

This is a **long-term, large-scale engineering project** requiring specialized knowledge in circuit theory, numerical methods, computer graphics, and systems architecture.

---

## 2. Technology Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **UI** | C# .NET 8 + WPF | Main desktop application shell, canvas, panels |
| **Rendering** | Direct2D / OpenGL | High-performance 2D/3D schematic and field visualization |
| **Analog Engine** | ngspice (C/C++ via P/Invoke) | SPICE-based analog circuit simulation |
| **Digital Engine** | Custom C++ event-driven solver | Digital logic and mixed-signal co-simulation |
| **FEA Engine** | Custom C++ FEM solver or OpenFOAM | Electromagnetic field, thermal, structural analysis |
| **Math Engine** | OpenBLAS / LAPACK (native) | Matrix operations, FFT, signal processing |
| **Scripting** | Embedded CPython or LuaJIT | User automation, scripting toolbox |
| **MCU Simulation** | Custom instruction-set simulators | ARM Cortex-M, AVR, PIC virtual microcontrollers |
| **File I/O** | XML/JSON (projects), Gerber, STEP, KiCad | Interop and manufacturing export |
| **MVVM Framework** | CommunityToolkit.Mvvm 8.3.2 | UI data binding and command pattern |
| **Waveform Plot** | OxyPlot.Wpf 2.1.2 | Oscilloscope-style waveform visualization |

---

## 3. Core Architecture

```
electronicssimulatorproject/
├── EdaSimulator.sln                     ← .NET solution (links all projects)
├── src/
│   ├── Frontend/
│   │   └── EdaSimulator.UI/             ← WPF Application (UI shell, canvas, panels)
│   │       ├── App.xaml / App.xaml.cs
│   │       ├── MainWindow.xaml / .cs    ← ⏳ Empty — next to be built
│   │       └── EdaSimulator.UI.csproj
│   ├── Engines/
│   │   └── EdaSimulator.Engines/        ← C# Engine interop layer
│   │       └── Core/
│   │           ├── Pin.cs               ✅ Done + audited
│   │           ├── Net.cs               ✅ Done + audited
│   │           ├── Component.cs         ✅ Done + audited
│   │           ├── Schematic.cs         ✅ Done + audited
│   │           └── Components/          ⏳ Next — Resistor, Capacitor, etc.
│   ├── NativeEngines/                   ⏳ Phase 3 — C/C++ simulation kernels
│   └── Scripting/                       ⏳ Phase 5 — Python scripting integration
├── docs/
│   ├── ROADMAP.md                       ✅ 8-phase plan
│   └── ARCHITECTURE.md                  ✅ Full system diagram
└── resources/
    └── components/                      ⏳ Phase 2 — component libraries
```

---

## 4. Key Feature Areas

### Circuit & Electronics (Proteus-level)
- SPICE analog simulation (ngspice backend)
- Event-driven digital logic simulation
- Mixed-signal co-simulation
- Microcontroller simulation (ARM, AVR, PIC)
- Virtual instruments (Oscilloscope, Logic Analyzer, Multimeter, Signal Generator)
- PCB Layout editor with DRC and Gerber export

### Mathematical Toolbox (MATLAB-level)
- Matrix computation (BLAS/LAPACK)
- Signal Processing: FFT, IIR/FIR filters, PSD
- Control Systems: Bode plots, Root Locus, State Space, PID tuning
- Simulink-style block diagram modeling
- Script editor (Python-like language)

### Physics Simulation (ANSYS-level)
- Electromagnetic FEM simulation
- Thermal analysis (steady state + transient)
- RF/Microwave: S-parameters, transmission lines
- 3D field visualization

---

## 5. Current Stage

🟡 **Phase 1: Foundation — IN PROGRESS (Day 1 Complete)**

### ✅ Completed — Session 2026-04-14

| Item | Status |
|------|--------|
| Project documentation (README, ROADMAP, ARCHITECTURE, CONTRIBUTING, LICENSE, .gitignore) | ✅ Done |
| `.NET 8 SDK` installed via `winget` | ✅ Done |
| `EdaSimulator.sln` solution scaffolded | ✅ Done |
| `EdaSimulator.UI` (WPF) project created | ✅ Done |
| `EdaSimulator.Engines` (Class Library) created | ✅ Done |
| `Pin.cs` — electrical terminal model | ✅ Done + audited |
| `Net.cs` — wire/junction graph model | ✅ Done + audited |
| `Component.cs` — abstract SPICE base | ✅ Done + audited |
| `Schematic.cs` — master circuit graph | ✅ Done + audited |
| Industry-standard bug audit (6 critical/medium bugs fixed) | ✅ Done |
| NuGet: `CommunityToolkit.Mvvm 8.3.2` | ✅ Installed |
| NuGet: `OxyPlot.Wpf 2.1.2` | ✅ Installed |
| `App.xaml.cs` global crash handler | ✅ Done |
| All changes committed to Git | ✅ Done |

### 🔜 Next Session — Pick Up Here

1. Create `src/Engines/EdaSimulator.Engines/Core/Components/` subfolder
2. Implement `Resistor.cs`, `Capacitor.cs`, `Inductor.cs`, `VoltageSource.cs`
3. Implement `SpiceNetlistExporter.cs` — generates a `.cir` netlist file
4. Create `MainViewModel.cs` wired to `Schematic` via MVVM
5. Build `MainWindow.xaml` 3-panel IDE layout (Toolbox | Canvas | Properties)

---

## 6. Bugs Fixed Today (2026-04-14)

| # | File | Severity | Bug | Fix Applied |
|---|------|----------|-----|-------------|
| 1 | `Net.cs` | 🔴 Critical | `Name` was publicly mutable — renaming ground to anything broke SPICE | Made ground name immutable with `InvalidOperationException` guard |
| 2 | `Schematic.cs` | 🔴 Critical | `CreateNet("0")` allowed — creates duplicate ground, SPICE matrix failure | Added `ArgumentException` guard rejecting `"0"` as a new net name |
| 3 | `Schematic.cs` | 🔴 Critical | No `RemoveNet()` — deleting a net left dangling pin references | Added `RemoveNet()` with full pin cleanup loop |
| 4 | `App.xaml.cs` | 🔴 Critical | No global exception handler — unhandled errors silently kill the app | Added `DispatcherUnhandledException` with user-facing `MessageBox` |
| 5 | `Pin.cs` | 🟡 Medium | `SpiceNodeSequence` accepted `0` or negative values — invalid SPICE output | Added `ArgumentOutOfRangeException` guard: must be ≥ 1 |
| 6 | `Component.cs` | 🟡 Medium | `Designator` setter accepted empty string — invalid SPICE element line | Added backing field `_designator` with whitespace validation |

---

## 7. Change Log
*(Format: [Date] - [Model] - [Changes])*

- **2026-04-14** — Initial creation — Scaffolded `README.md` and folder structure (`src`, `docs`, `resources`).
- **2026-04-14** — Gemini 3.1 Pro — Added `docs/ROADMAP.md` (5 phases, later expanded to 8). Added `AI_CONTEXT.md` for AI model context switching.
- **2026-04-14** — Claude Sonnet — Elevated scope to Proteus / MATLAB / ANSYS professional level. Rewrote `README.md`, `AI_CONTEXT.md`, `ROADMAP.md`. Added `CONTRIBUTING.md`, `LICENSE`, `.gitignore`, `CHANGELOG.md`, `docs/ARCHITECTURE.md`.
- **2026-04-14** — Gemini 3.1 Pro — Installed .NET 8 SDK via `winget`. Scaffolded `EdaSimulator.sln`, `EdaSimulator.UI` (WPF), `EdaSimulator.Engines` (Class Library). Linked projects. Confirmed 0 errors / 0 warnings build.
- **2026-04-14** — Gemini 3.1 Pro — Implemented core domain models: `Pin.cs`, `Net.cs`, `Component.cs`, `Schematic.cs`. Fixed state sync bug in Pin/Net graph disconnection logic.
- **2026-04-14** — Claude Sonnet — Full industry-standard audit. Fixed 6 critical/medium bugs (see table above). Added `Validate()`, `RemoveNet()`, `IsFloating`, `GetPinsInSpiceOrder()`, `Title`, ground immutability guard, SPICE sequence validation, global WPF crash handler. Installed `CommunityToolkit.Mvvm 8.3.2` and `OxyPlot.Wpf 2.1.2`. Updated `CHANGELOG.md` and `AI_CONTEXT.md`. Final end-of-day commit made. Build: **0 Errors / 0 Warnings** ✅
