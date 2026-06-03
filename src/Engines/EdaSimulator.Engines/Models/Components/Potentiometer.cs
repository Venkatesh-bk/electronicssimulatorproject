using System;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    /// <summary>
    /// Represents a three-terminal Potentiometer with a tunable wiper position.
    /// </summary>
    public class Potentiometer : Component
    {
        private double _wiperPosition = 0.5; // 0.0 to 1.0

        public double WiperPosition
        {
            get => _wiperPosition;
            set => _wiperPosition = System.Math.Max(0.0, System.Math.Min(1.0, value));
        }

        public Potentiometer(string designator, string value = "10k") : base(designator, value)
        {
            if (!designator.StartsWith("POT", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Potentiometer designator must start with 'POT'.", nameof(designator));

            RegisterPin("1", 1);
            RegisterPin("Wiper", 2);
            RegisterPin("2", 3);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            var pin1 = pins.FirstOrDefault(p => p.Name == "1");
            var pinWiper = pins.FirstOrDefault(p => p.Name == "Wiper");
            var pin2 = pins.FirstOrDefault(p => p.Name == "2");

            string net1 = pin1 != null ? schematic.GetNetNameForPin(pin1) : "0";
            string netWiper = pinWiper != null ? schematic.GetNetNameForPin(pinWiper) : "0";
            string net2 = pin2 != null ? schematic.GetNetNameForPin(pin2) : "0";

            double rTotal = ParseSpiceValue(Value);
            double rA = System.Math.Max(0.001, rTotal * (1.0 - WiperPosition));
            double rB = System.Math.Max(0.001, rTotal * WiperPosition);

            // Return two resistor models representing the wiper partition
            return $"R_POT_A_{Designator} {net1} {netWiper} {rA:F3}\n" +
                   $"R_POT_B_{Designator} {netWiper} {net2} {rB:F3}";
        }

        private double ParseSpiceValue(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return 10000.0;
            string clean = val.Trim().ToLower();
            double multiplier = 1.0;

            if (clean.EndsWith("meg")) { multiplier = 1e6; clean = clean.Substring(0, clean.Length - 3); }
            else if (clean.EndsWith("k")) { multiplier = 1e3; clean = clean.Substring(0, clean.Length - 1); }
            else if (clean.EndsWith("m")) { multiplier = 1e-3; clean = clean.Substring(0, clean.Length - 1); }
            else if (clean.EndsWith("u") || clean.EndsWith("µ")) { multiplier = 1e-6; clean = clean.Substring(0, clean.Length - 1); }
            else if (clean.EndsWith("n")) { multiplier = 1e-9; clean = clean.Substring(0, clean.Length - 1); }
            else if (clean.EndsWith("p")) { multiplier = 1e-12; clean = clean.Substring(0, clean.Length - 1); }
            else if (clean.EndsWith("g")) { multiplier = 1e9; clean = clean.Substring(0, clean.Length - 1); }

            while (clean.Length > 0 && !char.IsDigit(clean[clean.Length - 1]) && clean[clean.Length - 1] != '.')
            {
                clean = clean.Substring(0, clean.Length - 1);
            }

            if (double.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed * multiplier;
            }
            return 10000.0;
        }
    }
}
