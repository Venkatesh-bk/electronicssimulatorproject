using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EdaSimulator.Engines.Models
{
    /// <summary>
    /// The master circuit graph. Manages referential integrity between all Components,
    /// Pins, and routing Nets. This is the central object that gets serialized to disk
    /// and passed to simulation engines.
    /// </summary>
    public class Schematic
    {
        private readonly Dictionary<Guid, Component> _components = new();
        private readonly Dictionary<Guid, Net> _nets = new();

        /// <summary>Provides read-only access to all active schematic components.</summary>
        public IReadOnlyDictionary<Guid, Component> Components => _components;

        /// <summary>Provides read-only access to all routing nets.</summary>
        public IReadOnlyDictionary<Guid, Net> Nets => _nets;

        /// <summary>
        /// The SPICE ground reference node ("0"). Auto-created on schematic construction.
        /// Every valid SPICE simulation requires this reference to exist.
        /// </summary>
        public Net MasterGroundNet { get; }

        /// <summary>
        /// The user-visible name of this schematic (e.g., the filename without extension).
        /// </summary>
        public string Title { get; set; }

        /// <summary>Initializes an empty new schematic with a mandatory ground reference.</summary>
        public Schematic(string title = "Untitled")
        {
            Title = title ?? "Untitled";

            // Industry Standard: Every SPICE simulation environment requires a universal "0" reference node.
            MasterGroundNet = new Net(Net.SpiceGroundName);
            _nets.Add(MasterGroundNet.Id, MasterGroundNet);
        }

        // ─── Component Management ────────────────────────────────────────────────────

        /// <summary>
        /// Adds a component to the schematic. If the same component ID already exists, it is replaced.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if component is null.</exception>
        public void AddComponent(Component component)
        {
            ArgumentNullException.ThrowIfNull(component);

            if (_components.Values.Any(c => string.Equals(c.Designator, component.Designator, StringComparison.OrdinalIgnoreCase) && c.Id != component.Id))
                throw new ArgumentException($"A component with the designator '{component.Designator}' already exists in this schematic.");

            _components[component.Id] = component;
        }

        /// <summary>
        /// Safely removes a component, severing all its electrical connections before deletion
        /// to prevent dangling pin references inside Nets.
        /// </summary>
        public bool RemoveComponent(Guid componentId)
        {
            if (!_components.TryGetValue(componentId, out var component))
                return false;

            foreach (var pin in component.Pins)
                DisconnectPin(pin);

            _components.Remove(componentId);
            return true;
        }

        // ─── Net Management ──────────────────────────────────────────────────────────

        /// <summary>
        /// Creates and registers a new named net.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if name is null/empty or conflicts with the reserved "0" ground name.</exception>
        public Net CreateNet(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Net name cannot be null or empty.", nameof(name));
            if (name == Net.SpiceGroundName)
                throw new ArgumentException($"Net name '0' is reserved for the master ground net. Use MasterGroundNet directly.", nameof(name));
            if (_nets.Values.Any(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"A net with the name '{name}' already exists in this schematic.");

            var net = new Net(name);
            _nets.Add(net.Id, net);
            return net;
        }

        /// <summary>
        /// Removes a net from the schematic, first disconnecting all pins attached to it.
        /// The master ground net cannot be removed.
        /// </summary>
        public bool RemoveNet(Guid netId)
        {
            if (netId == MasterGroundNet.Id)
                throw new InvalidOperationException("The master ground net cannot be removed from a schematic.");

            if (!_nets.TryGetValue(netId, out var net))
                return false;

            // Disconnect all pins referencing this net
            var pinIds = net.ConnectedPinIds.ToList();
            foreach (var pinId in pinIds)
            {
                // Find pin across all components
                foreach (var comp in _components.Values)
                {
                    var pin = comp.Pins.FirstOrDefault(p => p.Id == pinId);
                    if (pin != null)
                    {
                        pin.Disconnect();
                        break;
                    }
                }
            }

            _nets.Remove(netId);
            return true;
        }

        // ─── Connection Management ───────────────────────────────────────────────────

        /// <summary>
        /// Connects a pin to an existing net, maintaining full bidirectional graph consistency.
        /// If the pin is already connected to another net, that connection is safely severed first.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown if the target net does not exist in this schematic.</exception>
        public void ConnectPinToNet(Pin pin, Guid netId)
        {
            ArgumentNullException.ThrowIfNull(pin);

            if (!_nets.TryGetValue(netId, out var targetNet))
                throw new KeyNotFoundException($"Cannot connect pin '{pin.Name}': Net ID '{netId}' does not exist in this schematic.");

            // If already connected to a different net, sever that link first
            if (pin.ConnectedNetId.HasValue && pin.ConnectedNetId.Value != netId)
            {
                if (_nets.TryGetValue(pin.ConnectedNetId.Value, out var oldNet))
                    oldNet.RemovePin(pin.Id);
            }

            pin.ConnectedNetId = netId;
            targetNet.AddPin(pin.Id);
        }

        /// <summary>
        /// Disconnects a pin from its current net, maintaining full bidirectional graph consistency.
        /// Safe to call on already-floating pins.
        /// </summary>
        public void DisconnectPin(Pin pin)
        {
            ArgumentNullException.ThrowIfNull(pin);

            if (!pin.ConnectedNetId.HasValue) return;

            if (_nets.TryGetValue(pin.ConnectedNetId.Value, out var net))
                net.RemovePin(pin.Id);

            pin.Disconnect();
        }

        // ─── Netlist Helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the SPICE net name for a given pin.
        /// Unconnected/floating pins return a statistically unique local node name ("NC_uuid")
        /// to prevent independent floating pins from silently short-circuiting together in SPICE.
        /// </summary>
        public string GetNetNameForPin(Pin pin)
        {
            ArgumentNullException.ThrowIfNull(pin);

            if (!pin.ConnectedNetId.HasValue)
                return $"NC_{pin.Id:N}";

            return _nets.TryGetValue(pin.ConnectedNetId.Value, out var net) ? net.Name : $"NC_{pin.Id:N}";
        }

        /// <summary>
        /// Validates the schematic for common errors that would prevent simulation:
        /// - Components with all pins floating
        /// - Nets with fewer than 2 connections
        /// - No ground reference connections
        /// </summary>
        /// <returns>List of human-readable warning/error strings. Empty list means no issues found.</returns>
        public IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();

            if (MasterGroundNet.ConnectedPinIds.Count == 0)
                issues.Add("CRITICAL: No pins are connected to the ground net ('0'). Simulation will fail.");

            var duplicateDesignators = _components.Values.GroupBy(c => c.Designator, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1);
            foreach (var group in duplicateDesignators)
                issues.Add($"CRITICAL: Duplicate component designator found: '{group.Key}'. SPICE requires unique designators.");

            var duplicateNets = _nets.Values.GroupBy(n => n.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1);
            foreach (var group in duplicateNets)
                issues.Add($"CRITICAL: Duplicate net name found: '{group.Key}'. Pins will implicitly short-circuit in SPICE.");

            foreach (var comp in _components.Values)
            {
                var floatingPins = comp.Pins.Where(p => p.IsFloating).ToList();
                if (floatingPins.Count == comp.Pins.Count)
                    issues.Add($"WARNING: Component '{comp.Designator}' has all pins floating (unconnected).");
                else if (floatingPins.Count > 0)
                    issues.Add($"WARNING: Component '{comp.Designator}' has {floatingPins.Count} floating pin(s): {string.Join(", ", floatingPins.Select(p => p.Name))}.");
            }

            foreach (var net in _nets.Values)
            {
                if (!net.IsGround && net.ConnectedPinIds.Count < 2)
                    issues.Add($"WARNING: Net '{net.Name}' connects fewer than 2 pins — may be a dangling stub.");
            }

            return issues;
        }

        /// <summary>
        /// Generates a summary of the schematic state — useful for debug logging.
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Schematic: '{Title}'");
            sb.AppendLine($"  Components : {_components.Count}");
            sb.AppendLine($"  Nets       : {_nets.Count}");
            sb.AppendLine($"  Ground pins: {MasterGroundNet.ConnectedPinIds.Count}");
            return sb.ToString();
        }
    }
}
