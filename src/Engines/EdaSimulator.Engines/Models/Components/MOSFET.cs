using System;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    public class MOSFET : Component
    {
        public MOSFET(string designator, string modelName = "2N7002") : base(designator, modelName)
        {
            if (!designator.StartsWith("M", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("MOSFET designator must start with 'M'.", nameof(designator));

            RegisterPin("D", 1); // Drain
            RegisterPin("G", 2); // Gate
            RegisterPin("S", 3); // Source
            RegisterPin("B", 4); // Bulk
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string nd = schematic.GetNetNameForPin(pins[0]);
            string ng = schematic.GetNetNameForPin(pins[1]);
            string ns = schematic.GetNetNameForPin(pins[2]);
            string nb = schematic.GetNetNameForPin(pins[3]);

            // Syntax: M[Name] ND NG NS NB ModelName
            return $"{Designator} {nd} {ng} {ns} {nb} {Value}";
        }
    }
}
