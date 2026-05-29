using System;
using System.Collections.Generic;
using System.Numerics;

namespace EdaSimulator.Engines.Physics
{
    /// <summary>
    /// Represents a multi-port S-Parameter (Scattering Parameter) network 
    /// extracted from a Touchstone file, essential for Signal Integrity (SI) analysis.
    /// </summary>
    public class SParameterNetwork
    {
        public int NumberOfPorts { get; }
        public string SourceFile { get; }
        public double ReferenceImpedance { get; set; } = 50.0;
        
        // Key: Frequency in Hz, Value: N x N matrix of Complex S-parameters
        public SortedDictionary<double, Complex[,]> DataMatrix { get; }

        public SParameterNetwork(int numberOfPorts, string sourceFile)
        {
            if (numberOfPorts <= 0)
                throw new ArgumentException("Number of ports must be > 0.");
            
            NumberOfPorts = numberOfPorts;
            SourceFile = sourceFile;
            DataMatrix = new SortedDictionary<double, Complex[,]>();
        }

        public void AddFrequencyData(double frequencyHz, Complex[,] sMatrix)
        {
            if (sMatrix.GetLength(0) != NumberOfPorts || sMatrix.GetLength(1) != NumberOfPorts)
                throw new ArgumentException($"S-Matrix dimensions must be {NumberOfPorts}x{NumberOfPorts}");

            DataMatrix[frequencyHz] = sMatrix;
        }

        /// <summary>
        /// Interpolates the S-matrix at a specific frequency.
        /// Useful for AC Sweep solvers or convolution in transient analysis.
        /// </summary>
        public Complex[,] GetMatrixAtFrequency(double targetFreqHz)
        {
            if (DataMatrix.ContainsKey(targetFreqHz))
                return DataMatrix[targetFreqHz];

            if (DataMatrix.Count == 0) return new Complex[NumberOfPorts, NumberOfPorts];

            // Linear interpolation of real and imaginary parts
            double lowerFreq = -1, upperFreq = -1;
            Complex[,] lowerMat = null, upperMat = null;

            foreach (var kvp in DataMatrix)
            {
                if (kvp.Key < targetFreqHz)
                {
                    lowerFreq = kvp.Key;
                    lowerMat = kvp.Value;
                }
                else if (kvp.Key > targetFreqHz)
                {
                    upperFreq = kvp.Key;
                    upperMat = kvp.Value;
                    break;
                }
            }

            // Extrapolations (Hold first or last value — never return null)
            if (lowerMat == null) return upperMat ?? new Complex[NumberOfPorts, NumberOfPorts];
            if (upperMat == null) return lowerMat;

            // Interpolation
            var result = new Complex[NumberOfPorts, NumberOfPorts];
            double ratio = (targetFreqHz - lowerFreq) / (upperFreq - lowerFreq);

            for (int i = 0; i < NumberOfPorts; i++)
            {
                for (int j = 0; j < NumberOfPorts; j++)
                {
                    double real = lowerMat[i, j].Real + ratio * (upperMat[i, j].Real - lowerMat[i, j].Real);
                    double imag = lowerMat[i, j].Imaginary + ratio * (upperMat[i, j].Imaginary - lowerMat[i, j].Imaginary);
                    result[i, j] = new Complex(real, imag);
                }
            }

            return result;
        }
    }
}
