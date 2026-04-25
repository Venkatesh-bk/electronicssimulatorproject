using System;
using System.Collections.Generic;
using System.Windows;
using EdaSimulator.Engines.Models.Components;

namespace EdaSimulator.UI.ViewModels.Canvas
{
    /// <summary>
    /// Holds the visual definition for how an electrical component is drawn on the 2D canvas.
    /// </summary>
    public class ComponentSymbol
    {
        public double Width { get; init; }
        public double Height { get; init; }
        
        /// <summary>
        /// The WPF Geometry path data used to uniquely draw the shape. 
        /// Coordinates are expected to be mapped to the Width/Height bounds.
        /// </summary>
        public string PathData { get; init; } 
        
        /// <summary>
        /// Maps a SPICE sequence (1-indexed) to its local X,Y physical offset.
        /// Coordinate (0,0) represents the center of the component body.
        /// </summary>
        public Dictionary<int, Point> PinOffsets { get; init; }
    }

    /// <summary>
    /// Industry-standard ANSI/IEEE graphical mappings for EDA components.
    /// </summary>
    public static class SymbolRegistry
    {
        private static readonly Dictionary<Type, ComponentSymbol> _registry = new();

        static SymbolRegistry()
        {
            // --- Resistor (Zig-Zag pattern) ---
            _registry[typeof(Resistor)] = new ComponentSymbol
            {
                Width = 60,
                Height = 20,
                // Zig zag line
                PathData = "M -30,0 L -20,0 L -15,10 L -5,-10 L 5,10 L 15,-10 L 20,0 L 30,0",
                PinOffsets = new Dictionary<int, Point>
                {
                    { 1, new Point(-30, 0) },
                    { 2, new Point(30, 0) }
                }
            };

            // --- Capacitor (Parallel Plates) ---
            _registry[typeof(Capacitor)] = new ComponentSymbol
            {
                Width = 40,
                Height = 30,
                PathData = "M -20,0 L -5,0 M -5,-15 L -5,15 M 5,-15 L 5,15 M 5,0 L 20,0",
                PinOffsets = new Dictionary<int, Point>
                {
                    { 1, new Point(-20, 0) },
                    { 2, new Point(20, 0) }
                }
            };

            // --- Inductor (Coils / Scallops) ---
            _registry[typeof(Inductor)] = new ComponentSymbol
            {
                Width = 60,
                Height = 20,
                // Four bezier bumps
                PathData = "M -30,0 L -15,0 C -15,-15 -5,-15 -5,0 C -5,-15 5,-15 5,0 C 5,-15 15,-15 15,0 L 30,0",
                PinOffsets = new Dictionary<int, Point>
                {
                    { 1, new Point(-30, 0) },
                    { 2, new Point(30, 0) }
                }
            };

            // --- Voltage Source (Circle with +/-) ---
            _registry[typeof(VoltageSource)] = new ComponentSymbol
            {
                Width = 40,
                Height = 40,
                // Circle at center + plus/minus text markings
                PathData = "M -20,0 A 20,20 0 1,1 20,0 A 20,20 0 1,1 -20,0 M -5,-10 L 5,-10 M 0,-15 L 0,-5 M -5,10 L 5,10",
                PinOffsets = new Dictionary<int, Point>
                {
                    { 1, new Point(0, -20) }, // +, Usually top
                    { 2, new Point(0, 20) }   // -, Usually bottom
                }
            };

            // --- Current Source (Circle with Arrow) ---
            _registry[typeof(CurrentSource)] = new ComponentSymbol
            {
                Width = 40,
                Height = 40,
                PathData = "M -20,0 A 20,20 0 1,1 20,0 A 20,20 0 1,1 -20,0 M 0,10 L 0,-10 M -5,-5 L 0,-10 L 5,-5",
                PinOffsets = new Dictionary<int, Point>
                {
                    { 1, new Point(0, -20) }, // output direction
                    { 2, new Point(0, 20) }
                }
            };
        }

        public static ComponentSymbol GetSymbol(Type componentType)
        {
            if (_registry.TryGetValue(componentType, out var symbol))
                return symbol;

            // Generic fallback symbol (Box with pins)
            return new ComponentSymbol
            {
                Width = 40,
                Height = 40,
                PathData = "M -20,-20 L 20,-20 L 20,20 L -20,20 Z",
                PinOffsets = new Dictionary<int, Point>
                {
                    { 1, new Point(-20, 0) },
                    { 2, new Point(20, 0) }
                }
            };
        }
    }
}
