using System;
using System.Collections.Generic;
using System.Linq;

namespace EdaSimulator.Engines.Models.Components
{
    /// <summary>
    /// Represents a Microcontroller Unit (MCU) like ESP32, Arduino, or STM32
    /// supporting virtual firmware loading and co-simulation execution.
    /// </summary>
    public class McuComponent : Component
    {
        public string McuType { get; set; } = "Arduino Uno R3";
        public string FirmwarePath { get; set; } = string.Empty;

        public McuComponent(string designator, string mcuType) : base(designator, mcuType)
        {
            McuType = mcuType;
            ConfigurePins();
        }

        private void ConfigurePins()
        {
            if (McuType.Contains("Arduino", StringComparison.OrdinalIgnoreCase))
            {
                // Arduino Uno R3: 14 digital, 6 analog, power/gnd/reset
                for (int i = 0; i <= 13; i++)
                    RegisterPin($"D{i}", i + 1);
                for (int i = 0; i <= 5; i++)
                    RegisterPin($"A{i}", 15 + i);
                RegisterPin("5V", 21);
                RegisterPin("3.3V", 22);
                RegisterPin("GND", 23);
                RegisterPin("RESET", 24);
            }
            else if (McuType.Contains("ESP32", StringComparison.OrdinalIgnoreCase))
            {
                // ESP32 WROOM: 38 pins
                RegisterPin("3V3", 1);
                RegisterPin("EN", 2);
                RegisterPin("VP", 3);
                RegisterPin("VN", 4);
                for (int i = 0; i < 20; i++)
                {
                    RegisterPin($"GPIO{i}", 5 + i);
                }
                RegisterPin("GND", 25);
            }
            else // Default STM32 Blue Pill / ARM
            {
                // STM32: PA0-PA15, PB0-PB15, power, ground
                for (int i = 0; i <= 15; i++)
                    RegisterPin($"PA{i}", i + 1);
                for (int i = 0; i <= 15; i++)
                    RegisterPin($"PB{i}", 17 + i);
                RegisterPin("3.3V", 33);
                RegisterPin("GND", 34);
            }
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pinNets = string.Join(" ", Pins.Select(p => schematic.GetNetNameForPin(p)));
            return $"X{Designator} {pinNets} McuBehavioralModel";
        }
    }
}
