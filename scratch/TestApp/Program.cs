using System;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;

class Program
{
    static void Main(string[] args)
    {
        var schematic = new Schematic("Test");
        var r1 = new Resistor("R1", "10k");
        schematic.AddComponent(r1);

        var netVcc = schematic.CreateNet("VCC");
        
        schematic.ConnectPinToNet(r1.Pins[0], netVcc.Id);
        schematic.ConnectPinToNet(r1.Pins[1], schematic.MasterGroundNet.Id);

        Console.WriteLine(schematic.ToString());

        var errors = schematic.Validate();
        foreach (var err in errors) {
            Console.WriteLine(err);
        }

        Console.WriteLine("Done executing.");
    }
}
