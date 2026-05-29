using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    /// <summary>
    /// Base class for mathematical/signal-processing block components.
    /// These are modeled as SPICE subcircuits or ABM sources in simulation.
    /// </summary>
    public abstract class BlockComponent : Component
    {
        protected BlockComponent(string designator, string value) : base(designator, value)
        {
        }
    }

    /// <summary>
    /// Mathematical gain block component.
    /// SPICE representation: XG1 IN OUT BlockGain params: gain=5.0
    /// </summary>
    public class BlockGainComponent : BlockComponent
    {
        public BlockGainComponent(string designator, string value = "1.0") : base(designator, value)
        {
            if (!designator.StartsWith("XG", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Gain block designator must start with 'XG'.", nameof(designator));

            RegisterPin("IN", 1);
            RegisterPin("OUT", 2);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string netIn = schematic.GetNetNameForPin(pins[0]);
            string netOut = schematic.GetNetNameForPin(pins[1]);

            return $"{Designator} {netIn} {netOut} BlockGain params: gain={Value}";
        }
    }

    /// <summary>
    /// Mathematical integrator block component.
    /// SPICE representation: XI1 IN OUT BlockIntegrator params: ic=0.0
    /// </summary>
    public class BlockIntegratorComponent : BlockComponent
    {
        public BlockIntegratorComponent(string designator, string value = "0.0") : base(designator, value)
        {
            if (!designator.StartsWith("XI", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Integrator block designator must start with 'XI'.", nameof(designator));

            RegisterPin("IN", 1);
            RegisterPin("OUT", 2);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string netIn = schematic.GetNetNameForPin(pins[0]);
            string netOut = schematic.GetNetNameForPin(pins[1]);

            return $"{Designator} {netIn} {netOut} BlockIntegrator params: ic={Value}";
        }
    }

    /// <summary>
    /// Mathematical 2-input summing junction component.
    /// SPICE representation: XS1 IN1 IN2 OUT BlockSum2 params: sign1=1 sign2=-1
    /// </summary>
    public class BlockSumComponent : BlockComponent
    {
        public BlockSumComponent(string designator, string value = "+-") : base(designator, value)
        {
            if (!designator.StartsWith("XS", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Sum block designator must start with 'XS'.", nameof(designator));

            RegisterPin("IN1", 1);
            RegisterPin("IN2", 2);
            RegisterPin("OUT", 3);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string netIn1 = schematic.GetNetNameForPin(pins[0]);
            string netIn2 = schematic.GetNetNameForPin(pins[1]);
            string netOut = schematic.GetNetNameForPin(pins[2]);

            // Parse signs from value (e.g. "+-" or "++" or "+ -")
            double sign1 = 1.0;
            double sign2 = 1.0;
            
            string signs = Value.Replace(" ", "");
            if (signs.Length >= 1 && signs[0] == '-') sign1 = -1.0;
            if (signs.Length >= 2 && signs[1] == '-') sign2 = -1.0;

            return $"{Designator} {netIn1} {netIn2} {netOut} BlockSum2 params: sign1={sign1.ToString(CultureInfo.InvariantCulture)} sign2={sign2.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    /// <summary>
    /// Mathematical source block component.
    /// Supported Values:
    /// - "Constant 5"
    /// - "Sine 0 1 1k 0" (offset, amplitude, frequency, phase)
    /// - "Step 0 1 1" (offset, step_val, step_time)
    /// </summary>
    public class BlockSourceComponent : BlockComponent
    {
        public BlockSourceComponent(string designator, string value = "Constant 1.0") : base(designator, value)
        {
            if (!designator.StartsWith("XSO", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Source block designator must start with 'XSO'.", nameof(designator));

            RegisterPin("OUT", 1);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string netOut = schematic.GetNetNameForPin(pins[0]);

            string[] tokens = Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string type = tokens.Length > 0 ? tokens[0].ToLowerInvariant() : "constant";

            if (type == "sine")
            {
                string offset = tokens.Length > 1 ? tokens[1] : "0";
                string amp = tokens.Length > 2 ? tokens[2] : "1";
                string freq = tokens.Length > 3 ? tokens[3] : "1k";
                string phase = tokens.Length > 4 ? tokens[4] : "0";
                return $"{Designator} {netOut} BlockSourceSine params: offset={offset} amp={amp} freq={freq} phase={phase}";
            }
            else if (type == "step")
            {
                string offset = tokens.Length > 1 ? tokens[1] : "0";
                string stepval = tokens.Length > 2 ? tokens[2] : "1";
                string steptime = tokens.Length > 3 ? tokens[3] : "1";
                return $"{Designator} {netOut} BlockSourceStep params: offset={offset} stepval={stepval} steptime={steptime}";
            }
            else
            {
                // Default: Constant
                string val = tokens.Length > 1 ? tokens[1] : "1.0";
                return $"{Designator} {netOut} BlockSourceConst params: val={val}";
            }
        }
    }

    /// <summary>
    /// Linear S-Domain Laplace Transfer Function block component.
    /// SPICE representation: E1 OUT 0 laplace {V(IN)} = { 1 / (s + 1) }
    /// Value format: "1 / 1 1" (numerator coeffs / denominator coeffs, low-to-high powers)
    /// </summary>
    public class BlockTransferFunctionComponent : BlockComponent
    {
        public BlockTransferFunctionComponent(string designator, string value = "1 / 1 1") : base(designator, value)
        {
            if (!designator.StartsWith("XTF", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Transfer function block designator must start with 'XTF'.", nameof(designator));

            RegisterPin("IN", 1);
            RegisterPin("OUT", 2);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string netIn = schematic.GetNetNameForPin(pins[0]);
            string netOut = schematic.GetNetNameForPin(pins[1]);

            // Parse numerator and denominator coefficients
            string[] parts = Value.Split('/');
            double[] numCoeffs = new[] { 1.0 };
            double[] denCoeffs = new[] { 1.0, 1.0 };

            if (parts.Length > 0)
            {
                var numPart = parts[0].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (numPart.Length > 0)
                    numCoeffs = numPart.Select(c => double.TryParse(c, NumberStyles.Any, CultureInfo.InvariantCulture, out double val) ? val : 1.0).ToArray();
            }
            if (parts.Length > 1)
            {
                var denPart = parts[1].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (denPart.Length > 0)
                    denCoeffs = denPart.Select(c => double.TryParse(c, NumberStyles.Any, CultureInfo.InvariantCulture, out double val) ? val : 1.0).ToArray();
            }

            string numPoly = FormatPolynomial(numCoeffs);
            string denPoly = FormatPolynomial(denCoeffs);

            // SPICE Laplace format: E<name> <out_pos> <out_neg> laplace {v(<in_pos>, <in_neg>)} = { <expr> }
            return $"E{Designator.Substring(1)} {netOut} 0 laplace {{V({netIn})}} = {{ {numPoly} / ({denPoly}) }}";
        }

        private static string FormatPolynomial(double[] coeffs)
        {
            var terms = new List<string>();
            for (int i = coeffs.Length - 1; i >= 0; i--)
            {
                double val = coeffs[i];
                if (System.Math.Abs(val) < 1e-15) continue;

                if (i == 0)
                    terms.Add(val.ToString(CultureInfo.InvariantCulture));
                else if (i == 1)
                    terms.Add(val == 1.0 ? "s" : $"{val.ToString(CultureInfo.InvariantCulture)}*s");
                else
                    terms.Add(val == 1.0 ? $"s^{i}" : $"{val.ToString(CultureInfo.InvariantCulture)}*s^{i}");
            }
            return terms.Count == 0 ? "0" : string.Join(" + ", terms);
        }
    }
}
