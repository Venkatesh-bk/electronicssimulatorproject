using System;
using System.Collections.Generic;

namespace EdaSimulator.Engines.Core
{
    /// <summary>
    /// Represents a wire or electrical junction traversing the schematic. 
    /// A Net enforces that all connected Pins share the exact same voltage node during simulation.
    /// </summary>
    public class Net
    {
        public static readonly string SpiceGroundName = "0";

        /// <summary>
        /// Unique globally identifiable ID for this net instance.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// User-defined or auto-generated name of the net (e.g., "Net_1001", "VCC", "5V").
        /// In SPICE simulation, the global universal ground MUST be designated as "0".
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A read-only collection of UUIDs pointing to all component pins physically touching this net.
        /// </summary>
        private readonly HashSet<Guid> _connectedPinIds = new HashSet<Guid>();
        public IReadOnlyCollection<Guid> ConnectedPinIds => _connectedPinIds;

        /// <summary>
        /// Indicates if this net is the universal simulation ground (0V).
        /// </summary>
        public bool IsGround => Name == SpiceGroundName;

        /// <summary>
        /// Initializes a new electrical net.
        /// </summary>
        /// <param name="name">Initial name of the net. SPICE tools expect ground to literally be "0".</param>
        public Net(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Net name cannot be null or empty.", nameof(name));
            
            Name = name;
        }

        /// <summary>
        /// Registers a pin ID locally to this net. Note: this does not update the pin's internal state.
        /// </summary>
        internal void AddPin(Guid pinId)
        {
            _connectedPinIds.Add(pinId);
        }

        /// <summary>
        /// Removes a pin ID from this net.
        /// </summary>
        internal void RemovePin(Guid pinId)
        {
            _connectedPinIds.Remove(pinId);
        }

        public override string ToString() => $"Net {Name} (Nodes: {_connectedPinIds.Count})";
    }
}
