using System;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    public class OpAmp : Component
    {
        public OpAmp(string designator, string subcircuitName = "LM358") : base(designator, subcircuitName)
        {
            if (!designator.StartsWith("X", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("OpAmp designator must start with 'X' (subcircuit).", nameof(designator));

            RegisterPin("IN+", 1); // IN+
            RegisterPin("IN-", 2); // IN-
            RegisterPin("V+", 3);  // V+
            RegisterPin("V-", 4);  // V-
            RegisterPin("OUT", 5); // OUT
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            string ninp = schematic.GetNetNameForPin(pins[0]);
            string ninn = schematic.GetNetNameForPin(pins[1]);
            string nvp = schematic.GetNetNameForPin(pins[2]);
            string nvn = schematic.GetNetNameForPin(pins[3]);
            string nout = schematic.GetNetNameForPin(pins[4]);

            // Syntax: X[Name] N1 N2 N3 N4 N5 SubcircuitName
            return $"{Designator} {ninp} {ninn} {nvp} {nvn} {nout} {Value}";
        }
    }
}
