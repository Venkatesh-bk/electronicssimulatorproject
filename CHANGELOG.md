# EDA Simulator — Changelog

---

## [Research & Planning Session] — 2026-04-25

### Gap Analysis vs Industry Tools
- Deep research completed comparing the project against **Proteus VSM, MATLAB/Simulink, Altium Designer, ANSYS HFSS**
- **6 critical gaps** identified and documented
- Full **10-phase extended roadmap (Phases 4–10)** created to close all gaps using free open-source tools

### Identified Gaps Summary
| Gap | Issue | Planned Phase |
|-----|-------|--------------|
| GAP 1 | No internal MNA/NR solver (delegates to ngspice — correct) | Keep as-is |
| GAP 2 | Only ideal R and V components | Phase 4 |
| GAP 3 | No MCU co-simulation (Proteus unique feature) | Phase 8 |
| GAP 4 | No AC sweep, Bode, Monte Carlo, Noise | Phase 5 |
| GAP 5 | No PCB layout module | Phase 9 |
| GAP 6 | No save/load, BOM, hierarchical schematics | Phase 6–7 |

### Open-Source Resources Catalogued for Future Phases
- `KiCad-Spice-Library` (GitHub) — free SPICE `.lib` models (transistors, diodes, op-amps)
- `SpiceSharp` (NuGet) — full SPICE engine in pure C#
- `Math.NET Numerics` (NuGet) — statistical distributions for Monte Carlo analysis
- `simavr` (GitHub C library) — AVR instruction-accurate simulation for MCU co-sim
- `Renode` (C# framework, antmicro) — ARM Cortex-M board emulation
- `FreeRouting` (Java CLI, GitHub) — open-source PCB autorouter via Specctra DSN/SES
- `GerberWriter` (NuGet) — RS-274X Gerber PCB file generation in C#
- `PdfSharp` (NuGet) — PDF export from WPF canvas
- OxyPlot `LogarithmicAxis` — already in package, ready for Bode plots

---

## [Phase 3.5 — Steps 1-3] — 2026-04-25 | Commit: `fb7d573`

### Added
- **Current Probe Tool** (`Key.I`): Drop gold `[A]` arrow probes onto component bodies to track branch currents.
- **`CurrentProbeItemViewModel`** + **`CurrentProbeTool.cs`**: Full MVVM canvas item and tool for current measurement.
- **Gold Arrow XAML DataTemplate**: Renders on canvas with device name label, visually distinct from voltage probes.
- **MATLAB-style Crosshair Tracker**: `TrackerFormatString` on every `LineSeries` — hover snaps crosshair to exact `<Time, Value>`.
- **OxyPlot Legend Overlay**: Semi-transparent legend (top-right) groups all active traces by name.
- **`.options savecurrents`**: Injected into every generated `.cir` netlist — forces ngspice to retain branch current vectors in `.raw` output.

---

## [Phase 3 — Complete] — 2026-04-25 | Commit: `fb7d573`

### Added
- **`SimulationConfiguration.cs`**: Physics analysis config model (`.tran`, `.op` parameters).
- **`SpiceNetlistExporter`** updated: appends simulation directives from `SimConfig`.
- **`SimulationConfigViewModel.cs`**: MVVM bridge exposing simulation parameters to the Properties panel.
- **`OscilloscopeWindow.xaml` + `.xaml.cs`**: Dark-themed oscilloscope popup. Hides on close to preserve traces.
- **`OscilloscopeViewModel.cs`**: OxyPlot `PlotModel` — `RenderTrace()`, `ClearTraces()`, MATLAB legend, crosshairs.
- **`SpiceExecutionService.cs`**: Async background ngspice process bridge. Batch mode, ASCII raw, `CancellationToken`.
- **`RawFileParser.cs`**: Parses SPICE ASCII `.raw` → `Dictionary<string, List<double>>` keyed by variable name.
- **`VoltageProbeItemViewModel`** + **`ProbeTool`** (`Key.P`): Blue `[V]` pin probes mapping net IDs → oscilloscope traces.
- **`SimulateAsync` command**: Full pipeline — DRC → netlist → ngspice → RAW parse → probe match → OscilloscopeWindow.
- **Stop Simulation ■** button: Replaces Run ▶ while simulating. Cancels background ngspice process via `CancellationTokenSource`.
- **`IsSimulating` flag**: Prevents concurrent runs; DataTrigger drives Run/Stop button visibility swap.
- **Error Pipeline**: Stdout keyword scanner (`singular matrix`, `timestep too small`, `fatal`, `aborted`) surfaces failures to the Output Log.
- **Live Tuning Toggle**: `DispatcherTimer` polls every 1s, compares netlist hash, auto-triggers `SimulateAsync` on schematic changes.
- **`OxyPlot.Wpf 2.2.0`** NuGet package added.

### Changed
- `MainWindow.xaml`: Properties panel `DataTrigger` shows Simulation Config when no component selected.
- `SchematicViewModel`: `SimConfig` property injected.
- `Schematic.cs`: Exposed `SimulationConfiguration SimConfig` property.

---

## [Phase 2 — Schematic Capture] — Earlier Sessions

### Added
- Full schematic canvas: drag-drop placement, wire routing, selection, move, property editing
- DRC (Design Rule Check) validation — floating pins, missing grounds
- SPICE netlist export (Resistors, Voltage Sources)
- Undo/Redo (`CommandManager`, `MoveItemsCommand`)
- Symbol registry with SVG-style path data per component type
- Zoom and pan on canvas
- 47 runtime tests (all green)

---

## [Phase 1 — Architecture & Domain Models] — Earlier Sessions

### Added
- `.NET 8 / WPF` solution with clean layered architecture
- `EdaSimulator.Engines` class library (domain models)
- `Schematic`, `ComponentBase`, `Net`, `Pin` core models
- Component types: `Resistor`, `VoltageSource`
- `SpiceNetlistExporter` initial implementation
- Graph integrity checks and validation
