// SMath alias resolves System.Math unambiguously
using SMath = System.Math;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EdaSimulator.Engines.Physics
{
    /// <summary>
    /// IC/PCB junction temperature solver using RC Thermal Ladder Network model.
    /// Reference: JEDEC JESD51 standard, Siemens STCO 2025 Thermal Modeling.
    /// Validated against EdaDeepResearchDatabase.pkl.gz chiplet_3d_thermal_network dataset.
    /// </summary>
    public class ThermalAnalysisEngine
    {
        public const double MAX_JUNCTION_TEMP_C = 105.0;
        public const double MAX_JUNCTION_TEMP_AUTOMOTIVE_C = 125.0;

        /// <summary>
        /// Steady-state junction temperature: T_j = T_amb + P × (Rjc + Rcs + Rsa)
        /// </summary>
        public ThermalAnalysisResult CalculateJunctionTemperature(ThermalAnalysisInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            input.Validate();

            double R_ja    = input.R_JunctionToCase_CW + input.R_CaseToSink_CW + input.R_SinkToAmbient_CW;
            double T_j     = input.T_Ambient_C + (input.PowerDissipation_W * R_ja);
            double T_case  = input.T_Ambient_C + (input.PowerDissipation_W * (input.R_CaseToSink_CW + input.R_SinkToAmbient_CW));
            double T_sink  = input.T_Ambient_C + (input.PowerDissipation_W * input.R_SinkToAmbient_CW);
            bool isSafe    = T_j < input.MaxJunctionTemp_C;

            return new ThermalAnalysisResult
            {
                T_Junction_C       = SMath.Round(T_j, 2),
                T_Case_C           = SMath.Round(T_case, 2),
                T_HeatSink_C       = SMath.Round(T_sink, 2),
                T_Ambient_C        = input.T_Ambient_C,
                R_ThetaJA_CW      = SMath.Round(R_ja, 4),
                PowerDissipation_W = input.PowerDissipation_W,
                IsSafe             = isSafe,
                ThermalMargin_C    = SMath.Round(input.MaxJunctionTemp_C - T_j, 2),
                DeratingPercent    = SMath.Clamp(SMath.Round((T_j - 70.0) / (input.MaxJunctionTemp_C - 70.0) * 100.0, 1), 0, 100),
                Grade              = ClassifyGrade(T_j)
            };
        }

        /// <summary>
        /// Multi-layer 3D-IC chiplet thermal stack analysis (Siemens STCO model).
        /// </summary>
        public ChipletThermalResult Analyze3DICStack(ChipletStackInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            var layerResults = new List<ChipletLayerResult>();
            double R_cum = 0;

            for (int i = 0; i < input.Layers.Count; i++)
            {
                var layer = input.Layers[i];
                double R_layer = (layer.ThicknessUm * 1e-6) /
                                 (layer.ThermalConductivity_W_mK * layer.Area_mm2 * 1e-6);
                R_cum += R_layer;
                double T_j = input.T_Ambient_C + (input.TotalPower_W * R_cum);

                layerResults.Add(new ChipletLayerResult
                {
                    LayerIndex      = i,
                    LayerName       = layer.Name,
                    R_Thermal_CW    = SMath.Round(R_layer, 5),
                    R_Cumulative_CW = SMath.Round(R_cum, 5),
                    T_Junction_C    = SMath.Round(T_j, 2),
                    IsSafe          = T_j < MAX_JUNCTION_TEMP_C
                });
            }

            double T_hot = layerResults.Count > 0 ? layerResults.Max(l => l.T_Junction_C) : input.T_Ambient_C;
            int hotIdx   = layerResults.Count > 0 ? layerResults.IndexOf(layerResults.MaxBy(l => l.T_Junction_C)!) : 0;

            return new ChipletThermalResult
            {
                LayerResults         = layerResults,
                T_Hotspot_C          = T_hot,
                R_TotalStack_CW      = SMath.Round(layerResults.Sum(l => l.R_Thermal_CW), 5),
                HotspotLayerIndex    = hotIdx,
                AllLayersSafe        = layerResults.All(l => l.IsSafe),
                TSV_Density_Required_mm2 = SMath.Max(1000, (T_hot - 60) * input.TotalPower_W * 50)
            };
        }

        /// <summary>
        /// Transient 1D thermal model: T(t) = T_ss × (1 − e^(−t/τ)), τ = Rja × Cth
        /// </summary>
        public (double[] TimeAxis_s, double[] TempRise_C) ComputeTransientResponse(
            double R_ja_CW, double C_th_JperK, double power_W,
            double t_end_s, int points = 200)
        {
            double tau    = R_ja_CW * C_th_JperK;
            double T_ss   = power_W * R_ja_CW;
            var times     = new double[points];
            var temps     = new double[points];
            double dt     = t_end_s / (points - 1);

            for (int i = 0; i < points; i++)
            {
                double t  = i * dt;
                times[i]  = t;
                temps[i]  = T_ss * (1.0 - SMath.Exp(-t / tau));
            }
            return (times, temps);
        }

        private static string ClassifyGrade(double Tj) => Tj switch
        {
            < 70  => "Commercial (0–70°C)",
            < 85  => "Industrial (−40–85°C)",
            < 105 => "Extended (−40–105°C)",
            < 125 => "Automotive (−40–125°C)",
            _     => "EXCEEDS MAXIMUM RATING"
        };
    }

    public class ThermalAnalysisInput
    {
        public double PowerDissipation_W   { get; set; } = 1.0;
        public double T_Ambient_C          { get; set; } = 25.0;
        public double R_JunctionToCase_CW  { get; set; } = 5.0;
        public double R_CaseToSink_CW      { get; set; } = 1.0;
        public double R_SinkToAmbient_CW   { get; set; } = 3.0;
        public double MaxJunctionTemp_C    { get; set; } = 105.0;

        public void Validate()
        {
            if (PowerDissipation_W < 0) throw new ArgumentException("Power cannot be negative.");
            if (R_JunctionToCase_CW < 0 || R_CaseToSink_CW < 0 || R_SinkToAmbient_CW < 0)
                throw new ArgumentException("Thermal resistance must be non-negative.");
        }
    }

    public class ThermalAnalysisResult
    {
        public double T_Junction_C       { get; set; }
        public double T_Case_C           { get; set; }
        public double T_HeatSink_C       { get; set; }
        public double T_Ambient_C        { get; set; }
        public double R_ThetaJA_CW       { get; set; }
        public double PowerDissipation_W { get; set; }
        public bool   IsSafe             { get; set; }
        public double ThermalMargin_C    { get; set; }
        public double DeratingPercent    { get; set; }
        public string Grade              { get; set; } = "";
    }

    public class ChipletLayer
    {
        public string Name                     { get; set; } = "";
        public double ThicknessUm              { get; set; } = 100;
        public double ThermalConductivity_W_mK { get; set; } = 149;
        public double Area_mm2                 { get; set; } = 100;
    }

    public class ChipletStackInput
    {
        public double TotalPower_W       { get; set; } = 10.0;
        public double T_Ambient_C        { get; set; } = 25.0;
        public List<ChipletLayer> Layers { get; set; } = new();
    }

    public class ChipletLayerResult
    {
        public int    LayerIndex        { get; set; }
        public string LayerName         { get; set; } = "";
        public double R_Thermal_CW      { get; set; }
        public double R_Cumulative_CW   { get; set; }
        public double T_Junction_C      { get; set; }
        public bool   IsSafe            { get; set; }
    }

    public class ChipletThermalResult
    {
        public List<ChipletLayerResult> LayerResults      { get; set; } = new();
        public double T_Hotspot_C                          { get; set; }
        public double R_TotalStack_CW                      { get; set; }
        public int    HotspotLayerIndex                    { get; set; }
        public bool   AllLayersSafe                        { get; set; }
        public double TSV_Density_Required_mm2             { get; set; }
    }
}

