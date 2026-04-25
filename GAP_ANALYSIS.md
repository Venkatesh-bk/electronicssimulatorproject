# EDA Simulator — Gap Analysis
**Date:** 2026-04-25 | Deep research session

---

## Project vs Industry Tools

Your project is 100% original code (not a fork). It currently sits at ~40% of Proteus-level capability.
Below is the honest breakdown of every gap and how to close it.

---

## GAP 1 — Internal Math Solver
**vs:** All professional tools  
**Verdict:** ngspice is the correct permanent choice. Building your own MNA+Newton-Raphson solver is PhD-level work (30+ years for HSPICE). **No action needed.**

---

## GAP 2 — Component Model Accuracy
**vs:** Proteus, LTspice, HSPICE  
**Current:** Only ideal `Resistor` and `VoltageSource` components.  
**Missing:** Diodes (Shockley equation, 14 params), BJTs (Gummel-Poon, 40+ params), MOSFETs (BSIM4, 300+ params), Capacitors (ESR, ESL), Inductors (saturation), LEDs, Op-Amps.

**Solution (Phase 4):** Import KiCad SPICE Library `.lib` files. Build a `SpiceLibParser.cs` and library browser UI. Pass `.model` / `.subckt` blocks through to ngspice. No custom math needed.

**Free resource:** `github.com/kicad-spice-library/KiCad-Spice-Library`

---

## GAP 3 — MCU Co-Simulation
**vs:** Proteus VSM (unique to Proteus)  
**Current:** No microcontroller simulation.  
**Missing:** Running actual `.hex` firmware (Arduino, PIC, ARM) synchronized with the analog circuit. Every GPIO pin state change propagates to the SPICE network and vice versa.

**Solution (Phase 8):** Bridge `simavr` (AVR emulator, GitHub C library) with ngspice via P/Invoke. A `CoSimulationService` synchronizes both at 100ns steps.

**Free resources:**
- `github.com/buserror/simavr` (AVR — Arduino Uno/Mega)
- `renode.io` (ARM Cortex-M, written in C#)

**Difficulty:** ⭐⭐⭐⭐ Hard — requires C P/Invoke wrapper and time-step synchronization logic.

---

## GAP 4 — Analysis Depth
**vs:** MATLAB/Simulink, ANSYS

### Achievable with ngspice (all free):
| Analysis | ngspice Directive | What It Shows |
|----------|-----------------|--------------|
| AC Sweep | `.ac dec 100 1Hz 10MHz` | Frequency response, Bode plots |
| Noise | `.noise v(out) vin 100` | Signal-to-noise ratio, spectral density |
| DC Sweep | `.dc V1 0 5 0.1` | I-V curves, transistor characteristics |

### Achievable with Math.NET Numerics NuGet:
| Analysis | Method | What It Shows |
|----------|--------|--------------|
| Parametric Sweep | Loop SimulateAsync, vary component values | Effect of component tolerances |
| Monte Carlo | `Normal.Sample()` random variation ×1000 runs | Statistical distribution of outputs |

### NOT achievable (ANSYS domain — different physics):
- Electromagnetic field simulation (FEM, Maxwell's equations in 3D space)
- PCB signal integrity at GHz frequencies
- Antenna radiation patterns

**Solution (Phase 5):** Add AC/DC/Noise modes to `SimulationConfiguration` UI, extend `RawFileParser` for frequency-domain data, add `LogarithmicAxis` to `OscilloscopeViewModel` for Bode plots.

---

## GAP 5 — PCB Layout
**vs:** Altium Designer, KiCad  
**Current:** Schematic only — no physical board layout.  
**Missing:** Component footprints, copper trace routing, design rule checking on PCB geometry, Gerber export for manufacturing.

**Solution (Phase 9):**
- `FreeRouting` Java CLI (open-source autorouter, GitHub) — accepts Specctra `.dsn`, outputs `.ses`
- `GerberWriter` NuGet (`github.com/macaba/GerberWriter`) — generates RS-274X Gerber files from C#

**Difficulty:** ⭐⭐⭐ Medium-Hard — significant new UI (PCB canvas), but most algorithms are delegated to existing tools.

---

## GAP 6 — Professional UX & Workflow
**vs:** All tools  
**Current:** No save/load, no project files, no export, single flat canvas.

| Missing Feature | Solution | NuGet/Library |
|----------------|---------|---------------|
| Save/Load project | `.edaproj` JSON format | `System.Text.Json` (built-in) |
| PDF export | Render canvas to PDF | `PdfSharp` NuGet |
| PNG/SVG export | WPF `RenderTargetBitmap` | Built-in WPF |
| BOM generation | Scan components → CSV | Built-in |
| Net labels / power symbols | `NetLabelItemViewModel` | Custom |
| Hierarchical / multi-sheet | Multiple `SchematicViewModel` instances | Custom |
| Component search library | `TreeView` sidebar with fuzzy search | Built-in WPF |
| Cross-probing | Trace click → highlight net on canvas | Custom event |

**Solution (Phase 6–7):** All achievable with standard .NET software engineering. No specialist needed.

---

## Realistic Progress Targets

```
Now (Phase 3.5):     ████████░░░░░░░░░░░░  ~40% of Proteus
After Phase 6:       ████████████░░░░░░░░  ~60% (matches LTspice + KiCad)
After Phase 8:       ████████████████░░░░  ~80% (Proteus-level for analog+MCU)
After Phase 9:       ██████████████████░░  ~90% (KiCad-level schematic+PCB)
After Phase 10:      ████████████████████  Distributable professional product
```

ANSYS HFSS (EM simulation) is a completely separate engineering domain and is explicitly out of scope — even well-funded teams spend years on it.
