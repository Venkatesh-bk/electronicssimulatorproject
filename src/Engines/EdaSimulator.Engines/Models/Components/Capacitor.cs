using System;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    /// <summary>
    /// Represents an ideal linear capacitor for SPICE simulation.
    /// </summary>
    public class Capacitor : Component
    {
        public Capacitor(string designator, string value) : base(designator, value)
        {
            if (!designator.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Capacitor designator must start with 'C'.", nameof(designator));

            RegisterPin("1", 1);
            RegisterPin("2", 2);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string net1 = schematic.GetNetNameForPin(pins[0]);
            string net2 = schematic.GetNetNameForPin(pins[1]);

            // SPICE format for capacitor: C<name> <node1> <node2> <value>
            return $"{Designator} {net1} {net2} {Value}";
        }
    }
}
