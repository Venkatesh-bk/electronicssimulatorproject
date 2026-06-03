using System;
using Xunit;
using EdaSimulator.Engines.Math;
using MathNet.Numerics.LinearAlgebra;
using System.Numerics;

namespace EdaSimulator.Tests
{
    public class MathEngineTests
    {
        [Fact]
        public void MathEngine_CanSolveLinearSystem()
        {
            // System:
            // 3x + 2y - z = 1
            // 2x - 2y + 4z = -2
            // -x + 0.5y - z = 0
            
            var A = MathEngine.CreateMatrix(new double[,] {
                { 3, 2, -1 },
                { 2, -2, 4 },
                { -1, 0.5, -1 }
            });

            var b = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(new double[] { 1, -2, 0 });

            var x = MathEngine.SolveLinearSystem(A, b);

            // Verify solution with a small tolerance for floating point errors
            Assert.True(Math.Abs(x[0] - 1.0) < 1e-6, "x should be 1");
            Assert.True(Math.Abs(x[1] - (-2.0)) < 1e-6, "y should be -2");
            Assert.True(Math.Abs(x[2] - (-2.0)) < 1e-6, "z should be -2");
        }

        [Fact]
        public void MathEngine_CanInvertMatrix()
        {
            var A = MathEngine.CreateMatrix(new double[,] {
                { 4, 7 },
                { 2, 6 }
            });

            var invA = MathEngine.Invert(A);

            // A * invA should be Identity
            var identity = A * invA;

            Assert.True(Math.Abs(identity[0, 0] - 1.0) < 1e-6);
            Assert.True(Math.Abs(identity[0, 1] - 0.0) < 1e-6);
            Assert.True(Math.Abs(identity[1, 0] - 0.0) < 1e-6);
            Assert.True(Math.Abs(identity[1, 1] - 1.0) < 1e-6);
        }

        [Fact]
        public void SignalProcessing_CanComputeFFT()
        {
            // Create a simple DC signal of length 4
            double[] signal = { 1.0, 1.0, 1.0, 1.0 };
            
            Complex[] fft = SignalProcessing.ComputeForwardFFT(signal);
            double[] magnitudes = SignalProcessing.ComputeMagnitudeSpectrum(fft);

            // For a DC signal, the 0th bin should have all the energy, and others 0
            Assert.Equal(4, fft.Length);
            Assert.True(Math.Abs(magnitudes[0] - 4.0) < 1e-6); // DC bin
            Assert.True(Math.Abs(magnitudes[1] - 0.0) < 1e-6);
            Assert.True(Math.Abs(magnitudes[2] - 0.0) < 1e-6);
            Assert.True(Math.Abs(magnitudes[3] - 0.0) < 1e-6);
        }

        [Fact]
        public void SignalProcessing_CanApplyWindowing()
        {
            double[] signal = { 1.0, 1.0, 1.0, 1.0 };
            
            // Hanning window check
            SignalProcessing.ApplyWindow(signal, "Hanning");
            
            // For N=4, i=0: 0.5 * (1 - cos(0)) = 0
            // i=1: 0.5 * (1 - cos(2pi/3)) = 0.5 * (1 - (-0.5)) = 0.75
            // i=2: 0.5 * (1 - cos(4pi/3)) = 0.5 * (1 - (-0.5)) = 0.75
            // i=3: 0.5 * (1 - cos(2pi)) = 0
            Assert.True(Math.Abs(signal[0] - 0.0) < 1e-6);
            Assert.True(Math.Abs(signal[1] - 0.75) < 1e-6);
            Assert.True(Math.Abs(signal[2] - 0.75) < 1e-6);
            Assert.True(Math.Abs(signal[3] - 0.0) < 1e-6);
        }
    }
}
