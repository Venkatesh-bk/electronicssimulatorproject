# Volume 2: Undergraduate Engineering Core (Deep Expanded)

This volume dives into the mathematics and solid-state physics taught at the university engineering level. These are strictly required to construct accurate active device models and AC analysis engines in SPICE.

## 1. Electromagnetics (Maxwell's Equations)
While lumped-element SPICE simulates purely nodes and branches, high-speed EDA must respect Maxwell's equations to extract parasitic values.

- **Gauss's Law**: $\nabla \cdot \mathbf{E} = \frac{\rho}{\varepsilon_0}$. Determines the parasitic capacitance between any two traces on a PCB.
- **Gauss's Law for Magnetism**: $\nabla \cdot \mathbf{B} = 0$. There are no magnetic monopoles.
- **Faraday's Law**: $\nabla \times \mathbf{E} = -\frac{\partial \mathbf{B}}{\partial t}$. Time-varying magnetic fields (from switching traces) induce crosstalk voltages on adjacent traces.
- **Ampère-Maxwell Law**: $\nabla \times \mathbf{B} = \mu_0\mathbf{J} + \mu_0\varepsilon_0\frac{\partial \mathbf{E}}{\partial t}$. High-frequency displacement currents create magnetic interference (EMI/EMC compliance).

## 2. Linear Systems, Signals, and Networks
To analyze AC Frequency responses (AC Sweeps), EDA tools use the complex $s$-domain.

### Laplace Transform & Transfer Functions
Converts time-domain differential equations into algebraic equations in the complex frequency domain ($s = \sigma + j\omega$):
$$ F(s) = \int_{0}^{\infty} f(t) e^{-st} dt $$
- Capacitor Impedance: $Z_C = \frac{1}{sC}$
- Inductor Impedance: $Z_L = sL$

A filter's behavior is described by a Transfer Function $H(s) = \frac{V_{out}(s)}{V_{in}(s)}$. Finding the poles (roots of the denominator) determines the circuit's stability (Nyquist criteria, Bode Plots).

### S-Parameters (Scattering Parameters)
At high frequencies (RF/Microwaves), voltage and current are difficult to measure. EDA tools use Power Waves (S-parameters).
$$ \begin{bmatrix} b_1 \\ b_2 \end{bmatrix} = \begin{bmatrix} S_{11} & S_{12} \\ S_{21} & S_{22} \end{bmatrix} \begin{bmatrix} a_1 \\ a_2 \end{bmatrix} $$
Where $a$ is incident power and $b$ is reflected power. $S_{11}$ represents Return Loss, and $S_{21}$ represents Insertion Loss.

## 3. Solid State Physics
To accurately simulate a Transistor, the simulator evaluates the actual physics of semiconductor crystals.

### Density of States (DOS) and Fermi-Dirac Statistics
Electrons in a crystal lattice can only occupy specific energy bands. The probability that an energy state $E$ is occupied by an electron is given by the Fermi-Dirac distribution:
$$ f(E) = \frac{1}{1 + \exp\left(\frac{E - E_F}{k_B T}\right)} $$
Where $E_F$ is the Fermi Level, $k_B$ is Boltzmann's constant, and $T$ is absolute temperature. This equation proves that chip behavior changes drastically with temperature (thermal runaway).

### Drift-Diffusion Equations
Current in a semiconductor is the sum of drift (driven by electric fields) and diffusion (driven by concentration gradients):
$$ \mathbf{J}_n = q n \mu_n \mathbf{E} + q D_n \nabla n $$
$$ \mathbf{J}_p = q p \mu_p \mathbf{E} - q D_p \nabla p $$
Where $\mu$ is mobility and $D$ is the diffusion coefficient. The Einstein Relation connects them: $D_n = \frac{k_B T}{q} \mu_n$.

## 4. MOSFET Device Physics (Levels 1, 2, and 3)
The classical MOSFET equations derived from Drift-Diffusion.

### Level 1 (Shichman-Hodges Square Law)
Separates operation into three regions:
- **Cutoff** ($V_{GS} < V_{TH}$): $I_{DS} = 0$
- **Linear/Triode** ($V_{GS} > V_{TH}$ and $V_{DS} < V_{GS} - V_{TH}$):
  $$ I_{DS} = \mu_n C_{ox} \frac{W}{L} \left[ (V_{GS} - V_{TH})V_{DS} - \frac{V_{DS}^2}{2} \right] $$
- **Saturation** ($V_{DS} \ge V_{GS} - V_{TH}$):
  $$ I_{DS} = \frac{1}{2} \mu_n C_{ox} \frac{W}{L} (V_{GS} - V_{TH})^2 (1 + \lambda V_{DS}) $$
  *(Note: $\lambda$ models Channel Length Modulation).*

### Level 2 & 3 (Bulk Charge and Empirical Scaling)
As channel lengths ($L$) shrink below 2 micrometers, the Square Law fails. Level 2 includes bulk charge effects where the depletion region of the drain starts stealing control of the channel from the gate. Level 3 introduces empirical fitting parameters for Subthreshold Conduction (current flowing when $V_{GS} < V_{TH}$) and Mobility Degradation (electrons bumping into the silicon surface due to high gate fields).
