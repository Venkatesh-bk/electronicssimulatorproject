using System;
using System.Linq;

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

    public enum SourceType
    {
        Constant,
        Step,
        Sine,
        Square
    }

    /// <summary>
    /// Generates continuous signals (Constant, Step, Sine, Square).
    /// </summary>
    public class SourceBlock : Block
    {
        public SourceType Type { get; set; } = SourceType.Constant;
        public double Amplitude { get; set; } = 1.0;
        public double Frequency { get; set; } = 1.0; // Hz
        public double Offset { get; set; } = 0.0;
        public double Phase { get; set; } = 0.0; // Radians
        public double StepTime { get; set; } = 1.0; // Seconds
        public double StepValue { get; set; } = 1.0;
        
        private double _currentTime = 0.0;

        public SourceBlock(string name) : base(name, 0, 1)
        {
        }

        public void Reset()
        {
            _currentTime = 0.0;
            Outputs[0] = 0.0;
        }

        public override void Step(double deltaTime)
        {
            _currentTime += deltaTime;
            Outputs[0] = Type switch
            {
                SourceType.Constant => Offset + Amplitude,
                SourceType.Step => _currentTime >= StepTime ? StepValue : Offset,
                SourceType.Sine => Offset + Amplitude * System.Math.Sin(2.0 * System.Math.PI * Frequency * _currentTime + Phase),
                SourceType.Square => Offset + Amplitude * (System.Math.Sin(2.0 * System.Math.PI * Frequency * _currentTime + Phase) >= 0 ? 1.0 : -1.0),
                _ => 0.0
            };
        }
    }

    /// <summary>
    /// Sums up multiple input signals with signs (+ or -).
    /// </summary>
    public class SumBlock : Block
    {
        private readonly string[] _signs;

        public SumBlock(string name, string[] signs) : base(name, signs.Length, 1)
        {
            _signs = signs;
        }

        public override void Step(double deltaTime)
        {
            double sum = 0.0;
            for (int i = 0; i < Inputs.Length; i++)
            {
                double val = Inputs[i];
                if (i < _signs.Length && _signs[i] == "-")
                {
                    sum -= val;
                }
                else
                {
                    sum += val;
                }
            }
            Outputs[0] = sum;
        }
    }

    /// <summary>
    /// Simulates a continuous-time transfer function: num(s) / den(s).
    /// Converts to State-Space representation using Controller Canonical Form.
    /// </summary>
    public class TransferFunctionBlock : Block
    {
        private readonly double[] _num;
        private readonly double[] _den;
        private readonly double[] _states;

        public TransferFunctionBlock(string name, double[] numerator, double[] denominator)
            : base(name, 1, 1)
        {
            if (denominator == null || denominator.Length == 0)
                throw new ArgumentException("Denominator cannot be null or empty.");

            // Normalize coefficients so that the leading denominator coefficient is 1.0
            double leadingDen = denominator[denominator.Length - 1];
            if (System.Math.Abs(leadingDen) < 1e-15)
                throw new ArgumentException("Leading coefficient of denominator cannot be zero.");

            _den = denominator.Select(c => c / leadingDen).ToArray();
            
            // Pad numerator with zeros if necessary
            int order = _den.Length - 1;
            _num = new double[order + 1];
            if (numerator != null)
            {
                for (int i = 0; i < numerator.Length && i < _num.Length; i++)
                {
                    _num[i] = numerator[i] / leadingDen;
                }
            }

            _states = new double[order];
        }

        public void Reset()
        {
            Array.Clear(_states, 0, _states.Length);
            Outputs[0] = 0.0;
        }

        public override void Step(double deltaTime)
        {
            double u = Inputs[0];
            int order = _states.Length;

            if (order == 0)
            {
                // Order is 0: static gain
                Outputs[0] = u * _num[0];
                return;
            }

            // Compute derivatives (Controller Canonical Form)
            double[] derivatives = new double[order];
            for (int i = 0; i < order - 1; i++)
            {
                derivatives[i] = _states[i + 1];
            }

            double sumFeedback = 0.0;
            for (int i = 0; i < order; i++)
            {
                sumFeedback += _den[i] * _states[i];
            }
            derivatives[order - 1] = u - sumFeedback;

            // Integrate states (Euler method)
            for (int i = 0; i < order; i++)
            {
                _states[i] += derivatives[i] * deltaTime;
            }

            // Compute output
            double directFeedthrough = _num[order]; // b_n
            double y = directFeedthrough * u;
            for (int i = 0; i < order; i++)
            {
                y += (_num[i] - directFeedthrough * _den[i]) * _states[i];
            }

            Outputs[0] = y;
        }
    }
}
