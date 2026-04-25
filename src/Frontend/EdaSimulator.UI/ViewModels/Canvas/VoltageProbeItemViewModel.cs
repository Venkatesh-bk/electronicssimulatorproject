using CommunityToolkit.Mvvm.ComponentModel;

namespace EdaSimulator.UI.ViewModels.Canvas
{
    public partial class VoltageProbeItemViewModel : CanvasItemViewModel
    {
        [ObservableProperty]
        private string _targetNetName = string.Empty;

        public VoltageProbeItemViewModel(string targetNetName, double x, double y)
        {
            TargetNetName = targetNetName;
            X = x;
            Y = y;
            ZIndex = 50; // Render on top of wires constraints
        }
    }
}
