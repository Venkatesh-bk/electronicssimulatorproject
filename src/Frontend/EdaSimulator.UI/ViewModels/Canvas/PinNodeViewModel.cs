using System;
using CommunityToolkit.Mvvm.ComponentModel;
using EdaSimulator.Engines.Models;

namespace EdaSimulator.UI.ViewModels.Canvas
{
    /// <summary>
    /// Visual wrapper for a Pin. 
    /// Tracks visual connection points and hit-testing states.
    /// </summary>
    public partial class PinNodeViewModel : CanvasItemViewModel
    {
        public Pin CorePin { get; }

        /// <summary>
        /// X-offset from the center of the parent component, unrotated.
        /// </summary>
        [ObservableProperty]
        private double _localX;

        /// <summary>
        /// Y-offset from the center of the parent component, unrotated.
        /// </summary>
        [ObservableProperty]
        private double _localY;

        [ObservableProperty]
        private bool _isHovered;

        public string Name => CorePin.Name;

        [ObservableProperty]
        private bool _hasError;

        /// <summary>
        /// Name of the net this pin is connected to; empty if floating.
        /// Updated by WiringTool after each successful connection.
        /// </summary>
        [ObservableProperty]
        private string _connectedNetName = string.Empty;

        /// <summary>True when this pin is part of a net (not floating).</summary>
        public bool IsConnected => !string.IsNullOrEmpty(ConnectedNetName);

        partial void OnConnectedNetNameChanged(string value)
            => OnPropertyChanged(nameof(IsConnected));

        public PinNodeViewModel(Pin corePin)
        {
            CorePin = corePin ?? throw new ArgumentNullException(nameof(corePin));
            Id = corePin.Id;
            ZIndex = 20; // Pins draw on top of everything to ensure they can be clicked for wiring
        }
    }
}
