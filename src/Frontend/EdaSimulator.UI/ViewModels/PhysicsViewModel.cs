using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdaSimulator.Engines.Physics;
using EdaSimulator.Engines.PCB;
using EdaSimulator.Engines.Models;

namespace EdaSimulator.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the Phase 6 Physics tab (Thermal + RF/Transmission Line).
    /// </summary>
    public partial class PhysicsViewModel : ObservableObject
    {
        private readonly ThermalAnalysisEngine _thermalEngine = new();

        // ── Thermal Analysis ─────────────────────────────────────────────────────────

        [ObservableProperty] private double _power_W          = 2.0;
        [ObservableProperty] private double _tAmbient_C       = 25.0;
        [ObservableProperty] private double _rJc_CW           = 5.0;
        [ObservableProperty] private double _rCs_CW           = 1.0;
        [ObservableProperty] private double _rSa_CW           = 8.0;
        [ObservableProperty] private double _maxJunction_C    = 105.0;

        [ObservableProperty] private string _thermalResult    = "Enter parameters and click Analyze.";
        [ObservableProperty] private bool   _thermalIsSafe    = true;
        [ObservableProperty] private double _tJunction_C;

        [RelayCommand]
        private void RunThermalAnalysis()
        {
            try
            {
                var input = new ThermalAnalysisInput
                {
                    PowerDissipation_W   = Power_W,
                    T_Ambient_C          = TAmbient_C,
                    R_JunctionToCase_CW  = RJc_CW,
                    R_CaseToSink_CW      = RCs_CW,
                    R_SinkToAmbient_CW   = RSa_CW,
                    MaxJunctionTemp_C    = MaxJunction_C
                };

                var res = _thermalEngine.CalculateJunctionTemperature(input);
                TJunction_C   = res.T_Junction_C;
                ThermalIsSafe = res.IsSafe;
                ThermalResult =
                    $"JUNCTION TEMPERATURE ANALYSIS\n" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                    $"T_Junction   = {res.T_Junction_C:F1} °C   {(res.IsSafe ? "[SAFE]" : "[EXCEEDS LIMIT!]")}\n" +
                    $"T_Case       = {res.T_Case_C:F1} °C\n" +
                    $"T_HeatSink   = {res.T_HeatSink_C:F1} °C\n" +
                    $"T_Ambient    = {res.T_Ambient_C:F1} °C\n\n" +
                    $"R_theta_JA   = {res.R_ThetaJA_CW:F3} °C/W\n" +
                    $"Power        = {res.PowerDissipation_W:F2} W\n" +
                    $"Thermal Margin = {res.ThermalMargin_C:F1} °C\n" +
                    $"Derating     = {res.DeratingPercent:F1}%\n\n" +
                    $"Grade Classification: {res.Grade}\n\n" +
                    $"JEDEC JESD51 Standard | RC Thermal Ladder Model";
            }
            catch (Exception ex)
            {
                ThermalResult = $"Error: {ex.Message}";
            }
        }

        // ── Transmission Line Calculator ─────────────────────────────────────────────

        [ObservableProperty] private string _tlTopology       = "Microstrip";
        [ObservableProperty] private double _tlWidth_mm       = 1.5;
        [ObservableProperty] private double _tlHeight_mm      = 1.6;
        [ObservableProperty] private double _tlEr             = 4.3;
        [ObservableProperty] private double _tlFreq_GHz       = 1.0;
        [ObservableProperty] private string _tlResult         = "Enter dimensions and click Calculate.";
        [ObservableProperty] private double _tlTargetZ0       = 50.0;

        [RelayCommand]
        private void CalculateTxLine()
        {
            try
            {
                TransmissionLineResult result;
                switch (TlTopology)
                {
                    case "Stripline":
                        result = TransmissionLineCalculator.CalculateStripline(TlWidth_mm, TlHeight_mm, TlEr, 0.035, TlFreq_GHz);
                        break;
                    case "Coaxial":
                        result = TransmissionLineCalculator.CalculateCoaxial(TlWidth_mm, TlHeight_mm, TlEr, TlFreq_GHz);
                        break;
                    default: // Microstrip
                        result = TransmissionLineCalculator.CalculateMicrostrip(TlWidth_mm, TlHeight_mm, TlEr, TlFreq_GHz);

                        // Also synthesize: what width gives 50Ω?
                        double w50 = TransmissionLineCalculator.SynthesizeMicrostripWidth(TlTargetZ0, TlHeight_mm, TlEr);
                        TlResult =
                            $"MICROSTRIP IMPEDANCE ANALYSIS\n" +
                            $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                            $"Z₀                = {result.Z0_Ohm:F2} Ω\n" +
                            $"εr_eff            = {result.Er_Effective:F4}\n" +
                            $"Prop. Delay       = {result.PropagationDelay_ps_mm:F3} ps/mm\n" +
                            $"Phase Velocity    = {result.PhaseVelocity_m_s / 1e8:F3} × 10⁸ m/s\n" +
                            $"λ_g @ {result.Frequency_GHz}GHz       = {result.GuidedWavelength_mm:F2} mm\n\n" +
                            $"SYNTHESIS — Width for {TlTargetZ0:F0}Ω:\n" +
                            $"  W = {w50:F4} mm  (H={TlHeight_mm}mm, εr={TlEr})\n\n" +
                            $"IPC-2141A | Pozar 'Microwave Engineering' 4th Ed.";
                        return;
                }

                TlResult =
                    $"{result.Topology.ToUpper()} ANALYSIS\n" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                    $"Z₀             = {result.Z0_Ohm:F2} Ω\n" +
                    $"εr_eff         = {result.Er_Effective:F4}\n" +
                    $"Prop. Delay    = {result.PropagationDelay_ps_mm:F3} ps/mm\n" +
                    $"Phase Velocity = {result.PhaseVelocity_m_s / 1e8:F3} × 10⁸ m/s\n" +
                    $"λ_g @ {result.Frequency_GHz}GHz    = {result.GuidedWavelength_mm:F2} mm\n\n" +
                    $"Wadell 1991 | Pozar Microwave Engineering";
            }
            catch (Exception ex)
            {
                TlResult = $"Error: {ex.Message}";
            }
        }
    }
}
