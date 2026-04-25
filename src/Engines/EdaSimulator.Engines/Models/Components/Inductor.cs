using System;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    /// <summary>
    /// Represents an ideal linear inductor for SPICE simulation.
    /// </summary>
    public class Inductor : Component
    {
        public Inductor(string designator, string value) : base(designator, value)
        {
            if (!designator.StartsWith("L", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Inductor designator must start with 'L'.", nameof(designator));

            RegisterPin("1", 1);
            RegisterPin("2", 2);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string net1 = schematic.GetNetNameForPin(pins[0]);
            string net2 = schematic.GetNetNameForPin(pins[1]);

            // SPICE format for inductor: L<name> <node1> <node2> <value>
            return $"{Designator} {net1} {net2} {Value}";
        }
    }
}
