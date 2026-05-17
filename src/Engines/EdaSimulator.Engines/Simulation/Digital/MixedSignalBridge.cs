using System;

namespace EdaSimulator.Engines.Simulation.Digital
{
    /// <summary>
    /// Bridges the SPICE Analog domain with the Digital Event-Driven domain.
    /// Acts as an ADC (Analog -> Digital) and DAC (Digital -> Analog).
    /// </summary>
    public class MixedSignalBridge
    {
        public double LogicHighThreshold { get; set; } = 3.5; // Volts
        public double LogicLowThreshold { get; set; } = 1.5;  // Volts

        public double LogicHighOutputVoltage { get; set; } = 5.0; // Volts
        public double LogicLowOutputVoltage { get; set; } = 0.0;  // Volts

        /// <summary>
        /// Converts an analog voltage reading from SPICE into a Digital LogicState.
        /// </summary>
        public LogicState AnalogToDigital(double voltage)
        {
            if (voltage >= LogicHighThreshold) return LogicState.High;
            if (voltage <= LogicLowThreshold) return LogicState.Low;
            return LogicState.Undefined; // In the invalid/transition zone
        }

        /// <summary>
        /// Converts a Digital LogicState into an Analog voltage value to be driven in SPICE.
        /// </summary>
        public double DigitalToAnalog(LogicState state)
        {
            return state switch
            {
                LogicState.High => LogicHighOutputVoltage,
                LogicState.Low => LogicLowOutputVoltage,
                _ => 0.0 // Undefined or HighZ defaults to 0V drive (or could be modeled as High Resistance)
            };
        }
    }
}
