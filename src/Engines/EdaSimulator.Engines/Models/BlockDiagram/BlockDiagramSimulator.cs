using System;
using System.Collections.Generic;

namespace EdaSimulator.Engines.Models.BlockDiagram
{
    /// <summary>
    /// Executes the unidirectional math blocks diagram simulation over a set time range.
    /// Handles step integration, signal propagation, and loop simulation.
    /// </summary>
    public class BlockDiagramSimulator
    {
        public List<Block> Blocks { get; } = new();
        public List<SignalWire> Wires { get; } = new();

        public void AddBlock(Block block)
        {
            if (block != null && !Blocks.Contains(block))
                Blocks.Add(block);
        }

        public void Connect(Block source, int srcPort, Block target, int targetPort)
        {
            if (source == null || target == null)
                throw new ArgumentNullException("Source and target blocks must not be null.");

            var wire = new SignalWire(source, srcPort, target, targetPort);
            Wires.Add(wire);
        }

        public void Reset()
        {
            foreach (var block in Blocks)
            {
                if (block is IntegratorBlock integrator)
                    integrator.Reset();
                else if (block is SourceBlock source)
                    source.Reset();
                else if (block is TransferFunctionBlock tf)
                    tf.Reset();
                
                // Reset inputs and outputs
                for (int i = 0; i < block.Inputs.Length; i++) block.SetInput(i, 0.0);
                
                // Force a 0.0 step to evaluate initial static outputs
                block.Step(0.0);
            }

            // Perform initial signal propagation
            PropagateSignals();
        }

        public void PropagateSignals()
        {
            foreach (var wire in Wires)
            {
                double val = wire.SourceBlock.GetOutput(wire.SourcePortIndex);
                wire.CurrentValue = val;
                wire.TargetBlock.SetInput(wire.TargetPortIndex, val);
            }
        }

        public void Step(double deltaTime)
        {
            // 1. Propagate outputs from the previous step (or initialization) along the wires to block inputs
            PropagateSignals();

            // 2. Execute step calculation on all blocks
            foreach (var block in Blocks)
            {
                block.Step(deltaTime);
            }
        }

        /// <summary>
        /// Runs the simulation for a specific duration with a fixed step size.
        /// </summary>
        public void Run(double stopTime, double deltaTime, Action<double, BlockDiagramSimulator>? onStep = null)
        {
            Reset();
            double currentTime = 0.0;
            while (currentTime <= stopTime)
            {
                onStep?.Invoke(currentTime, this);
                Step(deltaTime);
                currentTime += deltaTime;
            }
        }
    }
}
