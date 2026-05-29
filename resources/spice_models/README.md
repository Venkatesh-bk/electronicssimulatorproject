# EdaSimulator — SPICE Model Library

Standard SPICE model files (`.lib`, `.mod`, `.sub`) for vendor component models.

> This directory is populated by running `scripts\DownloadVendorLibraries.py`.

## Structure (after running setup)

```
resources\spice_models\
  ├── analog\          # Analog Devices / TI op-amp and linear models
  ├── discrete\        # Transistors, diodes, FETs
  ├── passives\        # RLC parasitics
  └── vendor\          # Manufacturer-provided subcircuit models
```

## Supported SPICE Formats

- **SPICE3** `.lib` — Standard Berkeley SPICE model format
- **HSPICE** `.sp` — Synopsys compatible subcircuits
- **Ngspice** `.mod` — Native ngspice model files

## Adding Custom Models

Place `.lib` or `.mod` files here, then reference them in the simulator:
1. Open a schematic
2. Go to **Simulation → Include Library...**
3. Select your `.lib` file — it will be auto-added as a `.include` in the netlist

## Downloading Free SPICE Libraries

```powershell
# Auto-downloads common free model libraries
python scripts\DownloadVendorLibraries.py
```

Sources include: TI SPICE models, ON Semiconductor, Vishay, STMicroelectronics.
