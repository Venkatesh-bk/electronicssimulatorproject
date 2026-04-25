# Changelog

All notable changes to the EDA Simulator Platform will be documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased] — Phase 1 Integration

### Added (Session 2026-04-25)
- Core discrete components: `Resistor.cs`, `Capacitor.cs`, `Inductor.cs`, `VoltageSource.cs`, `CurrentSource.cs`
- `SpiceNetlistExporter.cs` — converts `Schematic` to `.cir` files
- `MainViewModel.cs` — MVVM wiring core connected to schematic
- `MainWindow.xaml` — 3-panel professional dark UI (Toolbox | Canvas | Properties) layout

### Changed (Session 2026-04-25)
- Reorganized `EdaSimulator.Engines` to feature `Models/` and `Simulation/` directories for strict boundary control mechanism.
- Reorganized `EdaSimulator.UI` with canonical MVVM `ViewModels/` and `Views/` folders. Update namespaces globally.

### Fixed (Session 2026-04-25)
- **Critical:** Disconnected pins returned "NC" as their net, inadvertently short-circuiting all floating pins in the schematic together. Now returns dynamically unique ids (`NC_uuid`).
- **Critical:** `Schematic.CreateNet` allowed creating independent nets sharing the exact same string name, artificially shorting them together in SPICE output.
- **High:** `Schematic.AddComponent` allowed injecting duplicate designators (e.g., two "R1"s), crashing SPICE matrix syntax.
- **High:** `Component.Designator`, `Component.Value`, and `Net.Name` had no protection against whitespace insertion. Spaces would artificially break SPICE token limit arrays.

---

## [0.2.0] — 2026-04-14 — Phase 1: Core Engine & Industry Hardening

### Added
- **`Pin.cs`** — Electrical terminal model with SPICE sequence validation, `IsFloating` property
- **`Net.cs`** — Wire/junction model with O(1) HashSet tracking, ground immutability guard
- **`Component.cs`** — Abstract base with enforced SPICE contract (`GenerateSpiceNetlistLine`), `GetPinsInSpiceOrder()`
- **`Schematic.cs`** — Master circuit graph with `Validate()`, `RemoveNet()`, `Title`, ground net protection
- **`App.xaml.cs`** — Global `DispatcherUnhandledException` handler preventing silent crashes
- NuGet packages: `CommunityToolkit.Mvvm 8.3.2`, `OxyPlot.Wpf 2.1.2`
- `docs/ARCHITECTURE.md` — Full system architecture diagram and module breakdown
- `CONTRIBUTING.md` — Branch strategy, code standards, PR checklist
- `LICENSE` — MIT License
- `.gitignore` — .NET 8 + C++ + Python + CMake patterns

### Fixed
- **Critical:** `Net.Name` was publicly mutable — renaming the ground net `"0"` would break all SPICE simulations
- **Critical:** `CreateNet("0")` was allowed, potentially creating a duplicate ground
- **Critical:** No `RemoveNet()` — deleting a net left dangling `ConnectedNetId` references on pins
- **Critical:** No global unhandled exception handler in WPF — errors would silently terminate the app
- **Medium:** `Pin.SpiceNodeSequence` accepted `0` or negative values, producing invalid SPICE netlist lines
- **Medium:** `Designator` setter had no validation, allowing empty strings in netlist output

### Changed
- Project scope elevated from basic EDA to **Proteus Professional + MATLAB/Simulink + ANSYS** level
- Roadmap expanded from 5 to 8 phases
- `Pin.Disconnect()` made `internal` — disconnection must go through `Schematic` to preserve graph integrity

---

## [0.1.0] — 2026-04-14 — Project Initialization

### Added
- `README.md` — Professional project overview, prerequisites, AI assistant note
- `AI_CONTEXT.md` — AI model context-switching document with change log
- `docs/ROADMAP.md` — 8-phase detailed development roadmap (Proteus/MATLAB/ANSYS scope)
- Folder structure: `src/Frontend/`, `src/Engines/`, `src/NativeEngines/`, `src/Scripting/`, `docs/`, `resources/`
- `.NET 8 SDK` installed via `winget`
- `EdaSimulator.sln` scaffolded with two projects linked

---

## [0.1.0] — 2026-04-14 — Project Initialization

### Added
- `README.md` — Project overview and prerequisites
- `AI_CONTEXT.md` — AI model context switching document with change log
- `docs/ROADMAP.md` — 8-phase detailed development roadmap
- `docs/ARCHITECTURE.md` — Full system architecture with module breakdown and data flow diagrams
- `docs/CONTRIBUTING.md` — Contribution guidelines
- `.gitignore` — .NET 8 + C++ + Python ignore rules
- `LICENSE` — MIT License
- Folder structure: `src/Frontend/`, `src/Engines/`, `src/NativeEngines/`, `src/Scripting/`, `docs/`, `resources/components/`

### Changed
- Project scope elevated from basic EDA tool to **Proteus Professional + MATLAB/Simulink + ANSYS** level professional engineering simulation suite.
