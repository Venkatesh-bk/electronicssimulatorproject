using System;
using Xunit;
using EdaSimulator.Engines.Library;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;

namespace EdaSimulator.Tests
{
    public class CustomComponentTests
    {
        [Fact]
        public void CustomComponent_ShouldInstantiateWithCorrectProperties()
        {
            var libComp = new LibraryComponent
            {
                Id = "TL072_TEST",
                Name = "TL072",
                Category = "Op-Amps",
                Pins = 8,
                SpiceModel = ".subckt TL072 1 2 3 4 5\n.ends",
                PinMappings = "1:OUT1,2:IN1-,3:IN1+,4:VEE,5:IN2+,6:IN2-,7:OUT2,8:VCC",
                CadWidth = 10.0,
                CadHeight = 7.5,
                CadDepth = 4.0,
                CadColor = "#151515",
                CadShape = "DIP"
            };

            var customComp = new CustomComponent("XU1", "TL072", libComp);

            Assert.Equal("XU1", customComp.Designator);
            Assert.Equal("TL072", customComp.Value);
            Assert.Equal(8, customComp.Pins.Count);
            Assert.Equal("OUT1", customComp.Pins[0].Name);
            Assert.Equal("VCC", customComp.Pins[7].Name);
            Assert.Equal(1, customComp.Pins[0].SpiceNodeSequence);
            Assert.Equal(8, customComp.Pins[7].SpiceNodeSequence);
        }

        [Fact]
        public void CustomComponent_ShouldGenerateCorrectSubcircuitNetlistLine()
        {
            var libComp = new LibraryComponent
            {
                Id = "TL072_TEST",
                Name = "TL072",
                Category = "Op-Amps",
                Pins = 5,
                SpiceModel = ".subckt TL072 1 2 3 4 5\n.ends",
                PinMappings = "1,2,3,4,5"
            };

            var customComp = new CustomComponent("U1", "TL072", libComp);
            var schematic = new Schematic("TestSheet");
            schematic.AddComponent(customComp);

            // Connect pins to some nets
            var net1 = schematic.CreateNet("N001");
            var net2 = schematic.CreateNet("N002");

            schematic.ConnectPinToNet(customComp.Pins[0], net1.Id);
            schematic.ConnectPinToNet(customComp.Pins[1], net2.Id);
            schematic.ConnectPinToNet(customComp.Pins[2], schematic.MasterGroundNet.Id);
            schematic.ConnectPinToNet(customComp.Pins[3], schematic.MasterGroundNet.Id);
            schematic.ConnectPinToNet(customComp.Pins[4], net1.Id);

            string line = customComp.GenerateSpiceNetlistLine(schematic);

            // Should format: XU1 N001 N002 GND GND N001 TL072
            Assert.Equal("XU1 N001 N002 0 0 N001 TL072", line);
        }
    }
}
