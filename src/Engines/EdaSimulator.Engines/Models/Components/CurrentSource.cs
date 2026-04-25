using System;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    /// <summary>
    /// Represents an ideal independent current source for SPICE simulation.
    /// </summary>
    public class CurrentSource : Component
    {
        public CurrentSource(string designator, string value) : base(designator, value)
        {
            if (!designator.StartsWith("I", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("CurrentSource designator must start with 'I'.", nameof(designator));

            // SPICE node ordering for current sources: N+ N-
            RegisterPin("+", 1);
            RegisterPin("-", 2);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string netPositive = schematic.GetNetNameForPin(pins[0]);
            string netNegative = schematic.GetNetNameForPin(pins[1]);

            // SPICE format for current source: I<name> <N+ node> <N- node> <value>
            // example: I1 N001 0 DC 10m
            return $"{Designator} {netPositive} {netNegative} {Value}";
        }
    }
}
