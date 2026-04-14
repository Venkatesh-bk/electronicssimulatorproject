# Changelog

All notable changes to the EDA Simulator Platform will be documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased] — Phase 1 In Progress

### Planned
- .NET 8 solution scaffold (`EdaSimulator.sln`)
- WPF application project (`EdaSimulator.UI`)
- Engine class library project (`EdaSimulator.Engines`)
- Core domain models: `Component`, `Pin`, `Net`, `Schematic`
- Basic WPF main window (toolbox, canvas, properties panel)
- Project save/load (XML format)

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
