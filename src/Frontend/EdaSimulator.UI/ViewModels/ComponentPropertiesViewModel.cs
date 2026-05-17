using CommunityToolkit.Mvvm.ComponentModel;

namespace EdaSimulator.UI.ViewModels
{
    /// <summary>
    /// Exposed to the Properties Panel in the right sidebar.
    /// Populated when the user clicks a component on the canvas.
    /// </summary>
    public partial class ComponentPropertiesViewModel : ObservableObject
    {
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

        public void Clear()
        {
            Designator     = "";
            Value          = "";
            ComponentType  = "";
            SpiceModelName = "";
            PinSummary     = "";
            HasSelection   = false;
        }

        public void Populate(EdaSimulator.Engines.Models.Component component)
        {
            if (component == null) { Clear(); return; }

            Designator     = component.Designator;
            Value          = component.Value;
            ComponentType  = component.GetType().Name;
            SpiceModelName = $"{component.GetType().Name} ({component.Value})";
            PinSummary     = string.Join(", ", component.Pins.Select(p => $"{p.Name}:{p.SpiceNodeSequence}"));
            HasSelection   = true;
        }
    }
}
