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
        private ComponentNodeViewModel? _activeNode;
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
            if (_isPopulating || _activeNode == null) return;
            if (!string.IsNullOrWhiteSpace(value))
            {
                try
                {
                    _activeNode.Designator = value.Trim();
                }
                catch { /* Ignore invalid designators in real-time */ }
            }
        }

        partial void OnValueChanged(string value)
        {
            if (_isPopulating || _activeNode == null) return;
            _activeNode.Value = value ?? "";
            SpiceModelName = $"{_activeNode.CoreComponent.GetType().Name} ({_activeNode.Value})";
        }

        partial void OnFirmwarePathChanged(string value)
        {
            if (_isPopulating || _activeNode == null) return;
            if (_activeNode.CoreComponent is McuComponent mcu)
            {
                mcu.FirmwarePath = value ?? "";
            }
        }

        public void Clear()
        {
            _activeNode    = null;
            Designator     = "";
            Value          = "";
            ComponentType  = "";
            SpiceModelName = "";
            PinSummary     = "";
            IsMcu          = false;
            FirmwarePath   = "";
            HasSelection   = false;
        }

        public void Populate(ComponentNodeViewModel node)
        {
            _activeNode = node;
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
    }
}
