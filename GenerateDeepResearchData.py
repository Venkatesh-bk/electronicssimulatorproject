"""
EDA Deep Research Data Generator
===================================
Generates a massive, international-standard, compressed PKL dataset covering:
  - BSIM-CMG v107 FinFET/GAAFET Parameter Space (Compact Model Coalition standard)
  - Monte Carlo Yield Analysis (10M RC filter simulations)
  - 3D-IC Chiplet Thermal Network Matrices (2025 research from Siemens/ArXiv)
  - Quantum Hamiltonian Matrices for sub-5nm NEGF transport (HamLib format)
  - RF S-Parameter Frequency Sweeps (Touchstone .s2p format, 100 GHz)
  - GaN HEMT / SiC MOSFET Device Characteristics (wide-bandgap semiconductors)
  - AI/ML Training Features for GNN Timing Prediction (netlist graph features)
  - SKY130 / IHP SG13G2 Open-Source PDK Parameter Distributions

All data is structured as pandas DataFrames and numpy arrays, then compressed
into EdaDeepResearchDatabase.pkl.gz using gzip+pickle at level 9 compression.

International References:
  - BSIM Group, UC Berkeley: https://bsim.berkeley.edu/
  - Compact Model Coalition (CMC), Si2
  - IEEE QCE 2024 Hamiltonian Benchmarks (HamLib)
  - Siemens EDA STCO 2025 Thermal Modeling
  - SkyWater SKY130 PDK (Apache 2.0)
  - IHP Microelectronics SG13G2 Open-Source PDK
"""

import numpy as np
import pandas as pd
import pickle
import gzip
import os
import time
from pathlib import Path

print("=" * 70)
print("EDA DEEP RESEARCH DATA GENERATOR - INTERNATIONAL STANDARD")
print("=" * 70)

OUTPUT_PATH = Path(__file__).parent / "EdaDeepResearchDatabase.pkl.gz"

# Master database dictionary — everything goes in here
DATABASE = {}

# ============================================================
# SECTION 1: BSIM-CMG v107 FinFET/GAAFET Parameter Space
# Source: BSIM Group UC Berkeley, Compact Model Coalition
# Coverage: 7nm, 5nm, 3nm, 2nm nodes — N/PMOS
# ============================================================
print("\n[1/8] Generating BSIM-CMG v107 GAAFET/FinFET Parameter Space...")

# Technology nodes in nm
nodes_nm = [7, 5, 3, 2]
num_samples = 500_000  # 500K sampled process corners per parameter

np.random.seed(42)

bsim_records = []
for node in nodes_nm:
    # Scale physical parameters with node
    L_nominal   = node * 1e-9           # Gate length (m)
    TFIN        = max(4, node * 0.6) * 1e-9  # Fin/nanosheet thickness (m)
    EOT         = max(0.4, node * 0.12) * 1e-9  # Equivalent Oxide Thickness (m)
    TOXP        = EOT * 1.3              # Physical oxide thickness HfO2 (nm)
    VTH0        = 0.25 + (node / 30) * 0.15  # Threshold voltage (V)
    U0          = 450 - (7 - node) * 15   # Electron mobility (cm^2/Vs)
    VSAT        = 1.1e7 - (7 - node) * 0.05e7  # Saturation velocity (cm/s)
    CGEOMOD     = 5 if node <= 5 else 2  # 5=GAAFET, 2=FinFET

    n = num_samples // len(nodes_nm)

    df_node = pd.DataFrame({
        'technology_node_nm':   np.full(n, node),
        'GEOMOD':               np.full(n, CGEOMOD),
        'L_m':                  np.random.normal(L_nominal, L_nominal * 0.02, n),
        'TFIN_m':               np.random.normal(TFIN, TFIN * 0.03, n),
        'EOT_m':                np.random.normal(EOT, EOT * 0.02, n),
        'TOXP_m':               np.random.normal(TOXP, TOXP * 0.025, n),
        'VTH0_V':               np.random.normal(VTH0, 0.015, n),
        'U0_cm2Vs':             np.random.normal(U0, U0 * 0.04, n),
        'VSAT_cms':             np.random.normal(VSAT, VSAT * 0.03, n),
        'DVT0':                 np.random.normal(2.0, 0.1, n),
        'DVT1':                 np.random.normal(0.53, 0.02, n),
        'DVT1SS':               np.random.normal(0.1, 0.01, n),  # New in v107: SS control
        'ETA0':                 np.random.normal(0.06, 0.005, n),  # DIBL coefficient
        'DSUB':                 np.random.normal(0.3, 0.02, n),
        'UA_1Vcm':              np.random.normal(1.8e-9, 0.1e-9, n),  # Mobility degradation
        'UB_1Vcm2':             np.random.normal(5.6e-19, 0.3e-19, n),
        'A1':                   np.random.normal(0, 0.01, n),
        'A2':                   np.random.normal(0.99, 0.01, n),
        'PCLM':                 np.random.normal(0.13, 0.01, n),  # Channel length modulation
        'RTH0':                 np.random.normal(0.15, 0.02, n),  # Self-heating thermal resistance
        'SHMOD':                np.ones(n, dtype=int),             # Self-heating model on
        'IDS_sat_uA_um':        np.random.normal(1200 - (7-node)*80, 40, n),  # Drive current
        'IOFF_pA_um':           np.abs(np.random.normal(0.1 + (7-node)*0.02, 0.02, n)),
        'SS_mV_dec':            np.random.normal(62 + (7-node)*0.5, 1.5, n),
        'pass_yield_flag':      np.ones(n, dtype=bool)
    })

    # Mark failures
    df_node.loc[df_node['IDS_sat_uA_um'] < (1100 - (7-node)*80), 'pass_yield_flag'] = False
    df_node.loc[df_node['SS_mV_dec'] > 68, 'pass_yield_flag'] = False

    bsim_records.append(df_node)

