using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace EdaSimulator.Engines.Simulation
{
    public class SpiceSimulationData
    {
        public IReadOnlyList<string> Variables { get; set; } = new List<string>();
        // Key represents the "v(n1)" variable. Value represents the array of points across time.
        public Dictionary<string, List<double>> DataPoints { get; set; } = new Dictionary<string, List<double>>();
    }

    /// <summary>
    /// Parses the standard SPICE RAW ASCII dataset trace files into a 
    /// memory-optimized mapping of C# arrays usable for WPF charting.
    /// </summary>
    public static class RawFileParser
    {
        public static SpiceSimulationData Parse(string filePath)
        {
            var result = new SpiceSimulationData();
            
            if (!File.Exists(filePath))
                return result;

            string[] lines = File.ReadAllLines(filePath);
            bool parsingVariables = false;
            bool parsingValues = false;

            int varCount = 0;
            var varNames = new List<string>();

            // Current state trackers
            int currentVarIndex = 0;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("No. Variables:"))
                {
                    var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2) int.TryParse(parts[1].Trim(), out varCount);
                    continue;
                }

                if (line.StartsWith("Variables:"))
                {
                    parsingVariables = true;
                    parsingValues = false;
                    continue;
                }

                if (line.StartsWith("Values:"))
                {
                    parsingVariables = false;
                    parsingValues = true;
                    
                    // Initialize the dictionary based on variables
                    foreach (var varName in varNames)
                    {
                        result.DataPoints[varName] = new List<double>();
                    }
                    continue;
                }

                if (parsingVariables)
                {
                    // Format: 0   time   time
                    var parts = line.Split('\t', ' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        varNames.Add(parts[1].ToLower()); // standardizes v(1) to lowercase
                    }
                }
                else if (parsingValues)
                {
                    // The first variable holds an index counter " 0 0.00 "
                    // The subsequent variables are "   5.00 "
                    var parts = line.Split('\t', ' ', StringSplitOptions.RemoveEmptyEntries);
                    
                    string valString = parts.Length == 2 ? parts[1] : parts[0];

                    double traceVal = 0;
                    bool parsedSuccess = false;

                    if (valString.Contains(','))
                    {
                        var complexParts = valString.Split(',');
                        if (complexParts.Length == 2 &&
                            double.TryParse(complexParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double real) &&
                            double.TryParse(complexParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double imag))
                        {
                            traceVal = System.Math.Sqrt(real * real + imag * imag);
                            
                            // Extract phase (in degrees) and store in a parallel key suffix "_phase"
                            double phaseVal = System.Math.Atan2(imag, real) * (180.0 / System.Math.PI);
                            string targetVar = varNames[currentVarIndex];
                            string phaseKey = targetVar + "_phase";
                            if (!result.DataPoints.TryGetValue(phaseKey, out var phaseList))
                            {
                                phaseList = new List<double>();
                                result.DataPoints[phaseKey] = phaseList;
                            }
                            phaseList.Add(phaseVal);

                            parsedSuccess = true;
                        }
                    }
                    else
                    {
                        parsedSuccess = double.TryParse(valString, NumberStyles.Float, CultureInfo.InvariantCulture, out traceVal);
                    }

                    if (parsedSuccess)
                    {
                        string targetVar = varNames[currentVarIndex];
                        result.DataPoints[targetVar].Add(traceVal);
                        
                        currentVarIndex++;
                        if (currentVarIndex >= varCount) currentVarIndex = 0; // Wrap back to the next time segment
                    }
                }
            }

            result.Variables = varNames.AsReadOnly();
            return result;
        }
    }
}
