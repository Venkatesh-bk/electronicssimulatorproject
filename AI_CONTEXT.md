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

---

## 3. Core Architecture

```
electronicssimulatorproject/
├── src/
│   ├── Frontend/           # C# WPF Application (UI shell, canvas, tools)
│   ├── Engines/            # C# interop bindings to native simulation backends
│   ├── NativeEngines/      # C/C++ high-perf simulation kernels
│   └── Scripting/          # Python/Lua scripting engine integration
├── docs/                   # Architecture docs, API reference, roadmap
└── resources/              # Component libraries, PCB footprints, symbols
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

🟡 **Phase 1: Foundation — IN PROGRESS**

The basic C# .NET 8 solution and WPF/Engine projects have been scaffolded. 
Next immediate steps:
1. Define the core C# POCO models: `Component.cs`, `Pin.cs`, `Net.cs`, `Schematic.cs` in the `EdaSimulator.Engines` project.
2. Hook up the basic Main Window layout in `MainWindow.xaml` (adding a Toolbox pane, Canvas pane, and Properties pane).

---

## 6. Change Log
*(Format: [Date] - [Model] - [Changes])*

- **2026-04-14** - Initial creation - Scaffolded basic `README.md` and folder structure (`src`, `docs`, `resources`).
- **2026-04-14** - Gemini 3.1 Pro - Added `docs/ROADMAP.md` detailing the 5 development phases.
- **2026-04-14** - Gemini 3.1 Pro - Created `AI_CONTEXT.md` to seamlessly preserve context across different AI models.
- **2026-04-14** - Claude Sonnet - Upgraded project scope to Proteus Professional / MATLAB / ANSYS level. Updated `README.md`, `AI_CONTEXT.md`, and `docs/ROADMAP.md` to reflect the new professional-grade vision.
- **2026-04-14** - Gemini 3.1 Pro - Installed .NET 8 SDK and scaffolded `EdaSimulator.sln`, `EdaSimulator.UI` (WPF), and `EdaSimulator.Engines` projects.
- **2026-04-14** - Gemini 3.1 Pro - Implemented exact industry-standard C# Core Domain Models (Component, Pin, Net, Schematic) inside Engine Library.
- **2026-04-14** - Gemini 3.1 Pro - Fixed state synchronization logic bug preventing `Net` class from detecting when a `Pin` disconnected.
