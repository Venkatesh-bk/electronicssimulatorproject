# Volume 1: High School Foundations (Deep Expanded)

This document covers the fundamental science and physics required to understand the absolute basics of electronic circuits and Electronic Design Automation (EDA), pushed to the absolute limit of foundational knowledge.

## 1. Classical Circuit Theory & Advanced Network Analysis
The backbone of any nodal EDA simulator lies in macroscopic circuit theory.

### Ohm's Law & Power Dissipation
The relationship between Voltage ($V$), Current ($I$), and Resistance ($R$):
$$ V = I \times R $$
Joule's First Law (Power dissipation in heat):
$$ P = I^2 \times R = \frac{V^2}{R} $$
In a simulator, resistors form the diagonal of the conductance matrix ($\mathbf{G}$) where $G = 1/R$. Power dissipation tracking is the first step in thermal-co-simulation.

### Kirchhoff's Laws (KVL/KCL)
- **Kirchhoff's Current Law (KCL)**: $\sum_{k=1}^{n} I_k = 0$. Charge cannot be created or destroyed. This is the foundation of Modified Nodal Analysis (MNA), which this simulator uses to build its matrix $A \cdot x = b$.
- **Kirchhoff's Voltage Law (KVL)**: $\sum_{k=1}^{n} V_k = 0$. The total energy gained and lost in a closed loop is zero (Conservation of Energy).

### Advanced Network Theorems
EDA tools mathematically collapse large linear segments to save computation time:
- **Thevenin's Theorem**: Any linear circuit can be reduced to a single voltage source ($V_{th}$) and series resistor ($R_{th}$).
- **Norton's Theorem**: Any linear circuit can be reduced to a single current source ($I_{no}$) and parallel resistor ($R_{no}$).
- **Millman's Theorem**: Used for calculating the voltage at the ends of parallel branches.
- **Superposition Theorem**: The total current in any part of a linear circuit equals the algebraic sum of the currents produced by each source independently.

## 2. Electromagnetism and Energy Storage

### Capacitance ($C$) and the Electric Field
Capacitors store energy in an electric field between plates:
$$ C = \varepsilon_r \varepsilon_0 \frac{A}{d} $$
Where $\varepsilon_r$ is the relative permittivity (dielectric constant) of the PCB material.
The current is proportional to the *rate of change* of voltage:
$$ I = C \frac{dV}{dt} $$
Energy stored: $E = \frac{1}{2} C V^2$.

### Inductance ($L$) and the Magnetic Field
Inductors store energy in a magnetic field:
$$ L = \frac{N^2 \mu A}{l} $$
The voltage is proportional to the *rate of change* of current (Lenz's Law):
$$ V = L \frac{dI}{dt} $$
Energy stored: $E = \frac{1}{2} L I^2$.

## 3. Basic Semiconductor Physics

### Intrinsic vs Extrinsic Semiconductors
- **Intrinsic**: Pure Silicon (Si) or Germanium (Ge). At absolute zero, they are perfect insulators.
- **Doping**: Injecting impurities to create *Extrinsic* semiconductors.
  - **N-Type**: Doping with Phosphorus (Group V), providing excess **electrons**.
  - **P-Type**: Doping with Boron (Group III), providing excess **holes** (absence of electrons).

### The P-N Junction Diode
When P-type material is joined with N-type material, electrons diffuse into the P-region and holes diffuse into the N-region, leaving behind charged ions. This forms a **Depletion Region**.
An inherent electric field ($\vec{E}$) builds up, creating a barrier potential ($V_{barrier} \approx 0.7V$ for Silicon).

The macroscopic current is described by the Shockley Diode Equation:
$$ I_D = I_S \left( e^{\frac{V_D}{n V_T}} - 1 \right) $$
- $I_S$: Reverse saturation current
- $V_D$: Voltage across the diode
- $n$: Ideality factor (1.0 to 2.0)
- $V_T$: Thermal Voltage ($\frac{kT}{q} \approx 26mV$ at room temperature).

### Real-World Non-Idealities (High School +)
Even simple diodes are not perfect. A real simulator must account for:
- **Junction Capacitance ($C_j$)**: The depletion region acts as a capacitor, slowing down high-frequency switching.
- **Series Resistance ($R_s$)**: The physical bulk semiconductor material has resistance, meaning the diode voltage actually rises linearly at very high currents.
