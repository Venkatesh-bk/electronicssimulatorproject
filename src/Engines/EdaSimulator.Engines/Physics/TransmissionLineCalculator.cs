using System;
using System.Collections.Generic;

// SMath alias resolves System.Math unambiguously (avoids EdaSimulator.Engines.Math namespace collision)
using SMath = System.Math;

namespace EdaSimulator.Engines.Physics
{
    /// <summary>
    /// RF/Microwave Transmission Line Calculator.
    /// Implements closed-form design equations for the three most common PCB TL geometries:
    ///   - Microstrip (IPC-2141A)
    ///   - Stripline (Wadell 1991)
    ///   - Coaxial (standard coax formula)
    /// Also provides S-parameter utilities and Smith Chart impedance conversion.
    /// Reference: Pozar "Microwave Engineering" 4th Ed, Wadell "Transmission Line Design Handbook".
    /// </summary>
    public static class TransmissionLineCalculator
    {
        private const double C0  = 2.998e8;     // Speed of light in vacuum (m/s)
        private const double MU0 = 1.2566370614e-6; // 4π × 10⁻⁷ H/m

        // ──────────────────────────────────────────────────────────────────────────────
        // MICROSTRIP (IPC-2141A closed-form approximation)
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates microstrip characteristic impedance Z0 and effective permittivity.
        /// Valid for W/H > 0.1. Accuracy: ±1% for 0.1 ≤ W/H ≤ 20.
        /// </summary>
        public static TransmissionLineResult CalculateMicrostrip(
            double width_mm, double height_mm, double er, double freq_GHz = 1.0)
        {
            double W = width_mm;
            double H = height_mm;
            double u = W / H;

            double er_eff, Z0;

            if (u <= 1)
            {
                er_eff = (er + 1) / 2.0 + (er - 1) / 2.0 * (1.0 / SMath.Sqrt(1 + 12 / u) + 0.04 * SMath.Pow(1 - u, 2));
                Z0 = (60.0 / SMath.Sqrt(er_eff)) * SMath.Log(8 / u + u / 4.0);
            }
            else
            {
                er_eff = (er + 1) / 2.0 + (er - 1) / 2.0 / SMath.Sqrt(1 + 12 / u);
                Z0 = 120 * SMath.PI / (SMath.Sqrt(er_eff) * (u + 1.393 + 0.667 * SMath.Log(u + 1.444)));
            }

            double lambda_g = (C0 / (freq_GHz * 1e9)) / SMath.Sqrt(er_eff) * 1000; // mm
            double td_ps_mm = SMath.Sqrt(er_eff) / C0 * 1e12 * 1000;                // ps/mm
            double vp = C0 / SMath.Sqrt(er_eff);                                    // m/s

            return new TransmissionLineResult
            {
                Topology          = "Microstrip",
                Z0_Ohm            = SMath.Round(Z0, 2),
                Er_Effective      = SMath.Round(er_eff, 4),
                PropagationDelay_ps_mm = SMath.Round(td_ps_mm, 3),
                PhaseVelocity_m_s = SMath.Round(vp, 0),
                GuidedWavelength_mm = SMath.Round(lambda_g, 3),
                Width_mm          = width_mm,
                Height_mm         = height_mm,
                Er_Substrate      = er,
                Frequency_GHz     = freq_GHz
            };
        }