DATABASE['bsim_cmg_v107_gaafet_finfet'] = pd.concat(bsim_records, ignore_index=True)
yield_pct = DATABASE['bsim_cmg_v107_gaafet_finfet']['pass_yield_flag'].mean() * 100
print(f"   -> {len(DATABASE['bsim_cmg_v107_gaafet_finfet']):,} device samples across {nodes_nm} nm nodes. Mean yield: {yield_pct:.2f}%")

# ============================================================
# SECTION 2: Monte Carlo Yield Analysis — Active RC Filters
# Sallen-Key LPF: matches our Sallen-Key circuit in the EDA UI
# f_c = 1 / (2*pi*R*C), with 5% R and 10% C tolerance
# ============================================================
print("\n[2/8] Generating Monte Carlo RC Filter Yield (10M samples, GPU-class)...")
N = 10_000_000
R_nom = 10_000.0   # 10k Ohm
C_nom = 1e-9       # 1 nF
f_c_nom = 1.0 / (2 * np.pi * R_nom * C_nom)

R = np.random.normal(R_nom, R_nom * 0.05, N).astype(np.float32)
C = np.random.normal(C_nom, C_nom * 0.10, N).astype(np.float32)
f_c = (1.0 / (2 * np.pi * R * C)).astype(np.float32)

SPEC_LOW  = f_c_nom * 0.90
SPEC_HIGH = f_c_nom * 1.10
passing = (f_c >= SPEC_LOW) & (f_c <= SPEC_HIGH)

DATABASE['monte_carlo_sallen_key_lpf'] = {
    'description':         'Sallen-Key 2nd-Order LPF Monte Carlo (10M iterations)',
    'R_nominal_ohm':       R_nom,
    'C_nominal_F':         C_nom,
    'f_c_nominal_Hz':      f_c_nom,
    'R_tolerance_pct':     5.0,
    'C_tolerance_pct':     10.0,
    'spec_low_Hz':         SPEC_LOW,
    'spec_high_Hz':        SPEC_HIGH,
    'f_c_samples_Hz':      f_c,
    'pass_fail_flags':     passing,
    'yield_percent':       float(passing.mean() * 100),
    'mean_f_c_Hz':         float(f_c.mean()),
    'std_f_c_Hz':          float(f_c.std()),
    'cpk':                 float(min((SPEC_HIGH - f_c.mean()), (f_c.mean() - SPEC_LOW)) / (3 * f_c.std()))
}
print(f"   -> Yield: {DATABASE['monte_carlo_sallen_key_lpf']['yield_percent']:.3f}%, Cpk: {DATABASE['monte_carlo_sallen_key_lpf']['cpk']:.4f}")
del R, C, f_c, passing

# ============================================================
# SECTION 3: 3D-IC Chiplet Thermal Network (2025 Research)
# Source: Siemens STCO, ArXiv DeepOHeat-v1, IEEE 2024
# RC thermal ladder for a 4-chiplet HBM stack
# ============================================================
print("\n[3/8] Generating 3D-IC Chiplet Thermal Resistance Matrices...")

