# EDA Simulator Platform — User Guide & Reference Manual

Welcome to the **EDA Simulator Platform**, a professional-grade, all-in-one Electronic Design Automation (EDA), Circuit Simulation, and Engineering Analysis suite. This manual provides step-by-step instructions for schematic capture, simulation, oscilloscope analysis, PCB design, manufacturing export, and python-based automation.

---

## 📖 Table of Contents
1. [User Interface Overview](#1-user-interface-overview)
2. [Schematic Capture & Wiring](#2-schematic-capture--wiring)
3. [Running Circuit Simulations](#3-running-circuit-simulations)
4. [Waveform Visualization & Oscilloscope](#4-waveform-visualization--oscilloscope)
5. [MCU Firmware Injection & Co-Simulation](#5-mcu-firmware-injection--co-simulation)
6. [PCB Layout & Design Rules (DRC)](#6-pcb-layout--design-rules-drc)
7. [FreeRouting Auto-Router Integration](#7-freerouting-auto-router-integration)
8. [Manufacturing Exports (Gerber, BOM, Pick-and-Place)](#8-manufacturing-exports-gerber-bom-pick-and-place)
9. [Python Scripting & GPU-Accelerated Math](#9-python-scripting--gpu-accelerated-math)
10. [Keyboard Shortcuts Reference](#10-keyboard-shortcuts-reference)

---

## 1. User Interface Overview

The application features a professional, high-performance dark-themed workspace divided into three main docking panels:

```
+------------------------------------------------------------------------+
|  File   Simulation   View   Help                        [Edition Badge] |
+------------------+----------------------------------+------------------+
|  Toolbar         |                                  |  Properties      |
|  [Probe] [Undo]  |                                  |  &               |
|                  |                                  |  Simulation      |
+------------------+         Schematic Canvas         |  Settings        |
|  Parts Bin       |         (Infinite Grid)          |                  |
|                  |                                  |                  |
|  [Resistor]      |                                  |                  |
|  [Capacitor]     |                                  |                  |
|  [OpAmp]         |                                  |                  |
|                  |                                  |                  |
+------------------+----------------------------------+------------------+
|  Python Console / Output Log Log                     |  DRC Status LED  |
+------------------------------------------------------+------------------+
```

- **Schematic Canvas**: The central workspace where you place components and draw connections. Supports infinite panning (middle-mouse drag) and fluid zooming (mouse scroll).
- **Parts Bin (Left Panel)**: Lists all active and library components categorized by function (Discrete, Analog ICs, Digital Gates, MCU, Power Symbols).
- **Properties & Simulation Panel (Right Panel)**: Displays settings for the active component and configuration inputs for simulation types (Transient, AC Sweep, DC Sweep).
- **Console & Log (Bottom Panel)**: Switch between the live SPICE compiler output log and the interactive Python Console for custom data analysis.
- **Status Bar (Bottom Edge)**: Displays active tool, pointer coordinates, total component count, and a real-time event-driven Design Rule Check (DRC) status indicator.

---

## 2. Schematic Capture & Wiring

### Placing Components
1. Click a category in the **Parts Bin** to expand it.
2. Select a component (e.g., `Resistor`, `Capacitor`, or `OpAmp`).
3. Hover over the **Schematic Canvas**; you will see the component footprint outline following your cursor.
4. Left-click to place the component.
5. Right-click or press `Escape` to cancel placement mode.
6. To move components, drag them with your mouse. Multiple components can be moved at once by clicking and dragging a selection box around them.

### Drawing Connections (Wiring Tool)
1. Press the `W` key or click the **Wire Tool** on the toolbar to activate wiring mode.
2. Hover over a component pin (represented by a small square indicator). The pin highlights when targeted.
3. Left-click on the pin to start drawing a wire.
4. Click anywhere on the grid to create an orthogonal corner anchor, or click another pin directly to complete the path.
5. The connection is validated automatically; the pin indicator changes from **Red (Floating)** to **Green (Connected)**.
6. Press `Escape` at any time to cancel wire routing.

### Editing Component Properties
- Double-click any placed component to open the **Component Property Dialog**.
- Edit the **Designator** (e.g., `R1`, `C1`), **Value** (e.g., `10k`, `100nF`), and custom model parameters.
- Click **Save** to update the design database.

### Adding Net Labels
1. Connect a wire between components.
2. Click the **Label Tool** (`🏷 Label`) on the toolbar.
3. Click directly on the wire segment you wish to name.
4. Type a unique name (e.g., `V_IN`, `V_OUT`) and click OK.
5. A green label badge will appear at the wire's midpoint. Named nets can be directly referenced in simulation scripts.

### Power & Ground Symbols
* Use the **GND** and **VCC/Power** symbols from the Parts Bin to automatically declare global power nets.
* Ground is locked to net name `"0"` to satisfy SPICE requirements.
* Power rails are highlighted with red symbol paths, while Ground paths display in slate gray.

---

## 3. Running Circuit Simulations

The platform embeds a native high-performance **ngspice** core.

### Step-by-Step Simulation Setup
1. Define your circuit on the canvas. Ensure a **Ground (GND)** symbol is placed; circuits without a ground cannot be simulated.
2. Select the simulation profile in the right-hand panel:
   - **Transient (.tran)**: Observes voltage/current over time (Parameters: *Stop Time*, *Step Time*).
   - **AC Sweep (.ac)**: Analyzes frequency response (Parameters: *Start Hz*, *Stop Hz*, *Points/Decade*).
   - **DC Sweep (.dc)**: Sweeps a voltage source to plot I-V curves (Parameters: *Source Name*, *Start Volts*, *Stop Volts*, *Increment*).
3. Place **Voltage Probes** (`Probe`) or **Current Probes** (`I-Probe`) on specific nets or pins.
4. Click **Simulate ▶** (or press `F5`).
5. The live DRC validates the schematic first. If the DRC indicator remains **Green (Passed)**, the SPICE netlist is generated and sent to the simulation compiler.

---

## 4. Waveform Visualization & Oscilloscope

Upon simulation completion, the interactive **Oscilloscope Window** opens:

* **Interactive Zoom**: Drag a rectangle with your mouse to zoom into a region of interest. Scroll wheel zooms vertically/horizontally.
* **Trace Legend**: Toggle individual trace visibility by checking/unchecking their names.
* **Math Channels**: Click "Add Math Channel" to calculate custom formulas like differential voltages (e.g., `V(V_OUT)-V(V_IN)`) or execute a Fast Fourier Transform (`FFT`) to view the frequency spectrum.
* **Export Options**:
  * **Save PNG**: Saves the current scope view as a publication-quality vector image.
  * **Export CSV**: Writes all simulated coordinate points to a CSV spreadsheet for Excel/MATLAB imports.

---

## 5. MCU Firmware Injection & Co-Simulation

For digital and mixed-signal designs:
1. Place a microcontroller component (e.g., `ATmega328P` or `ARM Cortex-M4`) from the Parts Bin.
2. Double-click the MCU component to open its properties.
3. Click the **Browse** button next to **Firmware Path** and select your compiled `.hex`, `.elf`, or `.bin` binary file.
4. When you start the simulation, the custom event-driven MCU instruction-set simulator runs concurrently with ngspice, feeding pin logic states back and forth across the analog-digital boundary.

---

## 6. PCB Layout & Design Rules (DRC)

1. Select **View → PCB Layout** or press `Ctrl+Shift+P` to open the PCB environment.
2. Click **Sync to PCB Layout** to synchronize schematic nets and footprints. Components appear in the PCB viewport boundary.
3. Arrange footprints (e.g., `R_0805`, `DIP-8`) on the copper layers.
4. The board layout is verified automatically against **IPC-2221B design standards**:
   * Minimum trace width: `0.15 mm`
   * Minimum trace clearance: `0.15 mm`
   * Minimum via drill size: `0.2 mm`
   * Edge clearance: `0.5 mm`
5. Violations are flagged in the bottom status panel with exact coordinates.

---

## 7. FreeRouting Auto-Router Integration

To route complex double-sided PCBs automatically:
1. **Download FreeRouting**: Ensure you have the `freerouting.jar` executable downloaded locally.
2. **Configure Jar Path**: Open **Preferences** (`Ctrl+,`), locate the **FreeRouting Path** field under the PCB tab, and click **Browse** to select your JAR file.
3. Once the path is configured, the **PCB Auto-Router LED** turns Green.
4. Click **Auto-Route** in the PCB toolbar.
5. The app will:
   * Export the board layout to a Specctra `.dsn` netlist file.
   * Launch `freerouting.jar` in a background worker process.
   * Calculate path routing using automated heuristics.
   * Import the resulting Specctra `.ses` session file, drawing copper tracks and vias on your board.

---

## 8. Manufacturing Exports (Gerber, BOM, Pick-and-Place)

Once your PCB layout is complete, export fabrication files:
* **Export Gerber (All Layers)**: Generates standard **RS-274X Gerber** files (Copper, Silkscreen, Solder Mask, Board Outline) and **Excellon Drill** files ready to upload to any PCB fab house (JLCPCB, PCBWay, etc.).
* **Export BOM (CSV)**: Generates a Bill of Materials spreadsheet mapping component designators, values, quantities, and supplier footprint packages.
* **Export Pick & Place (CSV)**: Generates a Centroid file detailing $X$ and $Y$ coordinates, rotations, assembly layers, and package descriptions for pick-and-place assembly machines.

---

## 9. Python Scripting & GPU-Accelerated Math

The console at the bottom features an interactive **embedded Python shell** with preloaded numpy, scipy, and cupy packages.

### Automated Yield Calculation (Monte Carlo) Example
You can run automated parameter sweeps leveraging your graphics card (via CUDA/CuPy) directly from the console:

```python
import cupy as cp
import numpy as np

# Simulate 1,000,000 resistor tolerances on GPU
num_samples = 1000000
nominal_r = 10000  # 10k
tolerance = 0.05   # 5%

# Generate normal distribution on GPU
resistors = cp.random.normal(nominal_r, nominal_r * tolerance / 3.0, num_samples)
print("GPU Mean Resistance:", cp.mean(resistors))
print("GPU Std Dev:", cp.std(resistors))
```

To control the live schematic from Python, use the globally exposed `Schematic` object:
```python
# Print all components in the active schematic
for comp in Schematic.GetComponents():
    print(comp.Designator, comp.Value)
```

---

## 10. Keyboard Shortcuts Reference

| Shortcut | Action | Scope |
|----------|--------|-------|
| `Ctrl + N` | Create New Project | App |
| `Ctrl + O` | Open Existing Project | App |
| `Ctrl + S` | Save Active Project | App |
| `Ctrl + Shift + S` | Save Project As... | App |
| `Ctrl + ,` | Open App Preferences | App |
| `F1` | Open Help & Shortcut Guide | App |
| `Ctrl + Shift + H` | Open Community Component Hub | App |
| `W` | Activate Wiring Tool | Canvas |
| `Escape` | Cancel Active Tool / Return to Selection | Canvas |
| `Delete` / `Backspace` | Delete Selected Item(s) | Canvas |
| `Ctrl + Z` | Undo Last Command | Canvas |
| `Ctrl + Y` | Redo Last Undone Command | Canvas |
| `Ctrl + Shift + E` | Export Schematic Canvas to PNG | Canvas |
| `Ctrl + Shift + V` | Export Schematic Canvas to SVG | Canvas |
| `F5` | Start SPICE/Logic Simulation | Canvas |
| `F6` | Generate and View SPICE Netlist | Canvas |
| `Ctrl + Shift + P` | Sync Schematic to PCB Layout View | App |
