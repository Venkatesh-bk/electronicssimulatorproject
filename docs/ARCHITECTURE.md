# System Architecture

> This document describes how all major subsystems of the EDA Simulator Platform connect and interact.

---

## High-Level Architecture Diagram

```
┌──────────────────────────────────────────────────────────────────────┐
│                        EDA Simulator Platform                        │
│                        (Windows Desktop App)                         │
│                                                                      │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │                    WPF Frontend (C# .NET 8)                  │    │
│  │                                                              │    │
│  │  ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌────────────┐  │    │
│  │  │ Schematic │ │   PCB     │ │ Waveform  │ │  Script    │  │    │
│  │  │  Canvas   │ │  Layout  │ │  Viewer   │ │  Editor    │  │    │
│  │  └───────────┘ └───────────┘ └───────────┘ └────────────┘  │    │
│  │  ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌────────────┐  │    │
│  │  │  Toolbox  │ │ Component │ │Properties │ │  Simulation│  │    │
│  │  │  Panel    │ │ Library   │ │  Panel    │ │  Control   │  │    │
│  │  └───────────┘ └───────────┘ └───────────┘ └────────────┘  │    │
│  └─────────────────────────┬───────────────────────────────────┘    │
│                             │  C# Engine API                         │
│  ┌──────────────────────────▼───────────────────────────────────┐   │
│  │                  Engine Orchestrator (C#)                     │   │
│  │          EdaSimulator.Engines (Class Library)                 │   │
│  │                                                               │   │
│  │   Domain Models: Component, Pin, Net, Schematic, PCBBoard,   │   │
│  │                  SimulationResult, Waveform, FEAMesh          │   │
│  │                                                               │   │
│  │   Interop Bridges: P/Invoke wrappers to native engines        │   │
│  └──────┬────────────┬────────────┬─────────────┬───────────────┘   │
│         │            │            │             │                    │
│    P/Invoke     P/Invoke     P/Invoke      CPython                   │
│         │            │            │             │                    │
│  ┌──────▼──┐  ┌──────▼──┐  ┌─────▼──────┐ ┌───▼──────────────┐    │
│  │ ngspice │  │ Digital │  │  FEM/FEA   │ │ Python Scripting │    │
│  │  Engine │  │  Engine │  │   Engine   │ │ Engine (CPython) │    │
│  │ (C/C++) │  │ (C/C++) │  │  (C/C++)   │ │                  │    │
│  └─────────┘  └─────────┘  └────────────┘ └──────────────────┘    │
│                                                                      │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │           Math Engine (OpenBLAS / LAPACK native)              │  │
│  │      FFT · IIR/FIR Filters · Matrix Ops · PSD · Bode         │  │
│  └───────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │                    MCU Simulator Layer                         │  │
│  │         ARM Cortex-M · AVR (ATmega) · PIC (16/18)            │  │
│  │     Loads .hex/.elf firmware · Co-simulates with circuit      │  │
│  └───────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Module Breakdown

### 1. WPF Frontend (`src/Frontend/EdaSimulator.UI`)
- **Technology:** C# .NET 8, WPF, Direct2D rendering for canvas
- **Responsibility:** All user interaction, visual rendering, layout management
- **Key Components:**
  - `SchematicCanvas` — infinite zoomable/pannable 2D drawing surface
  - `PCBCanvas` — PCB layout editor with layer management
  - `WaveformViewer` — oscilloscope-style plot panel (OxyPlot-based)
  - `ComponentLibraryPanel` — searchable, categorized component browser
  - `ScriptEditor` — Python scripting with syntax highlighting (AvalonEdit)
  - `SimulationControlPanel` — run, stop, configure simulation parameters
  - `PropertiesPanel` — dynamically shows selected element's properties

### 2. Engine Orchestrator (`src/Engines/EdaSimulator.Engines`)
- **Technology:** C# .NET 8 Class Library
- **Responsibility:** Bridges UI to native engines; owns all domain models
- **Key Namespaces:**
  - `EdaSimulator.Engines.Core` — domain models (Component, Net, Pin, Schematic)
  - `EdaSimulator.Engines.Analog` — ngspice interop and SPICE netlist builder
  - `EdaSimulator.Engines.Digital` — digital engine interop
  - `EdaSimulator.Engines.FEA` — FEM engine interop and mesh data structures
  - `EdaSimulator.Engines.Math` — math engine bindings
  - `EdaSimulator.Engines.MCU` — MCU simulator bindings

### 3. Analog Engine (`src/NativeEngines/Analog/`)
- **Technology:** C/C++ — integration of **ngspice** library
- **Responsibility:** SPICE simulation (DC, AC, Transient, Noise, FFT)
- **Interface:** Exported C API called via P/Invoke from C#

### 4. Digital Engine (`src/NativeEngines/Digital/`)
- **Technology:** Custom C++ event-driven scheduler
- **Responsibility:** Logic gate and mixed-signal simulation
- **Interface:** Exported C API called via P/Invoke from C#

### 5. FEA Engine (`src/NativeEngines/FEA/`)
- **Technology:** Custom C++ FEM solver (or OpenFOAM integration)
- **Responsibility:** Electromagnetic, thermal, and structural analysis
- **Interface:** Exported C API called via P/Invoke from C#

### 6. Math Engine (`src/NativeEngines/Math/`)
- **Technology:** OpenBLAS + LAPACK, custom FFT/filter implementations in C
- **Responsibility:** Matrix operations, signal processing, Bode math
- **Interface:** Exported C API called via P/Invoke from C#

### 7. MCU Simulator (`src/NativeEngines/MCU/`)
- **Technology:** Custom C++ instruction-set simulators per architecture
- **Responsibility:** Execute `.hex`/`.elf` firmware against a simulated circuit
- **Supported Targets:** ARM Cortex-M0/M3/M4, AVR ATmega, PIC16/PIC18

### 8. Python Scripting Engine (`src/Scripting/`)
- **Technology:** Embedded CPython (Python 3.10+)
- **Responsibility:** User automation, custom analysis scripts, toolbox extensions
- **Interface:** C# calls Python via `Python.NET` (Pythonnet)

---

## Data Flow: Analog Simulation

```
User sets up schematic  →  SchematicCanvas (WPF)
    ↓
Netlist Generator (C#)  →  SPICE netlist (.net file)
    ↓
ngspice Engine (C++)    →  Runs simulation
    ↓
SimulationResult (C#)   →  Parsed waveform data
    ↓
WaveformViewer (WPF)    →  Interactive waveform display
```

## Data Flow: MCU Co-Simulation

```
User loads .hex firmware + draws circuit →  SchematicCanvas (WPF)
    ↓
MCU Simulator (C++)  ←→  Digital/Analog Engines (C++)
    ↓
Real-time pin state updates  →  Circuit simulation responds
    ↓
Virtual Instruments (WPF)  →  Display results in real time
```

---

## Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| UI Framework | WPF (.NET 8) | Best Windows desktop ecosystem; mature, hardware-accelerated |
| Canvas Rendering | Direct2D | GPU-accelerated 2D; handles thousands of components without lag |
| Engine IPC | P/Invoke (same-process DLLs) | Zero-copy data sharing; lowest latency |
| Analog Solver | ngspice | Industry-standard SPICE; mature, SPICE3f5 compatible |
| Math Library | OpenBLAS | Best open-source BLAS implementation; vectorized for SSE/AVX |
| Scripting | CPython via Python.NET | Largest ecosystem; most familiar to engineers |
| File Format | XML (project) + standard exports | Human-readable projects; interop via Gerber, STEP, KiCad |
