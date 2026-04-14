using System;

namespace EdaSimulator.Engines.Core
{
    /// <summary>
    /// Represents an electrical connection point on a physical or logical component.
    /// Acts as the interface between the internal physics of a component and the external circuit net.
    /// </summary>
    public class Pin
    {
        /// <summary>
        /// Unique globally identifiable ID for this specific pin instance.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// The textual designation/name of the pin as dictated by the component datasheet (e.g., "VCC", "GND", "In+", "1").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The internal index or ordering of this pin for SPICE matrix generation (e.g., node 1, node 2 of a resistor).
        /// </summary>
        public int SpiceNodeSequence { get; }

        /// <summary>
        /// The UUID of the component this pin belongs to.
        /// </summary>
        public Guid ComponentId { get; }

        /// <summary>
        /// The UUID of the net this pin is physically connected to across the schematic canvas.
        /// Will be null if the pin is floating (unconnected).
        /// </summary>
        public Guid? ConnectedNetId { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the Pin class.
        /// </summary>
        /// <param name="componentId">The parent component ID.</param>
        /// <param name="name">The name/designator of the pin.</param>
        /// <param name="sequence">The structural sequence for simulation models.</param>
        public Pin(Guid componentId, string name, int sequence)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Pin name cannot be null or empty.", nameof(name));

            ComponentId = componentId;
            Name = name;
            SpiceNodeSequence = sequence;
        }

        /// <summary>
        /// Clears the internal connected net ID. Must be coordinated with the Schematic/Net classes.
        /// </summary>
        internal void Disconnect()
        {
            ConnectedNetId = null;
        }

        public override string ToString() => $"Pin {Name} on Comp {ComponentId} -> Net: {ConnectedNetId?.ToString() ?? "Floating"}";
    }
}
