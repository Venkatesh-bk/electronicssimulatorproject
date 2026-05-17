using System;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    public class BJT : Component
    {
        public BJT(string designator, string modelName = "2N2222") : base(designator, modelName)
        {
            if (!designator.StartsWith("Q", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("BJT designator must start with 'Q'.", nameof(designator));

            RegisterPin("C", 1); // Collector
            RegisterPin("B", 2); // Base
            RegisterPin("E", 3); // Emitter
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string nc = schematic.GetNetNameForPin(pins[0]);
            string nb = schematic.GetNetNameForPin(pins[1]);
            string ne = schematic.GetNetNameForPin(pins[2]);

            // Syntax: Q[Name] NC NB NE ModelName
            return $"{Designator} {nc} {nb} {ne} {Value}";
        }
    }
}
