using System;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    /// <summary>
    /// Represents an interactive switch for transient and DC simulations.
    /// </summary>
    public class Switch : Component
    {
        public bool IsClosed { get; set; } = false;

        public Switch(string designator, string value = "Open") : base(designator, value)
        {
            if (!designator.StartsWith("SW", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Switch designator must start with 'SW'.", nameof(designator));

            RegisterPin("1", 1);
            RegisterPin("2", 2);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string net1 = schematic.GetNetNameForPin(pins[0]);
            string net2 = schematic.GetNetNameForPin(pins[1]);
            
            // Switch is modeled in SPICE as a dynamic resistor (1mΩ closed, 1GΩ open)
            string resistance = IsClosed ? "1m" : "1G";
            return $"R_SW_{Designator} {net1} {net2} {resistance}";
        }
    }
}