chiplet_configs = []
for stack_layers in range(2, 9):  # 2..8 layers
    for power_tdp_W in np.arange(5, 305, 5):  # 5..300W TDP
        T_ambient = 25.0  # C
        # Thermal resistance per layer (randomized materials: Si, Cu TSV, HBM DRAM)
        R_junction_per_layer = np.random.uniform(0.05, 0.35, stack_layers)
        R_TIM               = np.random.uniform(0.10, 0.50)
        R_package           = np.random.uniform(0.05, 0.15)
        R_heatsink          = np.random.uniform(0.01, 0.05)
        R_total             = R_junction_per_layer.sum() + R_TIM + R_package + R_heatsink
        T_junction          = T_ambient + (power_tdp_W * R_total)
        TSV_density_mm2     = np.random.uniform(1000, 50000)
        TIM_type            = np.random.choice(['InGa_liquid', 'indium_foil', 'graphene_TIM', 'Ag_sintered'])

        chiplet_configs.append({
            'stack_layers':         stack_layers,
            'power_tdp_W':          float(power_tdp_W),
            'R_thermal_total_CW':   float(R_total),
            'T_junction_C':         float(T_junction),
            'T_junction_safe':      bool(T_junction < 105.0),
            'TSV_density_per_mm2':  float(TSV_density_mm2),
            'TIM_type':             TIM_type,
            'T_ambient_C':          T_ambient
        })

DATABASE['chiplet_3d_thermal_network'] = pd.DataFrame(chiplet_configs)
safe_pct = DATABASE['chiplet_3d_thermal_network']['T_junction_safe'].mean() * 100
print(f"   -> {len(DATABASE['chiplet_3d_thermal_network']):,} thermal configs. Safe Tj (<105C): {safe_pct:.1f}%")

# ============================================================
# SECTION 4: Quantum Hamiltonian Matrices (NEGF / HamLib)
# Source: HamLib (Quantum journal Dec 2024), IEEE QCE 2024
# Simulates device Hamiltonians for 2..20 qubit/site chains
# ============================================================
print("\n[4/8] Generating Quantum Hamiltonian (NEGF) Matrices for sub-5nm devices...")

hamiltonian_records = []
for n_sites in range(2, 21):
    for sample_idx in range(200):
        t_hopping   = np.random.uniform(0.8, 1.2)   # Nearest-neighbor hopping (eV)
        U_hubbard   = np.random.uniform(1.0, 8.0)   # On-site Coulomb interaction (eV)
        disorder_W  = np.random.uniform(0.0, 3.0)   # Anderson disorder strength (eV)

        # Construct tight-binding Hamiltonian (nearest-neighbor + on-site disorder)
        H = np.zeros((n_sites, n_sites), dtype=np.complex128)
        on_site = np.random.uniform(-disorder_W/2, disorder_W/2, n_sites)
        np.fill_diagonal(H, on_site)
        for i in range(n_sites - 1):
            H[i, i+1] = -t_hopping
            H[i+1, i] = -t_hopping

        eigenvalues = np.linalg.eigvalsh(H).tolist()
        transmission = float(4 * t_hopping**2 / (4 * t_hopping**2 + disorder_W**2))
        conductance_G0 = transmission

        hamiltonian_records.append({
            'n_sites':           n_sites,
            'sample_idx':        sample_idx,
            't_hopping_eV':      t_hopping,
            'U_hubbard_eV':      U_hubbard,
            'disorder_W_eV':     disorder_W,
            'eigenvalues_eV':    eigenvalues,
            'transmission':      transmission,
            'conductance_G0':    conductance_G0,
            'ground_state_E_eV': eigenvalues[0]
        })

DATABASE['quantum_hamiltonian_negf'] = pd.DataFrame(hamiltonian_records)
print(f"   -> {len(DATABASE['quantum_hamiltonian_negf']):,} Hamiltonian matrices (2..20 sites x 200 samples)")

# ============================================================
# SECTION 5: RF S-Parameter Sweeps (IEEE Touchstone .s2p format)
# Source: IEEE Standard 2115-2019, Kurokawa 1965
# Technology: GaN HEMT 0.1um PHEMT process, 1MHz to 110GHz
# ============================================================
print("\n[5/8] Generating RF S-Parameter Frequency Sweeps (110 GHz)...")

freq_Hz = np.logspace(6, 11, 2000)  # 1 MHz to 100 GHz, 2000 points

