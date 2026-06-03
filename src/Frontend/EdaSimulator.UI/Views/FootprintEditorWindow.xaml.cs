using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using EdaSimulator.Engines.PCB;
using EdaSimulator.UI.ViewModels;

namespace EdaSimulator.UI.Views
{
    public partial class FootprintEditorWindow : Window
    {
        private readonly PcbFootprintVM _footprintVM;
        private readonly PcbLayoutViewModel _pcbVM;
        private readonly ObservableCollection<PcbPad> _editedPads;

        public FootprintEditorWindow(PcbFootprintVM footprintVM, PcbLayoutViewModel pcbVM)
        {
            InitializeComponent();
            _footprintVM = footprintVM;
            _pcbVM = pcbVM;

            // Load headers & fields
            DesignatorTitle.Text = footprintVM.Designator;
            DesignatorBox.Text = footprintVM.Designator;
            ValueBox.Text = footprintVM.Value;
            CrtWidthBox.Text = footprintVM.Model.CrtYd_Width_mm.ToString("F2");
            CrtHeightBox.Text = footprintVM.Model.CrtYd_Height_mm.ToString("F2");
            FpIdBox.Text = footprintVM.Model.FootprintId;

            // Populate combo box columns
            PadTypeCol.ItemsSource = Enum.GetValues(typeof(PadType));
            PadLayerCol.ItemsSource = new[] { PcbLayerType.FCu, PcbLayerType.BCu, PcbLayerType.FSilkS };

            // Clone pads for safe transactional editing
            var clonedPads = footprintVM.Model.Pads.Select(p => new PcbPad
            {
                PadNumber = p.PadNumber,
                Type = p.Type,
                X = p.X,
                Y = p.Y,
                Width_mm = p.Width_mm,
                Height_mm = p.Height_mm,
                DrillDia_mm = p.DrillDia_mm,
                Layer = p.Layer,
                NetName = p.NetName
            }).ToList();

            _editedPads = new ObservableCollection<PcbPad>(clonedPads);
            PadsGrid.ItemsSource = _editedPads;
        }

        private void AddPad_Click(object sender, RoutedEventArgs e)
        {
            int nextNum = _editedPads.Count + 1;
            while (_editedPads.Any(p => p.PadNumber == nextNum.ToString()))
            {
                nextNum++;
            }

            _editedPads.Add(new PcbPad
            {
                PadNumber = nextNum.ToString(),
                Type = PadType.SMD,
                X = 0.0,
                Y = 0.0,
                Width_mm = 1.2,
                Height_mm = 1.2,
                DrillDia_mm = 0.0,
                Layer = PcbLayerType.FCu
            });
        }

        private void RemovePad_Click(object sender, RoutedEventArgs e)
        {
            if (PadsGrid.SelectedItem is PcbPad selectedPad)
            {
                _editedPads.Remove(selectedPad);
            }
            else
            {
                MessageBox.Show("Please select a pad in the table to remove.", "Select Pad", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (!double.TryParse(CrtWidthBox.Text, out double crtW) || crtW <= 0)
            {
                MessageBox.Show("Courtyard Width must be a positive number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!double.TryParse(CrtHeightBox.Text, out double crtH) || crtH <= 0)
            {
                MessageBox.Show("Courtyard Height must be a positive number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Apply courtyard changes
            _footprintVM.Model.CrtYd_Width_mm = crtW;
            _footprintVM.Model.CrtYd_Height_mm = crtH;

            // Apply pad changes
            _footprintVM.Model.Pads.Clear();
            foreach (var pad in _editedPads)
            {
                _footprintVM.Model.Pads.Add(pad);
            }

            // Replace footprint VM in the PCB Layout ViewModel to force visual redraw
            int idx = _pcbVM.CanvasFootprints.IndexOf(_footprintVM);
            if (idx >= 0)
            {
                // Create a replacement view model, binding it to the updated model
                var replacementVM = new PcbFootprintVM(_footprintVM.Model, () => _pcbVM.UpdateRatsnestPositions());
                _pcbVM.CanvasFootprints[idx] = replacementVM;
            }

            // Trigger updates on routing ratsnest lines
            _pcbVM.UpdateRatsnestPositions();

            // Run DRC in background to check if the new footprint size violates anything
            if (_pcbVM.RunPcbDrcCommand.CanExecute(null))
            {
                _pcbVM.RunPcbDrcCommand.Execute(null);
            }

            DialogResult = true;
            Close();
        }
    }
}
