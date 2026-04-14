using System;
using System.Collections.Generic;
using System.Linq;

namespace EdaSimulator.Engines.Core
{
    /// <summary>
    /// Abstract base class for all electrical constructs present in the circuit.
    /// Provides foundational graph theory data (Pins) and simulation properties.
    /// </summary>
    public abstract class Component
    {
        /// <summary>
        /// Unique globally identifiable ID for the component instance on the canvas.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Schematic reference designator (e.g., "R1", "C2", "U5A").
        /// </summary>
        public string Designator { get; set; }

        /// <summary>
        /// Primary simulation or graphical value (e.g., "10k", "100nF", "1N4148").
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// A read-only collection of electrical terminals the component possesses.
        /// </summary>
        private readonly List<Pin> _pins = new List<Pin>();
        public IReadOnlyList<Pin> Pins => _pins;

        protected Component(string designator, string value)
        {
            Designator = designator ?? throw new ArgumentNullException(nameof(designator));
            Value = value ?? string.Empty;
        }

        /// <summary>
        /// Called by derived classes to instantiate their physical pins during construction.
        /// </summary>
        /// <param name="name">Textual name of the pin (e.g. VCC)</param>
        /// <param name="spiceSequence">Specific SPICE indexing requirement</param>
        /// <returns>The generated Pin object reference.</returns>
        protected Pin RegisterPin(string name, int spiceSequence)
        {
            var pin = new Pin(this.Id, name, spiceSequence);
            _pins.Add(pin);
            return pin;
        }

        /// <summary>
        /// Retrieves a pin by its exact designated name.
        /// </summary>
        public Pin GetPinByName(string name)
        {
            return _pins.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) 
                ?? throw new KeyNotFoundException($"Pin '{name}' not found on component {Designator}.");
        }

        /// <summary>
        /// Abstraction contract to force all components to implement their own representation in a SPICE Netlist.
        /// </summary>
        /// <param name="schematic">Reference to the schematic to resolve net connections during generation.</param>
        /// <returns>One or more lines of SPICE valid syntax (e.g., "R1 N001 N002 10k")</returns>
        public abstract string GenerateSpiceNetlistLine(Schematic schematic);

        public override string ToString() => $"[Component] {Designator} ({Value}) - {_pins.Count} pins";
    }
}
