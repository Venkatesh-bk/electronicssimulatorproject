using System;
using System.Collections.Generic;

namespace EdaSimulator.Engines.Core
{
    /// <summary>
    /// The master graph datastructure representing the active workspace payload.
    /// Manages the unified integrity between Components, Pins, and routing Nets.
    /// </summary>
    public class Schematic
    {
        private readonly Dictionary<Guid, Component> _components = new Dictionary<Guid, Component>();
        private readonly Dictionary<Guid, Net> _nets = new Dictionary<Guid, Net>();

        /// <summary>
        /// Provides read-only access to all active components.
        /// </summary>
        public IReadOnlyDictionary<Guid, Component> Components => _components;

        /// <summary>
        /// Provides read-only access to all routed electrical nets.
        /// </summary>
        public IReadOnlyDictionary<Guid, Net> Nets => _nets;

        /// <summary>
        /// The universal ground net reference required for simulation matrices.
        /// </summary>
        public Net MasterGroundNet { get; private set; }

        public Schematic()
        {
            // Industry Standard: Every valid SPICE environment requires a universal "0" reference.
            MasterGroundNet = new Net(Net.SpiceGroundName);
            _nets.Add(MasterGroundNet.Id, MasterGroundNet);
        }

        /// <summary>
        /// Ingests a new component instance into the tracking graph.
        /// </summary>
        public void AddComponent(Component component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            _components[component.Id] = component;
        }

        /// <summary>
        /// Creates a new net tracking object.
        /// </summary>
        public Net CreateNet(string name)
        {
            var net = new Net(name);
            _nets.Add(net.Id, net);
            return net;
        }

        /// <summary>
        /// Wires a specific physical pin to a specific net within the schematic.
        /// </summary>
        /// <param name="pin">The pin to connect</param>
        /// <param name="netId">The destination Net UUID</param>
        public void ConnectPinToNet(Pin pin, Guid netId)
        {
            if (!_nets.TryGetValue(netId, out var net))
                throw new KeyNotFoundException("Cannot connect pin. Assigned Net UUID does not exist in this schematic.");

            // Disconnect from old net safely if the pin had a prior connection.
            if (pin.ConnectedNetId.HasValue && pin.ConnectedNetId.Value != netId)
            {
                if (_nets.TryGetValue(pin.ConnectedNetId.Value, out var oldNet))
                {
                    oldNet.RemovePin(pin.Id);
                }
            }

            pin.ConnectedNetId = netId;
            net.AddPin(pin.Id);
        }

        /// <summary>
        /// Safely disconnects a pin from its attached net, keeping the graph synchronized.
        /// </summary>
        public void DisconnectPin(Pin pin)
        {
            if (pin.ConnectedNetId.HasValue)
            {
                if (_nets.TryGetValue(pin.ConnectedNetId.Value, out var net))
                {
                    net.RemovePin(pin.Id);
                }
                pin.Disconnect();
            }
        }

        /// <summary>
        /// Safely removes a component from the active schematic, severing all its electrical connections.
        /// </summary>
        public void RemoveComponent(Guid componentId)
        {
            if (_components.TryGetValue(componentId, out var component))
            {
                // Unwire all pins to prevent ghost connections
                foreach (var pin in component.Pins)
                {
                    DisconnectPin(pin);
                }
                _components.Remove(componentId);
            }
        }
        
        /// <summary>
        /// Retrieves the exact Name of the net connected to a specific pin, useful during netlist generation.
        /// </summary>
        public string GetNetNameForPin(Pin pin)
        {
            if (!pin.ConnectedNetId.HasValue) 
                return "NC"; // Floating or internally pulled
            
            return _nets.TryGetValue(pin.ConnectedNetId.Value, out var net) ? net.Name : "NC";
        }
    }
}
