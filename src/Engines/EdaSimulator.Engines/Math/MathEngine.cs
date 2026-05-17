using System;
using MathNet.Numerics.LinearAlgebra;

namespace EdaSimulator.Engines.Math
{
    /// <summary>
    /// Core Math Engine utilizing Math.NET Numerics for high-performance matrix operations.
    /// This forms the foundation of the Phase 5 mathematical toolbox (MATLAB equivalent).
    /// </summary>
    public class MathEngine
    {
        private static readonly MatrixBuilder<double> M = Matrix<double>.Build;
        private static readonly VectorBuilder<double> V = Vector<double>.Build;

        /// <summary>
        /// Creates a dense matrix of the given size.
        /// </summary>
        public static Matrix<double> CreateMatrix(int rows, int cols)
        {
            return M.Dense(rows, cols);
        }

        /// <summary>
        /// Creates a dense matrix from a 2D array.
        /// </summary>
        public static Matrix<double> CreateMatrix(double[,] data)
        {
            return M.DenseOfArray(data);
        }

        /// <summary>
        /// Solves the linear system Ax = b for x.
        /// Throws an exception if the matrix A is singular (not invertible).
        /// </summary>
        public static Vector<double> SolveLinearSystem(Matrix<double> A, Vector<double> b)
        {
            if (A.RowCount != A.ColumnCount)
                throw new ArgumentException("Matrix A must be square to solve Ax = b.");

            if (A.RowCount != b.Count)
                throw new ArgumentException("Matrix A rows must match Vector b length.");

            // Under the hood, this uses LU factorization (or similar depending on matrix type)
            // which is highly optimized via Math.NET
            return A.Solve(b);
        }

        /// <summary>
        /// Computes the inverse of a matrix.
        /// </summary>
        public static Matrix<double> Invert(Matrix<double> A)
        {
            if (A.RowCount != A.ColumnCount)
                throw new ArgumentException("Matrix must be square to compute inverse.");
            
            return A.Inverse();
        }
    }
}
