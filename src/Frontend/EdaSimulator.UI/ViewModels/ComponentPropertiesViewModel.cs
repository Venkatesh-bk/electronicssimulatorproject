using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdaSimulator.UI.ViewModels.Canvas;
using EdaSimulator.Engines.Models.Components;

namespace EdaSimulator.UI.ViewModels
{
    /// <summary>
    /// Exposed to the Properties Panel in the right sidebar.
    /// Populated when the user clicks a component on the canvas.
    /// </summary>
    public partial class ComponentPropertiesViewModel : ObservableObject
    {
        private CanvasItemViewModel? _activeItem;
        private SchematicViewModel? _activeSchematic;
        private bool _isPopulating;

        [ObservableProperty]
        private string _designator = "";

        [ObservableProperty]
        private string _value = "";

        [ObservableProperty]
        private string _componentType = "";

        [ObservableProperty]
        private string _spiceModelName = "";

        [ObservableProperty]
        private string _pinSummary = "";

        [ObservableProperty]
        private bool _hasSelection = false;

        [ObservableProperty]
        private bool _isMcu = false;

        [ObservableProperty]
        private bool _isWire = false;

        [ObservableProperty]
        private string _firmwarePath = "";

        [RelayCommand]
        private void BrowseFirmware()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Firmware Files (*.hex;*.bin;*.elf)|*.hex;*.bin;*.elf|All Files (*.*)|*.*",
                Title = "Select MCU Firmware File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FirmwarePath = openFileDialog.FileName;
            }
        }

        partial void OnDesignatorChanged(string value)
        {
            if (_isPopulating || _activeItem == null) return;
            if (string.IsNullOrWhiteSpace(value)) return;

            if (_activeItem is ComponentNodeViewModel compNode)
            {
                try
                {
                    compNode.Designator = value.Trim();
                }
                catch { /* Ignore invalid designators */ }
            }
            else if (_activeItem is WireViewModel wireNode && _activeSchematic != null)
            {
                try
                {
                    string cleaned = value.Trim();
                    if (System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"\s")) return; // No whitespace

                    Guid survivingId = _activeSchematic.CoreSchematic.RenameNet(wireNode.TargetNetId, cleaned);
                    var targetNet = _activeSchematic.CoreSchematic.GetNetById(survivingId);
                    string finalName = targetNet?.Name ?? cleaned;

                    Guid oldId = wireNode.TargetNetId;
                    foreach (var item in _activeSchematic.Items)
                    {
                        if (item is WireViewModel w && (w.TargetNetId == oldId || w.TargetNetId == survivingId))
                        {
                            w.TargetNetId = survivingId;
                            w.NetLabel = finalName;
                        }
                        else if (item is PinNodeViewModel p && p.CorePin.ConnectedNetId == survivingId)
                        {
                            p.ConnectedNetName = finalName;
                        }
                    }

                    // Update local property state dynamically
                    _isPopulating = true;
                    try
                    {
                        SpiceModelName = $"Net GUID: {survivingId:B}";
                        // Re-list pins
                        var pinNames = new System.Collections.Generic.List<string>();
                        if (targetNet != null)
                        {
                            foreach (var pinId in targetNet.ConnectedPinIds)
                            {
                                foreach (var comp in _activeSchematic.CoreSchematic.Components.Values)
                                {
                                    var pin = comp.Pins.FirstOrDefault(p => p.Id == pinId);
                                    if (pin != null)
                                    {
                                        pinNames.Add($"{comp.Designator}.{pin.Name}");
                                        break;
                                    }
                                }
                            }
                        }
                        PinSummary = string.Join(", ", pinNames);
                    }
                    finally
                    {
                        _isPopulating = false;
                    }
                }
                catch { /* Ignore invalid net renames */ }
            }
        }

        partial void OnValueChanged(string value)
        {
            if (_isPopulating || _activeItem == null) return;
            if (_activeItem is ComponentNodeViewModel compNode)
            {
                compNode.Value = value ?? "";
                SpiceModelName = $"{compNode.CoreComponent.GetType().Name} ({compNode.Value})";
            }
        }

        partial void OnFirmwarePathChanged(string value)
        {
            if (_isPopulating || _activeItem == null) return;
            if (_activeItem is ComponentNodeViewModel compNode && compNode.CoreComponent is McuComponent mcu)
            {
                mcu.FirmwarePath = value ?? "";
            }
        }

        public void Clear()
        {
            _activeItem    = null;
            _activeSchematic = null;
            Designator     = "";
            Value          = "";
            ComponentType  = "";
            SpiceModelName = "";
            PinSummary     = "";
            IsMcu          = false;
            IsWire         = false;
            FirmwarePath   = "";
            HasSelection   = false;
        }

        public void Populate(ComponentNodeViewModel node)
        {
            _activeItem = node;
            _activeSchematic = null;
            if (node == null) { Clear(); return; }

            _isPopulating = true;
            try
            {
                var component = node.CoreComponent;
                Designator     = component.Designator;
                Value          = component.Value;
                ComponentType  = component.GetType().Name;
                SpiceModelName = $"{component.GetType().Name} ({component.Value})";
                PinSummary     = string.Join(", ", component.Pins.Select(p => $"{p.Name}:{p.SpiceNodeSequence}"));
                
                IsWire         = false;
                if (component is McuComponent mcu)
                {
                    IsMcu = true;
                    FirmwarePath = mcu.FirmwarePath;
                }
                else
                {
                    IsMcu = false;
                    FirmwarePath = string.Empty;
                }

                HasSelection   = true;
            }
            finally
            {
                _isPopulating = false;
            }
        }

        public void PopulateWire(WireViewModel wire, SchematicViewModel schematic)
        {
            _activeItem = wire;
            _activeSchematic = schematic;
            if (wire == null || schematic == null) { Clear(); return; }

            _isPopulating = true;
            try
            {
                var net = schematic.CoreSchematic.GetNetById(wire.TargetNetId);
                Designator     = wire.NetLabel;
                Value          = "";
                ComponentType  = "Wire / Net";
                SpiceModelName = $"Net GUID: {wire.TargetNetId:B}";
                IsMcu          = false;
                IsWire         = true;
                FirmwarePath   = "";

                var pinNames = new System.Collections.Generic.List<string>();
                if (net != null)
                {
                    foreach (var pinId in net.ConnectedPinIds)
                    {
                        foreach (var comp in schematic.CoreSchematic.Components.Values)
                        {
                            var pin = comp.Pins.FirstOrDefault(p => p.Id == pinId);
                            if (pin != null)
                            {
                                pinNames.Add($"{comp.Designator}.{pin.Name}");
                                break;
                            }
                        }
                    }
                }
                PinSummary = string.Join(", ", pinNames);
                HasSelection = true;
            }
            finally
            {
                _isPopulating = false;
            }
        }
    }
}
