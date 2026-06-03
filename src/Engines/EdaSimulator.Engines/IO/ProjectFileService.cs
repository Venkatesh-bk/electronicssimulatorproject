using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;

namespace EdaSimulator.Engines.IO
{
    /// <summary>
    /// Serializes and deserializes a full EDA project (schematic graph + canvas layout)
    /// to/from a JSON-based .edaproj file. Follows the SPICE graph integrity rules
    /// enforced by Schematic.cs at all times.
    /// </summary>
    public static class ProjectFileService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // ──────────────────────────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Saves the project to a .edaproj JSON file at the given path.</summary>
        public static void Save(ProjectDocument doc, string filePath)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path must not be empty.", nameof(filePath));

            var json = JsonSerializer.Serialize(doc, _jsonOptions);
            File.WriteAllText(filePath, json);
        }

        /// <summary>Loads a .edaproj JSON file and returns the structured ProjectDocument.</summary>
        public static ProjectDocument Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Project file not found: {filePath}");

            var json = File.ReadAllText(filePath);
            var doc = JsonSerializer.Deserialize<ProjectDocument>(json, _jsonOptions);

            if (doc == null)
                throw new InvalidDataException("Project file is empty or corrupt.");

            return doc;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Conversion: Schematic + Canvas Layout ↔ ProjectDocument
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Converts a live schematic + canvas positions into a serializable document.</summary>
        public static ProjectDocument ToDocument(
            Schematic schematic,
            IEnumerable<ComponentPlacementRecord> placements,
            string schematicTitle,
            IEnumerable<NetLabelRecord>? netLabels = null)
        {
            var components = schematic.Components.Values.Select(c => new ComponentRecord
            {
                Id          = c.Id,
                TypeName    = c.GetType().Name,
                Designator  = c.Designator,
                Value       = c.Value,
                FirmwarePath = (c as McuComponent)?.FirmwarePath
            }).ToList();

            var nets = schematic.Nets.Values
                .Where(n => n.Id != schematic.MasterGroundNet.Id) // Ground is auto-created on load
                .Select(n =>
                {
                    var designators = new List<PinDesignatorRecord>();
                    foreach (var pinId in n.ConnectedPinIds)
                    {
                        var component = schematic.Components.Values
                            .FirstOrDefault(c => c.Pins.Any(p => p.Id == pinId));
                        if (component != null)
                        {
                            var pin = component.Pins.First(p => p.Id == pinId);
                            designators.Add(new PinDesignatorRecord
                            {
                                ComponentDesignator = component.Designator,
                                PinName = pin.Name
                            });
                        }
                    }
                    return new NetRecord
                    {
                        Id = n.Id,
                        Name = n.Name,
                        ConnectedPinIds = n.ConnectedPinIds.ToList(),
                        ConnectedPinDesignators = designators
                    };
                }).ToList();

            return new ProjectDocument
            {
                FileVersion = 1,
                SavedAt     = DateTime.UtcNow,
                Title       = schematicTitle,
                Components  = components,
                Nets        = nets,
                Placements  = placements.ToList(),
                NetLabels   = (netLabels ?? Enumerable.Empty<NetLabelRecord>()).ToList()
            };
        }

        /// <summary>Reconstructs a Schematic from a loaded ProjectDocument.</summary>
        public static Schematic FromDocument(ProjectDocument doc)
        {
            var schematic = new Schematic(doc.Title);

            // Re-create each component by type name
            foreach (var rec in doc.Components)
            {
                Component comp = rec.TypeName switch
                {
                    nameof(Resistor)      => new Resistor(rec.Designator, rec.Value ?? "1k"),
                    nameof(Capacitor)     => new Capacitor(rec.Designator, rec.Value ?? "1u"),
                    nameof(Inductor)      => new Inductor(rec.Designator, rec.Value ?? "1m"),
                    nameof(VoltageSource) => new VoltageSource(rec.Designator, rec.Value ?? "DC 5"),
                    nameof(CurrentSource) => new CurrentSource(rec.Designator, rec.Value ?? "DC 1m"),
                    nameof(Diode)         => new Diode(rec.Designator, rec.Value ?? "1N4148"),
                    nameof(BJT)           => new BJT(rec.Designator, rec.Value ?? "2N2222"),
                    nameof(MOSFET)        => new MOSFET(rec.Designator, rec.Value ?? "2N7002"),
                    nameof(OpAmp)         => new OpAmp(rec.Designator, rec.Value ?? "LM358"),
                    nameof(GroundSymbol)  => new GroundSymbol(rec.Designator),
                    nameof(PowerRail)     => new PowerRail(rec.Designator,
                        double.TryParse((rec.Value ?? "5V").Replace("V",""), out double v) ? v : 5.0),
                    nameof(McuComponent)  => new McuComponent(rec.Designator, rec.Value ?? "Arduino Uno R3")
                    {
                        FirmwarePath = rec.FirmwarePath ?? string.Empty
                    },
                    nameof(BlockGainComponent) => new BlockGainComponent(rec.Designator, rec.Value ?? "1.0"),
                    nameof(BlockIntegratorComponent) => new BlockIntegratorComponent(rec.Designator, rec.Value ?? "0.0"),
                    nameof(BlockSumComponent) => new BlockSumComponent(rec.Designator, rec.Value ?? "+-"),
                    nameof(BlockSourceComponent) => new BlockSourceComponent(rec.Designator, rec.Value ?? "Constant 1.0"),
                    nameof(BlockTransferFunctionComponent) => new BlockTransferFunctionComponent(rec.Designator, rec.Value ?? "1 / 1 1"),
                    _ => throw new NotSupportedException($"Unknown component type: {rec.TypeName}")
                };

                schematic.AddComponent(comp);
            }

            // Build a lookup: pin ID → Pin (from newly created components, which have fresh IDs)
            // We must match by designator + pin name since deserialized IDs don't persist
            var designatorToPins = schematic.Components.Values
                .ToDictionary(c => c.Designator, c => c.Pins);

            // Re-create nets and wire up pins
            foreach (var netRec in doc.Nets)
            {
                var net = schematic.CreateNet(netRec.Name);

                // Nets store connected pin IDs — we need to find matching pins in the new model
                // Match by looking at the original document's component records
                foreach (var compRec in doc.Components)
                {
                    if (!designatorToPins.TryGetValue(compRec.Designator, out var pins))
                        continue;

                    foreach (var pin in pins)
                    {
                        // The net record contains pin IDs from the *original* serialization.
                        // Since we can't match by ID (new components have new IDs), we use
                        // a secondary lookup stored in the document format.
                        if (netRec.ConnectedPinDesignators != null &&
                            netRec.ConnectedPinDesignators.Any(pd =>
                                pd.ComponentDesignator == compRec.Designator &&
                                pd.PinName == pin.Name))
                        {
                            schematic.ConnectPinToNet(pin, net.Id);
                        }
                    }
                }
            }

            return schematic;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Data Transfer Objects (DTOs) — the JSON document structure
    // ──────────────────────────────────────────────────────────────────────────────

    public class ProjectDocument
    {
        public int FileVersion { get; set; }
        public DateTime SavedAt { get; set; }
        public string Title { get; set; } = "Untitled";
        public List<ComponentRecord> Components { get; set; } = new();
        public List<NetRecord> Nets { get; set; } = new();
        public List<ComponentPlacementRecord> Placements { get; set; } = new();
        public List<NetLabelRecord> NetLabels { get; set; } = new();
    }

    public class NetLabelRecord
    {
        public string NetName { get; set; } = "";
        public Guid NetId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class ComponentRecord
    {
        public Guid Id { get; set; }
        public string TypeName { get; set; } = "";
        public string Designator { get; set; } = "";
        public string Value { get; set; } = "";
        public string? FirmwarePath { get; set; }
    }

    public class NetRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public List<Guid> ConnectedPinIds { get; set; } = new();
        public List<PinDesignatorRecord>? ConnectedPinDesignators { get; set; }
    }

    public class PinDesignatorRecord
    {
        public string ComponentDesignator { get; set; } = "";
        public string PinName { get; set; } = "";
    }

    public class ComponentPlacementRecord
    {
        public string Designator { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
    }
}
