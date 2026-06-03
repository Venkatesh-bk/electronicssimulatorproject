using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EdaSimulator.UI.ViewModels.Canvas
{
    /// <summary>
    /// Visual item representing a net label placed on the schematic canvas.
    /// Can be moved, selected, and renamed, connecting wires/nets under it.
    /// </summary>
    public partial class NetLabelItemViewModel : CanvasItemViewModel
    {
        [ObservableProperty]
        private string _netName = "NET";

        [ObservableProperty]
        private Guid _associatedNetId;

        public NetLabelItemViewModel(string netName, double x, double y, Guid associatedNetId)
        {
            NetName = netName;
            X = x;
            Y = y;
            AssociatedNetId = associatedNetId;
            ZIndex = 5; // Draw above wires, below components
        }

        public override void MoveBy(double dx, double dy)
        {
            base.MoveBy(dx, dy);
        }
    }
}
