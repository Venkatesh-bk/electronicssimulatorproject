using System;

namespace EdaSimulator.Engines.Models.BlockDiagram
{
    /// <summary>
    /// Multiplies the input signal by a constant gain factor.
    /// </summary>
    public class GainBlock : Block
    {
        public double Gain { get; set; }

        public GainBlock(string name, double gain = 1.0) : base(name, 1, 1)
        {
            Gain = gain;
        }

        public override void Step(double deltaTime)
        {
            // Output = Input * Gain
            Outputs[0] = Inputs[0] * Gain;
        }
    }

    /// <summary>
    /// Integrates the input signal over time using the Trapezoidal rule.
    /// </summary>
    public class IntegratorBlock : Block
    {
        private double _integralState = 0.0;
        private double _lastInput = 0.0;

        public double InitialCondition { get; set; }

        public IntegratorBlock(string name, double initialCondition = 0.0) : base(name, 1, 1)
        {
            InitialCondition = initialCondition;
            _integralState = initialCondition;
        }

        public void Reset()
        {
            _integralState = InitialCondition;
            _lastInput = 0.0;
        }

        public override void Step(double deltaTime)
        {
            // Trapezoidal integration
            _integralState += 0.5 * (Inputs[0] + _lastInput) * deltaTime;
            Outputs[0] = _integralState;

            _lastInput = Inputs[0];
        }
    }
}
