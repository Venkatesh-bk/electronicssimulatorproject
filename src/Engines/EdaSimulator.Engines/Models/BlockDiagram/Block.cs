using System;
using System.Collections.Generic;

namespace EdaSimulator.Engines.Models.BlockDiagram
{
    /// <summary>
    /// Represents a unidirectional data flow connection between two blocks,
    /// core to Simulink-style modeling (as opposed to bidirectional SPICE nets).
    /// </summary>
    public class SignalWire
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Block SourceBlock { get; }
        public int SourcePortIndex { get; }
        public Block TargetBlock { get; }
        public int TargetPortIndex { get; }

        public double CurrentValue { get; set; } = 0.0;

        public SignalWire(Block source, int srcPort, Block target, int targetPort)
        {
            SourceBlock = source;
            SourcePortIndex = srcPort;
            TargetBlock = target;
            TargetPortIndex = targetPort;
        }
    }

    /// <summary>
    /// Base class for all mathematical/signal-processing blocks.
    /// </summary>
    public abstract class Block
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; set; }

        // Unidirectional input and output ports
        protected double[] Inputs { get; set; }
        protected double[] Outputs { get; set; }

        protected Block(string name, int inputCount, int outputCount)
        {
            Name = name;
            Inputs = new double[inputCount];
            Outputs = new double[outputCount];
        }

        public void SetInput(int portIndex, double value)
        {
            if (portIndex >= 0 && portIndex < Inputs.Length)
                Inputs[portIndex] = value;
        }

        public double GetOutput(int portIndex)
        {
            if (portIndex >= 0 && portIndex < Outputs.Length)
                return Outputs[portIndex];
            return 0.0;
        }

        /// <summary>
        /// Computes the outputs based on current inputs and internal state (if any).
        /// Expected to be called exactly once per simulation time-step.
        /// </summary>
        public abstract void Step(double deltaTime);
    }
}
