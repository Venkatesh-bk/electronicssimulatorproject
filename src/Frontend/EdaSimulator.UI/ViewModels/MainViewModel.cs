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
            // Quick test logic to populate the schematic so layout and netlist functionality can be verified visually
            try
            {
                var r1 = new Resistor("R1", "10k");
                var v1 = new VoltageSource("V1", "DC 5V");
                
                ActiveSchematic.AddComponent(r1);
                ActiveSchematic.AddComponent(v1);

                // Connect them in series
                var net1 = ActiveSchematic.CreateNet("N001");
                ActiveSchematic.ConnectPinToNet(v1.GetPinByName("+"), net1.Id);
                ActiveSchematic.ConnectPinToNet(r1.GetPinByName("1"), net1.Id);
                
                // Connect to ground
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
