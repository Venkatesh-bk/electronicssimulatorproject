# Contributing to EDA Simulator Platform

Thank you for considering contributing to this project! Please read this guide before submitting code.

---

## 🧱 Project Architecture First

Before writing any code, read the following documents:
1. [`AI_CONTEXT.md`](AI_CONTEXT.md) — Current project state and change log
2. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — System architecture and module responsibilities
3. [`docs/ROADMAP.md`](docs/ROADMAP.md) — Which phase we are in and what is next

---

## 📁 Where Does Code Go?

| What you are building | Where it goes |
|-----------------------|--------------|
| UI panels, windows, controls | `src/Frontend/EdaSimulator.UI/` |
| C# domain models and engine wrappers | `src/Engines/EdaSimulator.Engines/` |
| C/C++ native simulation engines | `src/NativeEngines/` |
| Python scripting integration | `src/Scripting/` |
| Component/footprint/symbol data | `resources/` |
| Documentation | `docs/` |

---

## 🔀 Git Branching Strategy

- `main` — Stable, tested, working code only
- `dev` — Active development integration branch
- `feature/<name>` — Individual feature branches (branch from `dev`)
- `fix/<name>` — Bug fix branches (branch from `dev`)
- `phase<N>/<feature>` — Phase-specific branches

Example: `phase1/core-domain-models`, `phase2/schematic-canvas`, `phase3/ngspice-integration`

---

## ✅ Code Standards

### C# (.NET 8)
- Follow [Microsoft C# coding conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use `PascalCase` for classes/methods, `camelCase` for local variables, `_camelCase` for private fields
- All public members must have XML documentation comments (`/// <summary>`)
- Use `async/await` for all long-running simulation calls — never block the UI thread

### C/C++ (Native Engines)
- C API must be exported with `extern "C"` for stable P/Invoke interop
- Use CMake for build configuration
- Memory management: all heap allocations from native code **must** have a corresponding export for freeing that memory
- Target: C++17 minimum

### Python (Scripting Engine)
- PEP8 standards
- All public API functions must have docstrings

---

## 🧪 Testing Requirements

- All new C# domain models must have unit tests in the `EdaSimulator.Tests` project
- All new simulation engine integrations must have integration tests with known-good reference circuits
- UI changes should be manually verified and noted in the PR description

---

## 📋 Pull Request Checklist

Before opening a PR, confirm:
- [ ] Code builds with `dotnet build` (no errors or warnings)
- [ ] New code has unit/integration tests
- [ ] `AI_CONTEXT.md` change log is updated with what was done
- [ ] `CHANGELOG.md` is updated under `[Unreleased]`
- [ ] PR description explains what was changed and why
- [ ] No hardcoded paths or secret keys in code
