using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace EdaSimulator.Engines.Physics
{
    /// <summary>
    /// Parses IEEE standard Touchstone (.s1p, .s2p, .sNp) files.
    /// Industry standard for transferring S-Parameter RF/SI network models.
    /// </summary>
    public class TouchstoneParser
    {
        public enum FrequencyUnit { Hz, KHz, MHz, GHz }
        public enum FormatType { MA, DB, RI } // Magnitude-Angle, Decibel-Angle, Real-Imaginary
        public enum ParameterType { S, Y, Z, H, G }

        public static SParameterNetwork Parse(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Touchstone file not found.", filePath);

            var lines = File.ReadAllLines(filePath);
            
            // Determine port count from extension (e.g., .s2p -> 2 ports)
            string extension = Path.GetExtension(filePath).ToLower();
            int ports = 2; // Default
            if (extension.StartsWith(".s") && extension.EndsWith("p"))
            {
                string portStr = extension.Substring(2, extension.Length - 3);
                int.TryParse(portStr, out ports);
            }

            var network = new SParameterNetwork(ports, filePath);

            FrequencyUnit freqUnit = FrequencyUnit.GHz;
            FormatType format = FormatType.MA;
            ParameterType paramType = ParameterType.S;
            double refZ = 50.0;

            bool isOptionLineFound = false;
            
            List<double> rawData = new List<double>();

            // 1. Parse lines
            foreach (string line in lines)
            {
                string tLine = line.Trim();
                if (string.IsNullOrEmpty(tLine) || tLine.StartsWith("!")) continue; // Comment

                // Option line
                if (tLine.StartsWith("#"))
                {
                    ParseOptionLine(tLine, out freqUnit, out paramType, out format, out refZ);
                    network.ReferenceImpedance = refZ;
                    isOptionLineFound = true;
                    continue;
                }

                if (!isOptionLineFound) continue;

                // Data lines
                string[] tokens = tLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    if (token.StartsWith("!")) break; // Inline comment
                    if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                    {
                        rawData.Add(val);
                    }
                }
            }

            if (paramType != ParameterType.S)
                throw new NotSupportedException("Currently only S-parameters are supported in parsing.");

            // 2. Assemble Data into Matrices
            int elementsPerFreq = 1 + (ports * ports * 2); // 1 Freq + (N x N pairs)
            
            // For 2-port networks, Touchstone format is: Freq S11 S21 S12 S22
            // For 3+ ports, format is: Freq S11 S12 S13 ... S1N \n S21 S22 ...
            bool isTwoPort = (ports == 2);

            for (int i = 0; i < rawData.Count; i += elementsPerFreq)
            {
                if (i + elementsPerFreq > rawData.Count) break; // Incomplete block

                double freq = rawData[i];
                double freqHz = ConvertToHz(freq, freqUnit);

                Complex[,] matrix = new Complex[ports, ports];
                int dataIdx = i + 1;

                if (isTwoPort)
                {
                    matrix[0, 0] = ParseComplex(rawData[dataIdx++], rawData[dataIdx++], format);
                    matrix[1, 0] = ParseComplex(rawData[dataIdx++], rawData[dataIdx++], format);
                    matrix[0, 1] = ParseComplex(rawData[dataIdx++], rawData[dataIdx++], format);
                    matrix[1, 1] = ParseComplex(rawData[dataIdx++], rawData[dataIdx++], format);
                }
                else
                {
                    for (int row = 0; row < ports; row++)
                    {
                        for (int col = 0; col < ports; col++)
                        {
                            matrix[row, col] = ParseComplex(rawData[dataIdx++], rawData[dataIdx++], format);
                        }
                    }
                }

                network.AddFrequencyData(freqHz, matrix);
            }

            return network;
        }

        private static void ParseOptionLine(string line, out FrequencyUnit f, out ParameterType p, out FormatType t, out double z)
        {
            f = FrequencyUnit.GHz; p = ParameterType.S; t = FormatType.MA; z = 50.0;
            string[] tokens = line.Substring(1).ToUpper().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < tokens.Length; i++)
            {
                string tok = tokens[i];
                if (tok == "HZ") f = FrequencyUnit.Hz;
                else if (tok == "KHZ") f = FrequencyUnit.KHz;
                else if (tok == "MHZ") f = FrequencyUnit.MHz;
                else if (tok == "GHZ") f = FrequencyUnit.GHz;
                
                else if (tok == "S") p = ParameterType.S;
                else if (tok == "Y") p = ParameterType.Y;
                else if (tok == "Z") p = ParameterType.Z;
                else if (tok == "H") p = ParameterType.H;
                else if (tok == "G") p = ParameterType.G;
                
                else if (tok == "MA") t = FormatType.MA;
                else if (tok == "DB") t = FormatType.DB;
                else if (tok == "RI") t = FormatType.RI;
                
                else if (tok == "R")
                {
                    if (i + 1 < tokens.Length)
                    {
                        double.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out z);
                    }
                }
            }
        }

        private static Complex ParseComplex(double val1, double val2, FormatType format)
        {
            switch (format)
            {
                case FormatType.RI:
                    return new Complex(val1, val2);
                case FormatType.MA: // Magnitude / Angle (degrees)
                    double rads = val2 * System.Math.PI / 180.0;
                    return new Complex(val1 * System.Math.Cos(rads), val1 * System.Math.Sin(rads));
                case FormatType.DB: // Decibel / Angle (degrees)
                    double mag = System.Math.Pow(10, val1 / 20.0);
                    double radsDb = val2 * System.Math.PI / 180.0;
                    return new Complex(mag * System.Math.Cos(radsDb), mag * System.Math.Sin(radsDb));
                default:
                    return Complex.Zero;
            }
        }

        private static double ConvertToHz(double freq, FrequencyUnit unit)
        {
            return unit switch
            {
                FrequencyUnit.Hz => freq,
                FrequencyUnit.KHz => freq * 1e3,
                FrequencyUnit.MHz => freq * 1e6,
                FrequencyUnit.GHz => freq * 1e9,
                _ => freq
            };
        }
    }
}
