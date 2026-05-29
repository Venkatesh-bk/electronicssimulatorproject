import pickle
import gzip
import json
import os
import numpy as np
import pandas as pd

def export_database():
    print("Consolidating EDA Deep Research Databases...")
    
    # Paths
    v1_path = "resources/research/EdaDeepResearchDatabase.pkl.gz"
    v3_path = "resources/research/EdaDeepResearchDatabase_v3.pkl.gz"
    output_path = "resources/research/EdaDeepResearchDatabase.json"
    
    data_out = {
        "metadata": {
            "title": "EDA Simulator Consolidated Deep Research Database",
            "version": "3.1.0",
            "description": "Unified database of semiconductor physics, technology nodes, 3D-IC thermal networks, and industry modeling methodologies."
        }
    }
    
    # 1. Load V1 (Physics, Transistors, WBG Materials)
    if os.path.exists(v1_path):
        print(f"Loading {v1_path}...")
        with gzip.open(v1_path, "rb") as f:
            v1_db = pickle.load(f)
            
        # A. Wide Bandgap Semiconductors
        if "wide_bandgap_semiconductors" in v1_db:
            df_wbg = v1_db["wide_bandgap_semiconductors"]
            # Convert DataFrame to list of dicts
            data_out["wide_bandgap_semiconductors"] = df_wbg.to_dict(orient="records")
            print("   -> Extracted Wide Bandgap Semiconductor profiles.")
            
        # B. BSIM-CMG GAAFET/FinFET node statistics
        if "bsim_cmg_v107_gaafet_finfet" in v1_db:
            df_bsim = v1_db["bsim_cmg_v107_gaafet_finfet"]
            node_stats = []
            for node, group in df_bsim.groupby("technology_node_nm"):
                node = int(node)
                node_stats.append({
                    "node_nm": node,
                    "geometry_type": "GAAFET" if node <= 5 else "FinFET",
                    "channel_length_nm_mean": float(group["L_m"].mean() * 1e9),
                    "channel_length_nm_std": float(group["L_m"].std() * 1e9),
                    "thickness_nm_mean": float(group["TFIN_m"].mean() * 1e9),
                    "oxide_thickness_nm_mean": float(group["TOXP_m"].mean()),
                    "vth0_V_mean": float(group["VTH0_V"].mean()),
                    "vth0_V_std": float(group["VTH0_V"].std()),
                    "mobility_cm2Vs_mean": float(group["U0_cm2Vs"].mean()),
                    "sat_velocity_cms_mean": float(group["VSAT_cms"].mean()),
                    "subthreshold_slope_mv_dec_mean": float(group["SS_mV_dec"].mean()),
                    "drive_current_uA_um_mean": float(group["IDS_sat_uA_um"].mean()),
                    "off_current_pA_um_mean": float(group["IOFF_pA_um"].mean()),
                    "yield_pct": float(group["pass_yield_flag"].mean() * 100)
                })
            data_out["bsim_node_statistics"] = sorted(node_stats, key=lambda x: x["node_nm"])
            print("   -> Processed BSIM-CMG parameter distributions.")

        # C. 3D-IC Chiplet Thermal Network statistics
        if "chiplet_3d_thermal_network" in v1_db:
            df_thermal = v1_db["chiplet_3d_thermal_network"]
            thermal_stats = []
            for layers, group in df_thermal.groupby("stack_layers"):
                layers = int(layers)
                thermal_stats.append({
                    "stack_layers": layers,
                    "avg_thermal_resistance_CW": float(group["R_thermal_total_CW"].mean()),
                    "avg_junction_temp_C": float(group["T_junction_C"].mean()),
                    "safe_yield_pct": float(group["T_junction_safe"].mean() * 100),
                    "max_safe_tdp_W": float(group[group["T_junction_safe"]]["power_tdp_W"].max() if any(group["T_junction_safe"]) else 0.0)
                })
            data_out["chiplet_thermal_profiles"] = sorted(thermal_stats, key=lambda x: x["stack_layers"])
            print("   -> Synthesized 3D-IC Thermal Network models.")
            
        # D. Open-Source PDK Distributions
        if "open_source_pdk_distributions" in v1_db:
            pdk_dist = v1_db["open_source_pdk_distributions"]
            pdk_out = {}
            for name, df in pdk_dist.items():
                pdk_out[name] = {
                    "vth0_mean": float(df["Vth0_V"].mean()),
                    "vth0_std": float(df["Vth0_V"].std()),
                    "mobility_mean": float(df["U0_cm2Vs"].mean()),
                    "tox_nm_mean": float(df["Tox_m"].mean() * 1e9),
                    "l_min_nm": int(df["L_min_nm"].iloc[0]),
                    "w_min_nm": int(df["W_min_nm"].iloc[0])
                }
            data_out["open_source_pdk_distributions"] = pdk_out
            print("   -> Compiled open-source PDK statistical summaries.")
            
    # 2. Load V3 (Control Loops, S-Parameters, Core Industry Algorithms)
    if os.path.exists(v3_path):
        print(f"Loading {v3_path}...")
        with gzip.open(v3_path, "rb") as f:
            v3_db = pickle.load(f)
            
        # A. Copy core mathematical comparison descriptions
        if "signal_integrity_hyperlynx" in v3_db:
            data_out["signal_integrity_hyperlynx"] = v3_db["signal_integrity_hyperlynx"]
        if "electromagnetics_ansys" in v3_db:
            data_out["electromagnetics_ansys"] = v3_db["electromagnetics_ansys"]
        if "system_modeling_matlab" in v3_db:
            data_out["system_modeling_matlab"] = v3_db["system_modeling_matlab"]
        if "mixed_signal_proteus" in v3_db:
            data_out["mixed_signal_proteus"] = v3_db["mixed_signal_proteus"]
            
        if "core_algorithms" in v3_db:
            data_out["core_algorithms"] = v3_db["core_algorithms"]
        elif "core_algorithms" in v1_db:
            data_out["core_algorithms"] = v1_db["core_algorithms"]
            
        print("   -> Merged industry-standard simulation algorithms (Ansys, MATLAB, HyperLynx, Proteus).")

    # Save to JSON
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(data_out, f, indent=4)
        
    print(f"Consolidated database exported successfully to {output_path}!")

if __name__ == "__main__":
    export_database()
