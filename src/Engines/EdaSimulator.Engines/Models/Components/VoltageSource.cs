using System;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    /// <summary>
    /// Represents an ideal independent voltage source for SPICE simulation.
    /// </summary>
    public class VoltageSource : Component
    {
        public VoltageSource(string designator, string value) : base(designator, value)
        {
            if (!designator.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("VoltageSource designator must start with 'V'.", nameof(designator));

            // SPICE node ordering for voltage sources: N+ N-
            RegisterPin("+", 1);
            RegisterPin("-", 2);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string netPositive = schematic.GetNetNameForPin(pins[0]);
            string netNegative = schematic.GetNetNameForPin(pins[1]);

            // SPICE format for voltage source: V<name> <N+ node> <N- node> <value> 
            // example: V1 N001 0 DC 5V
            return $"{Designator} {netPositive} {netNegative} {Value}";
        }
    }
}
