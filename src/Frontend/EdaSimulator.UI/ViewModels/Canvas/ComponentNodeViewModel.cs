using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;

namespace EdaSimulator.UI.ViewModels.Canvas
{
    /// <summary>
    /// Visual representation of an electrical component on the canvas.
    /// Manages spatial rotation, label positioning, and its child pins.
    /// </summary>
    public partial class ComponentNodeViewModel : CanvasItemViewModel
    {
        public Component CoreComponent { get; }

        public ObservableCollection<PinNodeViewModel> Pins { get; } = new();

        [ObservableProperty]
        private double _rotationAngle;

        [ObservableProperty]
        private bool _isMirroredX;

        [ObservableProperty]
        private bool _isMirroredY;

        public string PathData { get; private set; }

        partial void OnRotationAngleChanged(double value) => UpdatePinAbsolutePositions();
        partial void OnIsMirroredXChanged(bool value)
        {
            UpdatePinAbsolutePositions();
            OnPropertyChanged(nameof(ScaleX));
        }
        partial void OnIsMirroredYChanged(bool value)
        {
            UpdatePinAbsolutePositions();
            OnPropertyChanged(nameof(ScaleY));
        }

        public double ScaleX => IsMirroredX ? -1.0 : 1.0;
        public double ScaleY => IsMirroredY ? -1.0 : 1.0;

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName == nameof(X) || e.PropertyName == nameof(Y))
            {
                UpdatePinAbsolutePositions();
            }
        }

        public double BoundsWidth { get; set; } = 50; // default generic fallback bounds
        public double BoundsHeight { get; set; } = 50;

        public string Designator
        {
            get => CoreComponent.Designator;
            set
            {
                if (CoreComponent.Designator != value)
                {
                    CoreComponent.Designator = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Value
        {
            get => CoreComponent.Value;
            set
            {
                if (CoreComponent.Value != value)
                {
                    CoreComponent.Value = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsGround => CoreComponent is GroundSymbol;
        public bool IsPower => CoreComponent is PowerRail;
        public bool IsPowerSymbol => IsGround || IsPower;

        public string PowerLabel
        {
            get
            {
                if (CoreComponent is PowerRail)
                {
                    return Designator; 
                }
                if (CoreComponent is GroundSymbol)
                {
                    return "GND";
                }
                return string.Empty;
            }
        }

        public ComponentNodeViewModel(Component coreComponent)
        {
            CoreComponent = coreComponent ?? throw new ArgumentNullException(nameof(coreComponent));
            Id = coreComponent.Id;
            ZIndex = 10; // Components draw above wires (z=0)
            
            var symbol = SymbolRegistry.GetSymbol(coreComponent);
            BoundsWidth = symbol.Width;
            BoundsHeight = symbol.Height;
            PathData = symbol.PathData;
            
            // Generate visual pins according to Symbol Registry specifications
            foreach (var pin in CoreComponent.Pins)
            {
                var pinVm = new PinNodeViewModel(pin);
                
                if (symbol.PinOffsets.TryGetValue(pin.SpiceNodeSequence, out var offset))
                {
                    pinVm.LocalX = offset.X;
                    pinVm.LocalY = offset.Y;
                }

                Pins.Add(pinVm);
            }
            
            // Prime absolute positions
            UpdatePinAbsolutePositions();
        }

        /// <summary>
        /// Translates the component by dx, dy and updates child absolute pin positions.
        /// </summary>
        public override void MoveBy(double dx, double dy)
        {
            base.MoveBy(dx, dy);
            UpdatePinAbsolutePositions();
        }

        /// <summary>
        /// Recalculates screen-space coords for pins based on rotation and center X/Y.
        /// Applies standard 2D affine rotation matrix math.
        /// </summary>
        public void UpdatePinAbsolutePositions()
        {
            // Convert degrees to radians for Math library
            double radians = RotationAngle * (Math.PI / 180.0);
            double cosTheta = Math.Cos(radians);
            double sinTheta = Math.Sin(radians);

            foreach (var pin in Pins)
            {
                // Process mirroring
                double effectiveLocalX = IsMirroredX ? -pin.LocalX : pin.LocalX;
                double effectiveLocalY = IsMirroredY ? -pin.LocalY : pin.LocalY;

                // 1. Rotate local coordinates (assuming center 0,0)
                double rotatedX = effectiveLocalX * cosTheta - effectiveLocalY * sinTheta;
                double rotatedY = effectiveLocalX * sinTheta + effectiveLocalY * cosTheta;

                // 2. Translate against absolute center X,Y (which serves as our origin point on the canvas)
                // Component X, Y usually defines top-left in WPF canvas, so we shift origin.
                // Assuming X,Y point to the upper-left, and center is X + Width/2.
                double centerX = X + (BoundsWidth / 2.0);
                double centerY = Y + (BoundsHeight / 2.0);

                pin.X = centerX + rotatedX;
                pin.Y = centerY + rotatedY;
            }
        }
    }
}
