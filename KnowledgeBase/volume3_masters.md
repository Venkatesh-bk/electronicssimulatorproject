# Volume 3: Master's Level (Advanced Simulation Algorithms)

At the Master's level, the focus shifts to computational modeling. How do we solve massive systems of differential and non-linear equations representing millions of transistors efficiently? This is the domain of numerical EDA architecture.

## 1. Numerical Methods for SPICE

### Modified Nodal Analysis (MNA)
Standard Nodal Analysis (using KCL) fails when components like Voltage Sources ($V = 5V$) or Inductors are present, because their currents cannot be expressed as $I = G \cdot V$.
MNA solves this by expanding the conductance matrix $\mathbf{G}$ into a larger block matrix $\mathbf{A}$, where voltage source currents become independent variables.
$$ \begin{bmatrix} \mathbf{G} & \mathbf{B} \\ \mathbf{C} & \mathbf{D} \end{bmatrix} \begin{bmatrix} \mathbf{v} \\ \mathbf{i} \end{bmatrix} = \begin{bmatrix} \mathbf{i}_{src} \\ \mathbf{v}_{src} \end{bmatrix} $$
- **$\mathbf{G}$**: Node conductances ($1/R$).
- **$\mathbf{B}$ and $\mathbf{C}$**: Incidence matrices tracking which nodes connect to voltage sources.
- **$\mathbf{D}$**: Often zero, unless dependent sources are used.

### Newton-Raphson (NR) Iteration & The Jacobian
Since Diodes and Transistors are non-linear (exponential), the matrix $\mathbf{A} \cdot \mathbf{x} = \mathbf{b}$ cannot be solved directly. SPICE uses Newton-Raphson to linearize the circuit around an operating point and iterate until convergence.
$$ \mathbf{x}_{k+1} = \mathbf{x}_k - \mathbf{J}^{-1}(\mathbf{x}_k) F(\mathbf{x}_k) $$
Where $\mathbf{J}$ is the Jacobian matrix. For a Diode $I_D = I_S e^{V_D / V_T}$, the entry in the Jacobian is the derivative $\frac{\partial I_D}{\partial V_D} = \frac{I_D}{V_T}$.
In circuit terms, this derivative is the "Dynamic Conductance" ($g_{eq}$). The non-linear device is replaced by a linear resistor $1/g_{eq}$ and a parallel current source $I_{eq}$ at each iteration step.

### Numerical Integration (Transient Analysis)
To solve differential equations like $I = C \frac{dV}{dt}$ over time, EDA simulators discretize time into steps ($\Delta t$). The capacitor is replaced by an equivalent history-dependent resistor and current source (Companion Model).
- **Backward Euler**: $V_{n+1} = V_n + \Delta t \cdot \frac{I_{n+1}}{C}$. Highly stable, but introduces artificial damping (numerical dissipation).
- **Trapezoidal Rule**: $V_{n+1} = V_n + \frac{\Delta t}{2C} (I_{n+1} + I_n)$. More accurate (2nd-order), but can suffer from numerical "ringing" around sharp discontinuities.
- **Gear's Method (BDF)**: A multi-step predictor-corrector method ideal for extremely stiff systems (highly inductive circuits) where Trapezoidal rings.

## 2. Advanced Semiconductor Device Models

### Gummel-Poon BJT Model
An advanced extension of Ebers-Moll that accounts for:
- **Early Effect**: Base width modulation by $V_{CB}$, decreasing output resistance.
- **High-Level Injection (Kirk Effect)**: At high current densities, the effective base width widens into the collector, causing current gain ($\beta$) roll-off.
- **Transit Times**: Dynamic charge storage ($Q_f, Q_r$) required to simulate accurate RF switching speeds and reverse recovery time.

### BSIM (Berkeley Short-Channel IGFET Model)
For nodes below 130nm, physical phenomena break the classical assumptions. BSIM3 and BSIM4 equations run into the thousands of parameters to model:
- **Velocity Saturation**: Electrons reach maximum speed due to optical phonon scattering. Mobility $\mu$ is no longer constant, but a function of the electric field $\mu(E)$.
- **Drain-Induced Barrier Lowering (DIBL)**: High drain voltages physically lower the threshold voltage barrier at the source, exponentially increasing subthreshold leakage.
- **Gate-Induced Drain Leakage (GIDL)**: Deep depletion in the drain overlap region causes band-to-band tunneling.
- **Quantum Mechanical Effects**: The extreme vertical electric field causes the peak of the inversion charge layer to shift away from the oxide interface into the bulk silicon.

## 3. Thermodynamics and Electro-Thermal Co-Simulation
Modern chips draw 100+ Amps of current. Joule heating ($P = I^2R$) causes the chip temperature to rise.
Because semiconductor physics (Fermi-Dirac distributions, carrier mobility $\mu$, threshold voltage $V_{TH}$) are heavily temperature-dependent, advanced EDA tools solve the Heat Equation via Partial Differential Equations (PDEs):
$$ \rho C_p \frac{\partial T}{\partial t} - \nabla \cdot (k \nabla T) = Q $$
Where $Q$ is the power density.
A thermal network is constructed using an electrical RC analogy:
- Temperature $\leftrightarrow$ Voltage
- Heat Flux $\leftrightarrow$ Current
- Thermal Resistance $\leftrightarrow$ Electrical Resistance
- Heat Capacity $\leftrightarrow$ Electrical Capacitance
The electrical solver (SPICE) and the thermal solver iterate back and forth until the temperature and currents converge to a steady state.
