using System;
using System.Linq;
using Xunit;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.PCB;

namespace EdaSimulator.Tests
{
    public class BomGeneratorTests
    {
        [Fact]
        public void BomGenerator_ShouldGroupIdenticalComponentsAndCalculatePrices()
        {
            var schematic = new Schematic("Test BOM");
            
            // Add three resistors of 10k
            var r1 = new Resistor("R1", "10k");
            var r2 = new Resistor("R2", "10k");
            var r3 = new Resistor("R3", "10k");
            
            // Add one capacitor of 100n
            var c1 = new Capacitor("C1", "100n");

            schematic.AddComponent(r1);
            schematic.AddComponent(r2);
            schematic.AddComponent(r3);
            schematic.AddComponent(c1);

            var bom = BomGenerator.GenerateBom(schematic);

            // Should have 2 line items: 10k resistors group and 100n capacitor group
            Assert.Equal(2, bom.Count);

            var resistorItem = bom.FirstOrDefault(i => i.ComponentType == "Resistor");
            Assert.NotNull(resistorItem);
            Assert.Equal(3, resistorItem.Quantity);
            Assert.Contains("R1", resistorItem.DesignatorString);
            Assert.Contains("R2", resistorItem.DesignatorString);
            Assert.Contains("R3", resistorItem.DesignatorString);
            Assert.Equal(0.012, resistorItem.UnitPrice);
            Assert.Equal(0.036, resistorItem.TotalPrice, 3);
            Assert.Equal("150,000+", resistorItem.Stock);

            var capacitorItem = bom.FirstOrDefault(i => i.ComponentType == "Capacitor");
            Assert.NotNull(capacitorItem);
            Assert.Equal(1, capacitorItem.Quantity);
            Assert.Equal(0.024, capacitorItem.UnitPrice);
            Assert.Equal(0.024, capacitorItem.TotalPrice, 3);
        }
    }
}
