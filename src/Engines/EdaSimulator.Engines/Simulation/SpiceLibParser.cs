using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EdaSimulator.Engines.Simulation
{
    public enum SpiceModelType
    {
        Model,
        Subcircuit
    }

    public class SpiceLibraryModel
    {
        public string Name { get; set; } = string.Empty;
        public SpiceModelType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public string RawDefinition { get; set; } = string.Empty;
        
        // For subcircuits, keep track of the pins
        public List<string> Pins { get; set; } = new List<string>();
    }

    /// <summary>
    /// Parses standard SPICE .lib and .mod files to extract component models (e.g., .model D1N4148 D(...)) 
    /// and subcircuits (e.g., .subckt LM358 ...).
    /// </summary>
    public class SpiceLibParser
    {
        public List<SpiceLibraryModel> ParseLibrary(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"SPICE library file not found: {filePath}");

            var models = new List<SpiceLibraryModel>();
            var lines = File.ReadAllLines(filePath);
            
            SpiceLibraryModel? currentModel = null;
            
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("*"))
                {
                    if (currentModel != null)
                    {
                        currentModel.RawDefinition += line + "\n";
                    }
                    continue;
                }

                string lowerLine = line.ToLower();

                // Continuation line (+) belongs to the current definition
                if (line.StartsWith("+") && currentModel != null)
                {
                    currentModel.RawDefinition += line + "\n";
                    continue;
                }

                if (lowerLine.StartsWith(".model"))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        currentModel = new SpiceLibraryModel
                        {
                            Name = parts[1],
                            Type = SpiceModelType.Model,
                            RawDefinition = line + "\n"
                        };
                        models.Add(currentModel);
                    }
                }
                else if (lowerLine.StartsWith(".subckt"))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        currentModel = new SpiceLibraryModel
                        {
                            Name = parts[1],
                            Type = SpiceModelType.Subcircuit,
                            RawDefinition = line + "\n"
                        };
                        
                        // Extract pins
                        for (int p = 2; p < parts.Length; p++)
                        {
                            if (!parts[p].StartsWith("params:", StringComparison.OrdinalIgnoreCase))
                            {
                                currentModel.Pins.Add(parts[p]);
                            }
                            else
                            {
                                break; // Ignore default parameters for now
                            }
                        }
                        
                        models.Add(currentModel);
                    }
                }
                else if (lowerLine.StartsWith(".ends") && currentModel?.Type == SpiceModelType.Subcircuit)
                {
                    currentModel.RawDefinition += line + "\n";
                    currentModel = null; // Close subcircuit
                }
                else if (currentModel?.Type == SpiceModelType.Subcircuit)
                {
                    // Inside a subcircuit definition
                    currentModel.RawDefinition += line + "\n";
                }
                else
                {
                    // If it's a standalone .model that finished its definition lines, we reset.
                    // But if it's just random commands, we ignore.
                    if (currentModel?.Type == SpiceModelType.Model && !line.StartsWith("+"))
                    {
                        currentModel = null;
                    }
                }
            }

            return models;
        }
    }
}
