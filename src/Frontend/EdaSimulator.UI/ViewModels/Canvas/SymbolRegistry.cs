using System;
using System.Collections.Generic;
using System.Windows;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.Simulation.Digital;

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
        public required string PathData { get; init; } 
        
        /// <summary>
        /// Maps a SPICE sequence (1-indexed) to its local X,Y physical offset.
        /// Coordinate (0,0) represents the center of the component body.
        /// </summary>
        public required Dictionary<int, Point> PinOffsets { get; init; }
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

            // === Phase 4: Advanced Analog Components ===

            _registry[typeof(Diode)] = new ComponentSymbol
            {
                Width = 40, Height = 20,
                PathData = "M -20,0 L -10,0 M -10,-10 L 10,0 L -10,10 Z M 10,-10 L 10,10 M 10,0 L 20,0",
                PinOffsets = new Dictionary<int, Point> { { 1, new Point(-20, 0) }, { 2, new Point(20, 0) } }
            };

            _registry[typeof(BJT)] = new ComponentSymbol
            {
                Width = 40, Height = 40,
                PathData = "M -20,0 L -5,0 M -5,-15 L -5,15 M -5,5 L 15,15 L 15,20 M -5,-5 L 15,-15 L 15,-20 M 10,15 L 5,10 L 15,10 Z",
                PinOffsets = new Dictionary<int, Point> { { 1, new Point(15, -20) }, { 2, new Point(-20, 0) }, { 3, new Point(15, 20) } }
            };

            _registry[typeof(MOSFET)] = new ComponentSymbol
            {
                Width = 40, Height = 40,
                PathData = "M -20,0 L -10,0 M -10,-15 L -10,15 M -5,-15 L -5,-5 M -5,-2 L -5,2 M -5,5 L -5,15 M -5,-10 L 15,-10 L 15,-20 M -5,10 L 15,10 L 15,20 M 15,10 L 15,0 L -5,0 M 10,-5 L 5,0 L 10,5 Z",
                PinOffsets = new Dictionary<int, Point> { { 1, new Point(15, -20) }, { 2, new Point(-20, 0) }, { 3, new Point(15, 20) }, { 4, new Point(15, 0) } }
            };

            _registry[typeof(OpAmp)] = new ComponentSymbol
            {
                Width = 60, Height = 60,
                PathData = "M -20,-30 L 30,0 L -20,30 Z M -20,-15 L -30,-15 M -20,15 L -30,15 M -15,-15 L -5,-15 M -10,-20 L -10,-10 M -15,15 L -5,15 M 0,-18 L 0,-28 M 0,18 L 0,28 M 30,0 L 40,0",
                PinOffsets = new Dictionary<int, Point> { { 1, new Point(-30, -15) }, { 2, new Point(-30, 15) }, { 3, new Point(0, -28) }, { 4, new Point(0, 28) }, { 5, new Point(40, 0) } }
            };

            // === Phase 4: Digital Gates ===

            _registry[typeof(AndGate)] = new ComponentSymbol
            {
                Width = 50, Height = 40,
                PathData = "M -25,-15 L -10,-15 M -25,15 L -10,15 M -10,-20 L 0,-20 A 20,20 0 0,1 0,20 L -10,20 Z M 20,0 L 35,0",
                PinOffsets = new Dictionary<int, Point> { { 1, new Point(-25, -15) }, { 2, new Point(-25, 15) }, { 3, new Point(35, 0) } }
            };

            _registry[typeof(OrGate)] = new ComponentSymbol
            {
                Width = 50, Height = 40,
                PathData = "M -25,-15 L -15,-15 M -25,15 L -15,15 M -20,-20 Q 0,-20 20,0 Q 0,20 -20,20 Q -5,0 -20,-20 M 20,0 L 35,0",
                PinOffsets = new Dictionary<int, Point> { { 1, new Point(-25, -15) }, { 2, new Point(-25, 15) }, { 3, new Point(35, 0) } }
            };

            _registry[typeof(NotGate)] = new ComponentSymbol
            {
                Width = 40, Height = 20,
                PathData = "M -20,0 L -10,0 M -10,-10 L 5,0 L -10,10 Z M 5,0 A 3,3 0 1,1 11,0 A 3,3 0 1,1 5,0 M 11,0 L 20,0",
                PinOffsets = new Dictionary<int, Point> { { 1, new Point(-20, 0) }, { 2, new Point(20, 0) } }
            };

            _registry[typeof(DFlipFlop)] = new ComponentSymbol
            {
                Width = 50, Height = 60,
                PathData = "M -20,-30 L 20,-30 L 20,30 L -20,30 Z M -20,-15 L -30,-15 M -20,15 L -30,15 M -20,10 L -10,15 L -20,20 M 20,-15 L 30,-15 M 20,15 L 30,15",
                PinOffsets = new Dictionary<int, Point> { { 1, new Point(-30, -15) }, { 2, new Point(-30, 15) }, { 3, new Point(30, -15) }, { 4, new Point(30, 15) } }
            };

            // === Power Symbols ===

            // Ground: classic 3-line downward triangle (IEEE/ANSI standard)
            _registry[typeof(GroundSymbol)] = new ComponentSymbol
            {
                Width = 30, Height = 24,
                PathData = "M 0,-12 L 0,0 M -12,0 L 12,0 M -7,6 L 7,6 M -3,12 L 3,12",
                PinOffsets = new Dictionary<int, Point> { { 1, new Point(0, -12) } }
            };

            // Power Rail: upward arrow line with label above (VCC/VDD style)
            _registry[typeof(PowerRail)] = new ComponentSymbol
            {
                Width = 20, Height = 30,
                PathData = "M 0,15 L 0,-5 M -8,-5 L 8,-5 M -4,-12 L 0,-20 L 4,-12",
                PinOffsets = new Dictionary<int, Point> { { 1, new Point(0, 15) } }
            };

            // MCU (Arduino, ESP32, STM32): Large chip box with dynamic pins layout
            var mcuSymbol = new ComponentSymbol
            {
                Width = 80,
                Height = 160,
                PathData = "M -40,-80 L 40,-80 L 40,80 L -40,80 Z M -35,-75 L 35,-75 L 35,75 L -35,75 Z",
                PinOffsets = new Dictionary<int, Point>()
            };
            for (int i = 1; i <= 40; i++)
            {
                if (i % 2 == 1) // Odd on left
                {
                    int row = (i - 1) / 2;
                    mcuSymbol.PinOffsets[i] = new Point(-40, -70 + row * 7);
                }
                else // Even on right
                {
                    int row = (i - 2) / 2;
                    mcuSymbol.PinOffsets[i] = new Point(40, -70 + row * 7);
                }
            }
            _registry[typeof(McuComponent)] = mcuSymbol;
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
