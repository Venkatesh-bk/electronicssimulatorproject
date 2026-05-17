using System;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace EdaSimulator.Engines.Math
{
    /// <summary>
    /// Provides Signal Processing capabilities including FFT and windowing,
    /// mimicking MATLAB's Signal Processing Toolbox.
    /// </summary>
    public class SignalProcessing
    {
        /// <summary>
        /// Computes the forward Fast Fourier Transform (FFT) of a real-valued signal.
        /// Returns an array of complex numbers representing the frequency bins.
        /// Note: The input array length must be a power of 2 for pure radix-2 FFT, 
        /// though Math.NET handles arbitrary sizes (Bluestein's algorithm).
        /// </summary>
        public static Complex[] ComputeForwardFFT(double[] realSignal)
        {
            // Convert double[] to Complex[] since MathNet operates on complex arrays in-place
            Complex[] complexSignal = new Complex[realSignal.Length];
            for (int i = 0; i < realSignal.Length; i++)
            {
                complexSignal[i] = new Complex(realSignal[i], 0);
            }

            // Perform in-place forward Fourier transform
            Fourier.Forward(complexSignal, FourierOptions.Matlab);

            return complexSignal;
        }

        /// <summary>
        /// Computes the magnitude (absolute value) of complex frequency bins,
        /// useful for plotting Bode plots or spectrum analysis.
        /// </summary>
        public static double[] ComputeMagnitudeSpectrum(Complex[] frequencyBins)
        {
            double[] magnitudes = new double[frequencyBins.Length];
            for (int i = 0; i < frequencyBins.Length; i++)
            {
                magnitudes[i] = frequencyBins[i].Magnitude;
            }
            return magnitudes;
        }
    }
}
