# EdaSimulator — Simulation Engine Binaries

This directory contains vendored simulation engine binaries that power EdaSimulator's real circuit simulation.

## ngspice (SPICE Simulation Engine)

| Property | Value |
|----------|-------|
| Version  | ngspice-46 |
| Platform | Windows x64 |
| License  | BSD-3-Clause |
| Source   | https://ngspice.sourceforge.io |

### Installed Location

```
resources\engines\ngspice\Spice64\bin\ngspice_con.exe   ← Console batch-mode binary
resources\engines\ngspice\Spice64\bin\ngspice.exe        ← GUI binary (not used)
resources\engines\ngspice\Spice64\bin\libomp140.*.dll    ← OpenMP runtime
resources\engines\ngspice\Spice64\lib\ngspice\           ← XSPICE code models
```

> The `Spice64\` folder is in `.gitignore` — it's large (~25 MB) and reproducible via the setup script.

### First-Time Setup (after cloning)

Run the automated setup script from the project root:

```powershell
.\resources\engines\Setup-NgSpice.ps1
```

This downloads ngspice-46 (~10 MB) from SourceForge CDN and extracts it to the correct location automatically.

### How EdaSimulator Finds ngspice

`NgSpiceLocator.cs` probes these locations in priority order:

1. **User setting** — `Tools → Preferences → Simulation → ngspice Path`
2. **Project vendor** — `resources\engines\ngspice\Spice64\bin\ngspice_con.exe` ← **this folder**
3. **Standard installs** — `C:\Program Files\Spice64\bin\`, `C:\ngspice\bin\`, etc.
4. **Chocolatey / Scoop** — managed package manager paths
5. **System PATH** — any `ngspice_con.exe` on the environment PATH

No manual configuration is needed if you run the setup script.
