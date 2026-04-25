using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.Simulation;
using EdaSimulator.UI.ViewModels.Canvas;
using System.Windows;

namespace EdaSimulator.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private SchematicViewModel _activeSchematicViewModel;

        [ObservableProperty]
        private string _netlistOutput;

        public MainViewModel()
        {
            // Initialize with an empty schematic
            ActiveSchematicViewModel = new SchematicViewModel(new Schematic("New Project"));
            NetlistOutput = "No netlist generated yet. Click 'Generate .cir' to export.";
        }

        [RelayCommand]
        private void GenerateNetlist()
        {
            var drcResult = ActiveSchematicViewModel.RunDRC();
            
            if (!drcResult.isValid)
            {
                NetlistOutput = drcResult.logOutput + "Netlist generation aborted due to critical simulation physics errors.\nPlease connect the required pins and ground referenced nodes.";
                return;
            }

            var exporter = new SpiceNetlistExporter();
            var netlist = exporter.GenerateNetlist(ActiveSchematicViewModel.CoreSchematic);
            
            NetlistOutput = drcResult.logOutput + netlist;
        }

        [RelayCommand]
        private void AddMockComponents()
        {
            // Reset the schematic each time so this command is safe to call multiple times
            ActiveSchematicViewModel = new SchematicViewModel(new Schematic("Mock Circuit"));
            var schVM = ActiveSchematicViewModel;
            var sch = schVM.CoreSchematic;

            try
            {
                var r1 = new Resistor("R1", "10k");
                var v1 = new VoltageSource("V1", "DC 5");  

                // Create Visual Wrappers and assign locations
                var r1Node = new ComponentNodeViewModel(r1) { X = 300, Y = 200 };
                var v1Node = new ComponentNodeViewModel(v1) { X = 100, Y = 200 };
                
                schVM.AddComponentNode(r1Node);
                schVM.AddComponentNode(v1Node);

                // Connect V1(+) → N001 ← R1(pin1) and V1(-) → GND ← R1(pin2)
                var net1 = sch.CreateNet("N001");
                sch.ConnectPinToNet(v1.GetPinByName("+"), net1.Id);
                sch.ConnectPinToNet(r1.GetPinByName("1"), net1.Id);

                sch.ConnectPinToNet(v1.GetPinByName("-"), sch.MasterGroundNet.Id);
                sch.ConnectPinToNet(r1.GetPinByName("2"), sch.MasterGroundNet.Id);

                MessageBox.Show("Successfully added mock V1 and R1 components to schematic.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