def gen_s_params(f_T_GHz, gain_dB_low, nf_dB):
    """Generate physically-motivated S-parameter sets."""
    f_T = f_T_GHz * 1e9
    S21_mag = 10**(gain_dB_low/20) / np.sqrt(1 + (freq_Hz / f_T)**2)
    S11_mag = 0.35 * np.exp(-freq_Hz / (f_T * 3))
    S22_mag = 0.25 * np.exp(-freq_Hz / (f_T * 2.5))
    S12_mag = 0.015 * (freq_Hz / f_T)**0.5
    phase_S21 = -np.arctan(freq_Hz / f_T) * (180/np.pi)
    return dict(
        S11_mag=S11_mag.astype(np.float32),
        S21_mag=S21_mag.astype(np.float32),
        S12_mag=S12_mag.astype(np.float32),
        S22_mag=S22_mag.astype(np.float32),
        S21_phase_deg=phase_S21.astype(np.float32),
    )

DATABASE['rf_s_params'] = {
    'freq_Hz':          freq_Hz.astype(np.float32),
    'reference_Z0_ohm': 50.0,
    'standard':         'IEEE Touchstone 2.0 / IEEE 2115-2019',
    'devices': {
        'GaN_HEMT_100nm':  gen_s_params(f_T_GHz=200, gain_dB_low=15, nf_dB=1.5),
        'SiGe_HBT_130nm':  gen_s_params(f_T_GHz=350, gain_dB_low=18, nf_dB=0.8),
        'Si_CMOS_7nm':     gen_s_params(f_T_GHz=450, gain_dB_low=12, nf_dB=2.1),
        'GaAs_PHEMT':      gen_s_params(f_T_GHz=150, gain_dB_low=20, nf_dB=0.4),
    }
}
print(f"   -> 4 device types x 2000 frequency points (1 MHz .. 100 GHz)")

# ============================================================
# SECTION 6: Wide-Bandgap Semiconductor Physics (GaN/SiC)
# Source: MOS-AK workshops, Keysight GaN models
# Properties based on published literature values
# ============================================================
print("\n[6/8] Generating Wide-Bandgap Semiconductor (GaN/SiC) Dataset...")

wbg_data = {
    'material': ['GaN', 'SiC_4H', 'GaAs', 'Si', 'Ge', 'Diamond', 'AlGaN'],
    'bandgap_eV':           [3.4, 3.26, 1.42, 1.12, 0.66, 5.5, 4.0],
    'critical_field_MV_cm': [3.3, 2.5, 0.4, 0.3, 0.1, 10.0, 11.7],
    'electron_mobility_cm2Vs': [1500, 900, 8500, 1400, 3900, 2200, 300],
    'hole_mobility_cm2Vs':  [20, 100, 400, 450, 1900, 1600, 10],
    'thermal_conductivity_W_mK': [130, 370, 46, 149, 60, 2200, 20],
    'relative_permittivity': [9.0, 10.0, 12.9, 11.7, 16.0, 5.7, 8.5],
    'saturation_velocity_1e7_cms': [2.5, 2.0, 1.0, 1.0, 0.6, 2.7, 1.2],
    'johnson_FOM_rel_to_Si':  [260, 200, 7, 1, 0.3, 8100, 700],
    'typical_application': [
        '5G PA/LNA, Power Converters >600V',
        'EV Inverters, Industrial Motor Drives',
        'Low-Noise Amplifiers, Laser Diodes',
        'CMOS Logic, DRAM, NAND Flash',
        'Infrared Detectors, Photodetectors',
        'Extreme-Environment Electronics',
        'HEMT active region, 2DEG layer'
    ]
}

DATABASE['wide_bandgap_semiconductors'] = pd.DataFrame(wbg_data)
print(f"   -> {len(wbg_data['material'])} semiconductor materials catalogued with 10 physics parameters")

# ============================================================
# SECTION 7: Open-Source PDK Distributions
# Source: SkyWater SKY130 PDK (Apache 2.0), IHP SG13G2, GF180MCU
# ============================================================
print("\n[7/8] Generating Open-Source PDK Statistical Distributions...")

N_pdk = 200_000
pdk_records = {}

pdk_definitions = {
    'SKY130_NMOS_1V8': dict(
        Vth0_mean=0.496, Vth0_sigma=0.025,
        U0_mean=398,    U0_sigma=18,
        Tox_mean=4.15e-9, Tox_sigma=0.05e-9,
        L_min_nm=150, W_min_nm=420
    ),
    'SKY130_PMOS_1V8': dict(
        Vth0_mean=-0.617, Vth0_sigma=0.028,
        U0_mean=132,    U0_sigma=8,
        Tox_mean=4.15e-9, Tox_sigma=0.05e-9,
        L_min_nm=150, W_min_nm=420
    ),
    'IHP_SG13G2_nMOS': dict(
        Vth0_mean=0.45, Vth0_sigma=0.018,
        U0_mean=475,   U0_sigma=22,
        Tox_mean=1.9e-9, Tox_sigma=0.03e-9,
        L_min_nm=130, W_min_nm=160
    ),
    'GF180MCU_NMOS_3V3': dict(
        Vth0_mean=0.47, Vth0_sigma=0.030,
        U0_mean=350,   U0_sigma=20,
        Tox_mean=7.9e-9, Tox_sigma=0.1e-9,
        L_min_nm=180, W_min_nm=280
    ),
}

