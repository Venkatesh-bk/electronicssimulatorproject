using System;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    public class Diode : Component
    {
        public Diode(string designator, string modelName = "1N4148") : base(designator, modelName)
        {
            if (!designator.StartsWith("D", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Diode designator must start with 'D'.", nameof(designator));

            RegisterPin("A", 1); // Anode
            RegisterPin("K", 2); // Cathode
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string n1 = schematic.GetNetNameForPin(pins[0]);
            string n2 = schematic.GetNetNameForPin(pins[1]);

            // Syntax: D[Name] N+ N- ModelName
            return $"{Designator} {n1} {n2} {Value}";
        }
    }
}
