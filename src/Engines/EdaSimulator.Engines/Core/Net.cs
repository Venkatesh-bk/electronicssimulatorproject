using System;
using System.Collections.Generic;

namespace EdaSimulator.Engines.Core
{
    /// <summary>
    /// Represents a wire or electrical junction traversing the schematic.
    /// A Net enforces that all connected Pins share the exact same voltage node during simulation.
    /// </summary>
    public sealed class Net
    {
        /// <summary>
        /// The SPICE-standard name for the universal ground reference node. Must always be "0".
        /// </summary>
        public static readonly string SpiceGroundName = "0";

        /// <summary>
        /// Unique globally identifiable ID for this net instance.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        private string _name;

        /// <summary>
        /// User-defined or auto-generated label for this net (e.g., "VCC", "Net_1001", "GND").
        /// SPICE tools require the universal ground to be exactly "0".
        /// The ground net's name is immutable once created.
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (IsGround)
                    throw new InvalidOperationException("The ground net name '0' is immutable and cannot be changed.");
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Net name cannot be null or empty.", nameof(value));
                _name = value;
            }
        }

        /// <summary>
        /// A set of UUIDs pointing to all component pins physically touching this net.
        /// HashSet provides O(1) lookup, add, and remove.
        /// </summary>
        private readonly HashSet<Guid> _connectedPinIds = new();
        public IReadOnlyCollection<Guid> ConnectedPinIds => _connectedPinIds;

        /// <summary>
        /// Indicates if this net is the universal simulation ground reference (0V).
        /// </summary>
        public bool IsGround => _name == SpiceGroundName;

        /// <summary>
        /// Initializes a new electrical net.
        /// </summary>
        /// <param name="name">Name of the net. The SPICE ground must be exactly "0".</param>
        public Net(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Net name cannot be null or empty.", nameof(name));

            _name = name;
        }

        /// <summary>
        /// Registers a pin ID to this net. Coordinated by Schematic — do not call directly.
        /// </summary>
        internal void AddPin(Guid pinId) => _connectedPinIds.Add(pinId);

        /// <summary>
        /// Removes a pin ID from this net. Coordinated by Schematic — do not call directly.
        /// </summary>
        internal void RemovePin(Guid pinId) => _connectedPinIds.Remove(pinId);

        public override string ToString() => $"Net '{Name}' ({_connectedPinIds.Count} pin(s) connected)";
    }
}