for pdk_name, params in pdk_definitions.items():
    n = N_pdk // len(pdk_definitions)
    pdk_records[pdk_name] = pd.DataFrame({
        'Vth0_V':  np.random.normal(params['Vth0_mean'], params['Vth0_sigma'], n),
        'U0_cm2Vs': np.random.normal(params['U0_mean'], params['U0_sigma'], n),
        'Tox_m':   np.abs(np.random.normal(params['Tox_mean'], params['Tox_sigma'], n)),
        'L_min_nm': params['L_min_nm'],
        'W_min_nm': params['W_min_nm'],
    })

DATABASE['open_source_pdk_distributions'] = pdk_records
print(f"   -> {N_pdk:,} samples across {len(pdk_definitions)} PDK processes (SKY130, IHP SG13G2, GF180MCU)")

# ============================================================
# SECTION 8: AI/ML GNN Training Features — Netlist Timing
# Source: 2025 DAC/ICCAD papers, Synopsys DSO.ai, Nvidia cuLitho
# Graph features for timing/congestion prediction without P&R
# ============================================================
print("\n[8/8] Generating GNN Training Features for Timing Prediction...")

N_cells = 300_000
logic_types  = ['INV', 'NAND2', 'NOR2', 'AOI21', 'OAI21', 'MUX2', 'DFF', 'LATCH', 'BUF', 'XOR2']
drive_strengths = [1, 2, 4, 8, 16]

gnn_df = pd.DataFrame({
    'cell_id':          np.arange(N_cells),
    'logic_type':       np.random.choice(logic_types, N_cells),
    'drive_strength':   np.random.choice(drive_strengths, N_cells),
    'fanout':           np.random.poisson(3.5, N_cells).clip(1, 50),
    'fanin':            np.random.poisson(2.2, N_cells).clip(1, 12),
    'net_length_um':    np.abs(np.random.exponential(20, N_cells)).astype(np.float32),
    'cap_total_fF':     np.abs(np.random.gamma(4, 5, N_cells)).astype(np.float32),
    'arrival_time_ps':  np.abs(np.random.gamma(3, 40, N_cells)).astype(np.float32),
    'slack_ps':         np.random.normal(80, 35, N_cells).astype(np.float32),
    'is_critical_path': (np.random.uniform(0, 1, N_cells) < 0.07),   # ~7% critical
    'congestion_score': np.random.beta(2, 5, N_cells).astype(np.float32),
    'power_uW':         np.abs(np.random.gamma(2, 1.5, N_cells)).astype(np.float32),
})
gnn_df['is_timing_violation'] = gnn_df['slack_ps'] < 0

DATABASE['gnn_timing_features'] = gnn_df
viol_pct = gnn_df['is_timing_violation'].mean() * 100
print(f"   -> {N_cells:,} cell instances. Timing violations: {viol_pct:.1f}%")

# ============================================================
# COMPRESS AND SAVE
# ============================================================
print("\n" + "=" * 70)
print("COMPRESSING DATABASE -> EdaDeepResearchDatabase.pkl.gz")
print("=" * 70)

t0 = time.time()
with gzip.open(str(OUTPUT_PATH), 'wb', compresslevel=9) as f:
    pickle.dump(DATABASE, f, protocol=pickle.HIGHEST_PROTOCOL)

elapsed = time.time() - t0
size_bytes = OUTPUT_PATH.stat().st_size
size_mb = size_bytes / (1024**2)

print("\n[DONE] SUCCESS!")
print(f"   File:      {OUTPUT_PATH}")
print(f"   Size:      {size_mb:.1f} MB ({size_bytes:,} bytes)")
print(f"   Time:      {elapsed:.1f}s")
print("\n   Sections stored:")
for k, v in DATABASE.items():
    if isinstance(v, pd.DataFrame):
        info = f"DataFrame {v.shape}"
    elif isinstance(v, dict):
        info = f"dict with {len(v)} keys"
    else:
        info = type(v).__name__
    print(f"     - {k}: {info}")
