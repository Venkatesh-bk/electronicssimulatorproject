using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.Simulation;
using System.Windows;

namespace EdaSimulator.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private Schematic _activeSchematic;

        [ObservableProperty]
        private string _netlistOutput;

        public MainViewModel()
        {
            // Initialize with an empty schematic
            ActiveSchematic = new Schematic("New Project");
            NetlistOutput = "No netlist generated yet. Click 'Generate .cir' to export.";
        }

        [RelayCommand]
        private void GenerateNetlist()
        {
            var exporter = new SpiceNetlistExporter();
            NetlistOutput = exporter.GenerateNetlist(ActiveSchematic);
        }

        [RelayCommand]
        private void AddMockComponents()
        {
            // Reset the schematic each time so this command is safe to call multiple times
            ActiveSchematic = new Schematic("Mock Circuit");

            try
            {
                var r1 = new Resistor("R1", "10k");
                var v1 = new VoltageSource("V1", "DC 5");  // Valid ngspice syntax: DC <value>

                ActiveSchematic.AddComponent(r1);
                ActiveSchematic.AddComponent(v1);

                // Connect V1(+) → N001 ← R1(pin1) and V1(-) → GND ← R1(pin2)
                var net1 = ActiveSchematic.CreateNet("N001");
                ActiveSchematic.ConnectPinToNet(v1.GetPinByName("+"), net1.Id);
                ActiveSchematic.ConnectPinToNet(r1.GetPinByName("1"), net1.Id);

                ActiveSchematic.ConnectPinToNet(v1.GetPinByName("-"), ActiveSchematic.MasterGroundNet.Id);
                ActiveSchematic.ConnectPinToNet(r1.GetPinByName("2"), ActiveSchematic.MasterGroundNet.Id);

                MessageBox.Show("Successfully added mock V1 and R1 components to schematic.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