        /// <summary>
        /// Reverse-solves microstrip width for a target impedance.
        /// Uses Hammerstad & Jensen synthesis equations.
        /// </summary>
        public static double SynthesizeMicrostripWidth(double Z0_target, double H_mm, double er)
        {
            double A = Z0_target / 60.0 * SMath.Sqrt((er + 1) / 2.0) + (er - 1) / (er + 1) * (0.23 + 0.11 / er);
            double B = 377 * SMath.PI / (2 * Z0_target * SMath.Sqrt(er));

            // Choose which region we're in
            double W_H_narrowstrip = 8 * SMath.Exp(A) / (SMath.Exp(2 * A) - 2);
            double W_H_widestrip   = 2 / SMath.PI * (B - 1 - SMath.Log(2 * B - 1) + (er - 1) / (2 * er) * (SMath.Log(B - 1) + 0.39 - 0.61 / er));

            double W_H = W_H_narrowstrip <= 2 ? W_H_narrowstrip : W_H_widestrip;
            return SMath.Round(W_H * H_mm, 4);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // STRIPLINE (Wadell 1991)
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates centered stripline Z0. Valid for W/B < 0.85 and T/B < 0.1.
        /// </summary>
        public static TransmissionLineResult CalculateStripline(
            double width_mm, double b_mm, double er, double thickness_mm = 0.035, double freq_GHz = 1.0)
        {
            double W    = width_mm;
            double B    = b_mm;
            double T    = thickness_mm;

            // Effective width correction for finite thickness
            double dW   = T / SMath.PI * (1 + SMath.Log(4 * SMath.E / SMath.Tanh(SMath.Sqrt(6.27 * T / B))));
            double We   = W + dW;

            // Z0 formula
            double Z0;
            if (We / B <= 0.85)
            {
                // Narrow strip approximation
                Z0 = (60.0 / SMath.Sqrt(er)) * SMath.Log(4 * B / (SMath.PI * We));
            }
            else
            {
                // Wide strip formula
                double x = We / B;
                Z0 = (94.15 / SMath.Sqrt(er)) / (We / B + 0.441);
            }

            double td = SMath.Sqrt(er) / C0 * 1e12 * 1000; // ps/mm
            double vp = C0 / SMath.Sqrt(er);

            return new TransmissionLineResult
            {
                Topology               = "Stripline",
                Z0_Ohm                 = SMath.Round(SMath.Abs(Z0), 2),
                Er_Effective           = er,  // Stripline is fully embedded → er_eff = er
                PropagationDelay_ps_mm = SMath.Round(td, 3),
                PhaseVelocity_m_s      = SMath.Round(vp, 0),
                GuidedWavelength_mm    = SMath.Round((C0 / (freq_GHz * 1e9)) / SMath.Sqrt(er) * 1000, 3),
                Width_mm               = width_mm,
                Height_mm              = b_mm,
                Er_Substrate           = er,
                Frequency_GHz          = freq_GHz
            };
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // COAXIAL
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Characteristic impedance of coaxial cable. Z0 = (60/√er) × ln(D/d)</summary>
        public static TransmissionLineResult CalculateCoaxial(
            double inner_dia_mm, double outer_dia_mm, double er, double freq_GHz = 1.0)
        {
            if (inner_dia_mm >= outer_dia_mm)
                throw new ArgumentException("Inner diameter must be less than outer diameter.");

            double Z0 = (60.0 / SMath.Sqrt(er)) * SMath.Log(outer_dia_mm / inner_dia_mm);
            double td = SMath.Sqrt(er) / C0 * 1e12 * 1000;

            return new TransmissionLineResult
            {
                Topology               = "Coaxial",
                Z0_Ohm                 = SMath.Round(Z0, 2),
                Er_Effective           = er,
                PropagationDelay_ps_mm = SMath.Round(td, 3),
                PhaseVelocity_m_s      = SMath.Round(C0 / SMath.Sqrt(er), 0),
                GuidedWavelength_mm    = SMath.Round((C0 / (freq_GHz * 1e9)) / SMath.Sqrt(er) * 1000, 3),
                Width_mm               = inner_dia_mm,
                Height_mm              = outer_dia_mm,
                Er_Substrate           = er,
                Frequency_GHz          = freq_GHz
            };
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // SMITH CHART UTILITIES
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Converts complex S11 (magnitude + phase in degrees) to a normalized Smith Chart point.
        /// Reflection coefficient Γ = S11, normalized impedance z = (1+Γ)/(1−Γ).
        /// Returns (r_normalized, x_normalized) for plotting.
        /// </summary>
        public static (double r, double x) S11ToNormalizedImpedance(double s11_mag, double s11_phase_deg)
        {
            double phi = s11_phase_deg * SMath.PI / 180.0;
            double Gr  = s11_mag * SMath.Cos(phi);   // Real part of Γ
            double Gi  = s11_mag * SMath.Sin(phi);   // Imaginary part of Γ

            // z = (1 + Γ) / (1 - Γ) in complex arithmetic
            double denom = (1 - Gr) * (1 - Gr) + Gi * Gi;
            double r = ((1 + Gr) * (1 - Gr) + Gi * Gi) / denom;
            double x = (2 * Gi) / denom;

            return (SMath.Round(r, 4), SMath.Round(x, 4));
        }

        /// <summary>
        /// Converts normalized Smith Chart point (r, x) to actual impedance at Z0.
        /// </summary>
        public static (double R_ohm, double X_ohm) NormalizedToImpedance(double r, double x, double Z0 = 50.0)
            => (r * Z0, x * Z0);

        /// <summary>
        /// Generates a set of Smith Chart points from S-parameter sweep data.
        /// </summary>
        public static List<SmithChartPoint> GenerateSmithChartPoints(
            double[] freq_Hz, float[] s11_mag, float[] s11_phase_deg)
        {
            if (freq_Hz.Length != s11_mag.Length || freq_Hz.Length != s11_phase_deg.Length)
                throw new ArgumentException("Frequency, S11 magnitude, and S11 phase arrays must have the same length.");

            var points = new List<SmithChartPoint>(freq_Hz.Length);
            for (int i = 0; i < freq_Hz.Length; i++)
            {
                var (r, x) = S11ToNormalizedImpedance(s11_mag[i], s11_phase_deg[i]);
                var (R, X) = NormalizedToImpedance(r, x);
                points.Add(new SmithChartPoint
                {
                    Frequency_Hz     = freq_Hz[i],
                    S11_Magnitude    = s11_mag[i],
                    S11_Phase_deg    = s11_phase_deg[i],
                    NormalizedR      = r,
                    NormalizedX      = x,
                    Impedance_R_Ohm  = R,
                    Impedance_X_Ohm  = X,
                    VSWR             = SMath.Round((1 + s11_mag[i]) / SMath.Max(1e-9, 1 - s11_mag[i]), 3),
                    ReturnLoss_dB    = SMath.Round(-20 * SMath.Log10(SMath.Max(1e-9, s11_mag[i])), 2)
                });
            }
            return points;
        }
    }

    public class TransmissionLineResult
    {
        public string Topology               { get; set; } = "";
        public double Z0_Ohm                 { get; set; }
        public double Er_Effective           { get; set; }
        public double PropagationDelay_ps_mm { get; set; }
        public double PhaseVelocity_m_s      { get; set; }
        public double GuidedWavelength_mm    { get; set; }
        public double Width_mm               { get; set; }
        public double Height_mm              { get; set; }
        public double Er_Substrate           { get; set; }
        public double Frequency_GHz          { get; set; }
    }

    public class SmithChartPoint
    {
        public double Frequency_Hz     { get; set; }
        public double S11_Magnitude    { get; set; }
        public double S11_Phase_deg    { get; set; }
        public double NormalizedR      { get; set; }
        public double NormalizedX      { get; set; }
        public double Impedance_R_Ohm  { get; set; }
        public double Impedance_X_Ohm  { get; set; }
        public double VSWR             { get; set; }
        public double ReturnLoss_dB    { get; set; }
    }
}
