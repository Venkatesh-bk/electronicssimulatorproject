# EDA Simulator Platform

> **A professional-grade Electronic Design Automation (EDA), Circuit Simulation, and Engineering Analysis platform for Windows — targeting the capability level of Proteus Professional, MATLAB/Simulink, and ANSYS.**

---

## 🎯 Project Vision

This platform aims to be an all-in-one engineering simulation suite that combines:
- **Schematic Capture & PCB Design** *(like Proteus Professional)*
- **Mathematical Modeling & Signal Processing** *(like MATLAB/Simulink)*
- **Finite Element Analysis (FEA) & Thermal/EM Simulation** *(like ANSYS)*

The goal is a unified, professional Windows desktop tool that allows electrical engineers, PCB designers, and researchers to design, simulate, analyze, and validate complex electronic systems without needing to switch between multiple costly software packages.

---

## 🏗️ Project Structure

| Folder | Contents |
|--------|----------|
| `src/Frontend/` | WPF C# .NET 8 UI — canvas, toolbars, panels, plotting |
| `src/Engines/` | C# interop bindings to native C/C++ simulation backends |
| `src/NativeEngines/` | High-performance C/C++ simulation kernels (SPICE, FEA, digital) |
| `src/Scripting/` | Python/MATLAB-compatible scripting engine and toolboxes |
| `docs/` | Architecture plans, API references, and roadmap |
| `resources/` | Component libraries, footprint databases, symbol packs |

---

## 🔬 Core Simulation Capabilities (Target)

### 1. Circuit & Electronics Simulation *(Proteus-level)*
- Full SPICE analog simulation (ngspice backend)
- Digital logic simulation (event-driven engine)
- Mixed-signal (analog-digital co-simulation)
- Microcontroller simulation (ARM Cortex-M, AVR, PIC families)
- Virtual instruments: Oscilloscope, Logic Analyzer, Signal Generator, Multimeter
- PCB Layout editor with Design Rule Checking (DRC)
- Gerber file export for manufacturing

### 2. Mathematical & Signal Processing Toolbox *(MATLAB-level)*
- Matrix computation engine (BLAS/LAPACK backed)
- Signal Processing: FFT, filtering, PSD analysis
- Control Systems: Bode plots, Root Locus, State Space
- Simulink-style block diagram modeling and simulation
- Symbolic mathematics and equation solving
- Script editor with a Python-like language for automation

### 3. Finite Element & Physics Simulation *(ANSYS-level)*
- Electromagnetic field simulation (FEM-based)
- Thermal analysis: steady-state and transient heat flow
- Structural/mechanical stress analysis
- RF & Microwave: S-parameters, transmission line analysis
- 3D model visualization of simulation results

---

## ⚙️ Technology Stack

| Layer | Technology |
|-------|-----------|
| **UI Framework** | C# .NET 8 + WPF (or Avalonia for cross-platform later) |
| **Rendering** | Direct2D / OpenGL for high-performance canvas |
| **Analog Engine** | ngspice (C/C++ — P/Invoke bindings) |
| **Digital Engine** | Custom event-driven simulator in C++ |
| **FEA Engine** | OpenFOAM or custom C++ FEM solver |
| **Matrix Math** | LAPACK/OpenBLAS (native bindings) |
| **Scripting** | Embedded Python (CPython) or LuaJIT |
| **File Formats** | XML/JSON projects + Gerber, STEP, SPICE netlist export |

---

## 📋 Prerequisites

- **.NET 8 SDK** (for the WPF frontend)
- **Visual Studio 2022** with C++ Desktop Development workload (for native engines)
- **Python 3.10+** (for scripting engine integration)
- **CMake 3.24+** (for building the C++ simulation kernels)
- **Git** with LFS support (for large component libraries)

### AI-Assisted Development

Due to the extreme complexity of this platform — spanning circuit theory, FEM physics, signal processing, advanced rendering, and cross-language interop — AI-assisted development is **essential**. Use **Claude Sonnet**, **Gemini Pro**, or **GitHub Copilot** throughout development.

**Note for AI Assistants:** Always read [AI_CONTEXT.md](AI_CONTEXT.md) before writing any code. Update the change log when you complete a task.

---

## 📖 Documentation

* **User Reference Manual**: [docs/USER_GUIDE.md](docs/USER_GUIDE.md) — Comprehensive guide on canvas controls, wiring, simulation configuration, PCB design rules (DRC), FreeRouting auto-routing, manufacturing export, and Python automation.
* **Architecture Specifications**: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
* **Detailed Development Roadmap**: [docs/ROADMAP.md](docs/ROADMAP.md)
