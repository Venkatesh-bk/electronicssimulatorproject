using System;

namespace EdaSimulator.Engines.Models
{
    /// <summary>
    /// Represents a single electrical terminal point on a component.
    /// Acts as the interface between a component's internal physics and the external circuit net.
    /// </summary>
    public sealed class Pin
    {
        /// <summary>Unique identity for this pin instance within the schematic graph.</summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Textual pin name as per the component datasheet (e.g., "VCC", "GND", "IN+", "1").
        /// Immutable — matches physical part definition.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Ordering sequence (1-indexed) used to correctly position this pin in a SPICE netlist line.
        /// e.g., for a Resistor: Pin 1 = positive terminal, Pin 2 = negative terminal.
        /// </summary>
        public int SpiceNodeSequence { get; }

        /// <summary>The Guid of the parent component that owns this pin.</summary>
        public Guid ComponentId { get; }

        /// <summary>
        /// The Guid of the net this pin is connected to.
        /// Null means the pin is electrically floating (unconnected).
        /// </summary>
        public Guid? ConnectedNetId { get; internal set; }

        /// <summary>Returns true if this pin is not connected to any net.</summary>
        public bool IsFloating => !ConnectedNetId.HasValue;

        /// <summary>
        /// Initializes a new Pin.
        /// </summary>
        /// <param name="componentId">The parent component's ID.</param>
        /// <param name="name">Pin name (e.g., "VCC"). Must not be null or empty.</param>
        /// <param name="spiceNodeSequence">1-indexed SPICE ordering position.</param>
        /// <exception cref="ArgumentException">Thrown when name is null or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when sequence is less than 1.</exception>
        public Pin(Guid componentId, string name, int spiceNodeSequence)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Pin name cannot be null or empty.", nameof(name));
            if (spiceNodeSequence < 1)
                throw new ArgumentOutOfRangeException(nameof(spiceNodeSequence), "SPICE node sequence must be 1 or greater.");

            ComponentId = componentId;
            Name = name;
            SpiceNodeSequence = spiceNodeSequence;
        }

        /// <summary>
        /// Internal-only disconnection. Must be called via Schematic.DisconnectPin() to keep the graph consistent.
        /// </summary>
        internal void Disconnect() => ConnectedNetId = null;

        public override string ToString() =>
            $"Pin '{Name}' [seq={SpiceNodeSequence}] on Comp {ComponentId} → {(IsFloating ? "FLOATING" : $"Net {ConnectedNetId}")}";
    }
}
