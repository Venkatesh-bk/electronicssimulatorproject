using System;
using System.Collections.Generic;
using System.Linq;

namespace EdaSimulator.Engines.Core
{
    /// <summary>
    /// Abstract base class for all electrical components in the simulator.
    /// Enforces SPICE netlist generation contract and manages pin ownership.
    /// </summary>
    public abstract class Component
    {
        /// <summary>Unique identity for this component instance on the canvas.</summary>
        public Guid Id { get; } = Guid.NewGuid();

        private string _designator;

        /// <summary>
        /// Reference designator following EIA standard convention (e.g., "R1", "C2", "U5", "VCC1").
        /// Must not be null or empty.
        /// </summary>
        public string Designator
        {
            get => _designator;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Designator cannot be null or empty.", nameof(value));
                _designator = value;
            }
        }

        /// <summary>
        /// Primary simulation value or part number (e.g., "10k", "100nF", "2N3904", "LM741").
        /// Empty string is valid for components that carry no intrinsic value (e.g., connectors).
        /// </summary>
        public string Value { get; set; }

        private readonly List<Pin> _pins = new();

        /// <summary>Read-only ordered list of electrical terminals this component has.</summary>
        public IReadOnlyList<Pin> Pins => _pins;

        /// <summary>
        /// Initializes the component with a designator and value.
        /// </summary>
        protected Component(string designator, string value)
        {
            if (string.IsNullOrWhiteSpace(designator))
                throw new ArgumentException("Designator cannot be null or empty.", nameof(designator));

            _designator = designator;
            Value = value ?? string.Empty;
        }

        /// <summary>
        /// Factory method for derived classes to create and register a pin during construction.
        /// Pins must be registered in the correct SPICE order (sequence 1, 2, 3, ...).
        /// </summary>
        /// <param name="name">Pin name from the component's datasheet.</param>
        /// <param name="spiceSequence">1-indexed position for SPICE netlist line output.</param>
        protected Pin RegisterPin(string name, int spiceSequence)
        {
            var pin = new Pin(Id, name, spiceSequence);
            _pins.Add(pin);
            return pin;
        }

        /// <summary>
        /// Retrieves a pin by its exact name (case-insensitive).
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown if the pin name does not exist on this component.</exception>
        public Pin GetPinByName(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return _pins.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                   ?? throw new KeyNotFoundException($"Pin '{name}' not found on component '{Designator}'.");
        }

        /// <summary>
        /// Returns all pins in their SPICE sequence order. Validates no sequence gaps exist.
        /// </summary>
        public IEnumerable<Pin> GetPinsInSpiceOrder() => _pins.OrderBy(p => p.SpiceNodeSequence);

        /// <summary>
        /// Forces all derived component types to implement their SPICE netlist representation.
        /// Example output: "R1 N001 GND 10k" for a 10kΩ resistor R1.
        /// </summary>
        /// <param name="schematic">The containing schematic, used to resolve net names for each pin.</param>
        /// <returns>A valid SPICE netlist element line.</returns>
        public abstract string GenerateSpiceNetlistLine(Schematic schematic);

        public override string ToString() => $"[{GetType().Name}] {Designator} = {Value} ({_pins.Count} pins)";
    }
}
