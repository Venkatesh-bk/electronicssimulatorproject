# EDA Simulator — Changelog

---

## [Design Flaw Analysis & Resolution] — 2026-06-04

### Fixed Critical Simulation Flaws
- **LM358 SPICE Behavioral Model**: Fixed a syntax crash in `eda_components.lib` caused by invalid curly-bracket node references (`{v+-voh}`) and a buggy `Aout` XSpice device declaration. Replaced it with a standard-compliant, rail-clamped Voltage-Controlled Voltage Source (VCVS) limiter using the standard ngspice `limit()` function.
- **Op-Amp Rail Clamping**: Upgraded the remaining op-amp subcircuits (`LM741`, `TL071`, `LM324`, and `NE5532`) to utilize standard-compliant rail-clamping limiter stages, preventing physically unrealistic voltage swings.
- **Microsecond Time Scale Step Sources**: Redesigned the mathematical step source model (`BlockSourceStep` in `SpiceNetlistExporter`) to utilize a strictly monotonically increasing rise time sequence (`steptime` to `steptime + 1e-9`), preventing ngspice non-monotonic PWL step failures at microsecond or smaller time scales.
- **Robust Subcircuit/Model Detection**: Upgraded both `CustomComponent` and `ModelLibraryService` to use regular expressions (`\.subckt\s+`) rather than `StartsWith()` to check for subcircuits, making them robust to leading comments, empty lines, and copyright headers.
- **Time Suffix Parsing**: Enhanced `ParseSpiceTime` to strip the `s` unit suffix (e.g., from `10ms`, `100us`, or `2.5s`) prior to parsing, avoiding scaling calculation fallbacks.
- **Integration Tests**: Added comprehensive physical simulation test coverage for both active filter (Sallen-Key) and block step source simulations.

## [Refinements & Bug Fix Audits] — 2026-06-04
- **AVR / Python Firmware Co-Simulation**: Syncs Arduino `.ino` and Python `.py` logs with SPICE transient time steps.
- **Matrix Dark Serial Monitor**: Neon green console displaying co-simulation outputs.
- **Schematic Text Annotations**: Custom notes exported as SPICE netlist comments (`* NOTE: ...`) with Ctrl+C/Ctrl+V cloning.
- **BOM Pricing Dashboard**: Automated part inventory, cost calculation, and live supplier search linking.
- **WiX Installer Pipeline**: Automated `.msi` native installer creation.

### Five Iterations of Bug Fixing
- **Iteration 1 (VirtualMcuSimulationEngine)**: Enhanced regex patterns to support single/double quotes in serial print matching and protected time steps with a lower safety bound (`0.001s`).
- **Iteration 2 (Clipboard Cloning)**: Added state-preservation logic during copy/paste operations (syncing `WiperPosition` for Potentiometers, `IsClosed` for Switches, and `FirmwarePath` for MCUs).
- **Iteration 3 (Specctra DSN / SES Router)**: Consolidated designator-pad net tokens inside Specctra DSN exports to support spaces/brackets, and upgraded SES regex patterns to parse quoted net names from FreeRouting.
- **Iteration 4 (Math & Signal Processing)**: Guarded the DSP windowing library (`ApplyWindow`) against division-by-zero / NaN when processing array sizes of 1 or less.
- **Iteration 5 (Digital Logic Solver)**: Fixed AND/OR gate three-state logic solvers to evaluate standard IEEE 1364 truth tables correctly (e.g. low overrides undefined on AND gates; high overrides undefined on OR gates).

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
