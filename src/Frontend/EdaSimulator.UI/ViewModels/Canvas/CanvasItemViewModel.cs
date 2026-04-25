using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EdaSimulator.UI.ViewModels.Canvas
{
    /// <summary>
    /// Base class for all visual items rendered on the schematic canvas.
    /// Provides common properties like Z-Index, Selection state, and coordinates.
    /// </summary>
    public abstract partial class CanvasItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private double _x;

        [ObservableProperty]
        private double _y;

        [ObservableProperty]
        private int _zIndex;

        [ObservableProperty]
        private bool _isSelected;
        
        /// <summary>
        /// Unique visual ID (can map to backing core electrical ID).
        /// </summary>
        public Guid Id { get; protected set; } = Guid.NewGuid();
    }
}
