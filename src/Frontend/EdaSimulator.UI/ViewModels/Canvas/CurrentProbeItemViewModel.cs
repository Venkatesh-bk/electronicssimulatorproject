using CommunityToolkit.Mvvm.ComponentModel;

namespace EdaSimulator.UI.ViewModels.Canvas
{
    public partial class CurrentProbeItemViewModel : CanvasItemViewModel
    {
        [ObservableProperty]
        private string _targetDeviceDesignator = string.Empty;

        public CurrentProbeItemViewModel(string targetDeviceDesignator, double x, double y)
        {
            TargetDeviceDesignator = targetDeviceDesignator;
            X = x;
            Y = y;
            ZIndex = 51; // Render slightly above voltage probes
        }
    }
}
