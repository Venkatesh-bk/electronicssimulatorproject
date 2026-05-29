using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EdaSimulator.Engines.Physics
{
    public class WbgMaterial
    {
        [JsonPropertyName("material")]
        public string Material { get; set; } = string.Empty;

        [JsonPropertyName("bandgap_eV")]
        public double Bandgap_eV { get; set; }

        [JsonPropertyName("critical_field_MV_cm")]
        public double CriticalField_MV_cm { get; set; }

        [JsonPropertyName("electron_mobility_cm2Vs")]
        public double ElectronMobility_cm2Vs { get; set; }

        [JsonPropertyName("hole_mobility_cm2Vs")]
        public double HoleMobility_cm2Vs { get; set; }

        [JsonPropertyName("thermal_conductivity_W_mK")]
        public double ThermalConductivity_W_mK { get; set; }

        [JsonPropertyName("relative_permittivity")]
        public double RelativePermittivity { get; set; }

        [JsonPropertyName("typical_application")]
        public string TypicalApplication { get; set; } = string.Empty;
    }

    public class BsimNodeStat
    {
        [JsonPropertyName("node_nm")]
        public int NodeNm { get; set; }

        [JsonPropertyName("geometry_type")]
        public string GeometryType { get; set; } = string.Empty;

        [JsonPropertyName("channel_length_nm_mean")]
        public double ChannelLengthNmMean { get; set; }

        [JsonPropertyName("oxide_thickness_nm_mean")]
        public double OxideThicknessNmMean { get; set; }

        [JsonPropertyName("vth0_V_mean")]
        public double Vth0_V_mean { get; set; }

        [JsonPropertyName("mobility_cm2Vs_mean")]
        public double Mobility_cm2Vs_mean { get; set; }

        [JsonPropertyName("subthreshold_slope_mv_dec_mean")]
        public double SubthresholdSlopeMvDecMean { get; set; }

        [JsonPropertyName("drive_current_uA_um_mean")]
        public double DriveCurrentUaUmMean { get; set; }

        [JsonPropertyName("yield_pct")]
        public double YieldPct { get; set; }
    }

    public class ChipletThermalProfile
    {
        [JsonPropertyName("stack_layers")]
        public int StackLayers { get; set; }

        [JsonPropertyName("avg_thermal_resistance_CW")]
        public double AvgThermalResistanceCW { get; set; }

        [JsonPropertyName("avg_junction_temp_C")]
        public double AvgJunctionTempC { get; set; }

        [JsonPropertyName("safe_yield_pct")]
        public double SafeYieldPct { get; set; }

        [JsonPropertyName("max_safe_tdp_W")]
        public double MaxSafeTdpW { get; set; }
    }

    public class PdkDistribution
    {
        [JsonPropertyName("vth0_mean")]
        public double Vth0Mean { get; set; }

        [JsonPropertyName("vth0_std")]
        public double Vth0Std { get; set; }

        [JsonPropertyName("mobility_mean")]
        public double MobilityMean { get; set; }

        [JsonPropertyName("tox_nm_mean")]
        public double ToxNmMean { get; set; }

        [JsonPropertyName("l_min_nm")]
        public int LMinNm { get; set; }

        [JsonPropertyName("w_min_nm")]
        public int WMinNm { get; set; }
    }

    public class ResearchDatabaseContent
    {
        [JsonPropertyName("wide_bandgap_semiconductors")]
        public List<WbgMaterial> WideBandgapSemiconductors { get; set; } = new();

        [JsonPropertyName("bsim_node_statistics")]
        public List<BsimNodeStat> BsimNodeStatistics { get; set; } = new();

        [JsonPropertyName("chiplet_thermal_profiles")]
        public List<ChipletThermalProfile> ChipletThermalProfiles { get; set; } = new();

        [JsonPropertyName("open_source_pdk_distributions")]
        public Dictionary<string, PdkDistribution> OpenSourcePdkDistributions { get; set; } = new();
    }

    /// <summary>
    /// Service loading consolidated deep research data for simulation constraints, limits, and guidelines.
    /// </summary>
    public class ResearchDatabaseService
    {
        private static readonly Lazy<ResearchDatabaseService> _instance = 
            new(() => new ResearchDatabaseService());

        public static ResearchDatabaseService Instance => _instance.Value;

        public ResearchDatabaseContent Content { get; private set; } = new();
        public bool IsLoaded { get; private set; }

        private ResearchDatabaseService()
        {
            LoadDatabase();
        }

        public void LoadDatabase()
        {
            try
            {
                string? dbPath = null;
                string? dir = AppDomain.CurrentDomain.BaseDirectory;
                while (dir != null)
                {
                    string candidate = Path.Combine(dir, "resources", "research", "EdaDeepResearchDatabase.json");
                    if (File.Exists(candidate))
                    {
                        dbPath = candidate;
                        break;
                    }
                    candidate = Path.Combine(dir, "EdaDeepResearchDatabase.json");
                    if (File.Exists(candidate))
                    {
                        dbPath = candidate;
                        break;
                    }
                    dir = Path.GetDirectoryName(dir);
                }

                if (dbPath != null && File.Exists(dbPath))
                {
                    string json = File.ReadAllText(dbPath);
                    var loaded = JsonSerializer.Deserialize<ResearchDatabaseContent>(json);
                    if (loaded != null)
                    {
                        Content = loaded;
                        IsLoaded = true;
                    }
                }
            }
            catch (Exception)
            {
                // Graceful fallback for design-time and tests
                IsLoaded = false;
            }
        }
    }
}
