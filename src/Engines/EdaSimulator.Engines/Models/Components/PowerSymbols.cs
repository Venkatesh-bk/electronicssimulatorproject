using System;
using EdaSimulator.Engines.Models;

namespace EdaSimulator.Engines.Models.Components
{
    /// <summary>
    /// A SPICE ground reference node. When placed on the schematic, any pin wired
    /// to this symbol is automatically connected to the MasterGroundNet ("0").
    /// This is the universal SPICE ground convention — every circuit needs one.
    /// </summary>
    public class GroundSymbol : Component
    {
        public GroundSymbol(string designator = "GND1") : base(designator, "0")
        {
            RegisterPin("GND", 1);
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            // Ground symbols don't generate a SPICE element line.
            // Their effect comes from pins being wired to net "0" via WiringTool.
            return $"* GND symbol — {Designator} (net reference only)";
        }
    }

    /// <summary>
    /// A named power rail (VCC, VDD, +5V, +3.3V, +12V, etc.).
    /// Generates an ideal DC voltage source referenced to absolute ground.
    /// </summary>
    public class PowerRail : Component
    {
        /// <summary>Voltage level in volts (e.g. 5.0 for +5V).</summary>
        public double Voltage { get; set; }

        public PowerRail(string designator, double voltage)
            : base(designator, $"{voltage}V")
        {
            Voltage = voltage;
            RegisterPin("PWR", 1); // Power output — wire this to circuit Vcc pins
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            string pwrNet = schematic.GetNetNameForPin(Pins[0]);
            // V<designator> <+node> <-node=GND> DC <voltage>
            return $"V{Designator} {pwrNet} 0 DC {Voltage}";
        }
    }
}
