using System;
using System.Collections.Generic;

namespace EdaSimulator.Engines.Simulation.Digital
{
    public class DigitalNode
    {
        public string Name { get; }
        private LogicState _state = LogicState.Undefined;

        public event Action<LogicState>? OnStateChanged;

        public DigitalNode(string name)
        {
            Name = name;
        }

        public LogicState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnStateChanged?.Invoke(_state);
                }
            }
        }
    }

    public abstract class DigitalComponent
    {
        public string Designator { get; }
        public DigitalSimulator Simulator { get; }

        public DigitalComponent(string designator, DigitalSimulator simulator)
        {
            Designator = designator;
            Simulator = simulator;
        }

        // Delay in ticks for the component to propagate inputs to outputs
        public long PropagationDelay { get; set; } = 1; 

        public abstract void Evaluate();
    }
}
