using System;

namespace EdaSimulator.Engines.Simulation.Digital
{
    public class AndGate : DigitalComponent
    {
        public DigitalNode? InputA { get; set; }
        public DigitalNode? InputB { get; set; }
        public DigitalNode? Output { get; set; }

        public AndGate(string designator, DigitalSimulator sim) : base(designator, sim) { }

        public override void Evaluate()
        {
            LogicState newState = (InputA?.State == LogicState.High && InputB?.State == LogicState.High) 
                                    ? LogicState.High : LogicState.Low;
            
            // Handle Undefined states
            if (InputA?.State == LogicState.Undefined || InputB?.State == LogicState.Undefined)
                newState = LogicState.Undefined;

            Simulator.ScheduleEvent(PropagationDelay, () => {
                if (Output != null) Output.State = newState;
            });
        }
    }

    public class OrGate : DigitalComponent
    {
        public DigitalNode? InputA { get; set; }
        public DigitalNode? InputB { get; set; }
        public DigitalNode? Output { get; set; }

        public OrGate(string designator, DigitalSimulator sim) : base(designator, sim) { }

        public override void Evaluate()
        {
            LogicState newState = (InputA?.State == LogicState.High || InputB?.State == LogicState.High) 
                                    ? LogicState.High : LogicState.Low;
            
            if (InputA?.State == LogicState.Undefined && InputB?.State == LogicState.Undefined)
                newState = LogicState.Undefined;

            Simulator.ScheduleEvent(PropagationDelay, () => {
                if (Output != null) Output.State = newState;
            });
        }
    }

    public class NotGate : DigitalComponent
    {
        public DigitalNode? Input { get; set; }
        public DigitalNode? Output { get; set; }

        public NotGate(string designator, DigitalSimulator sim) : base(designator, sim) { }

        public override void Evaluate()
        {
            LogicState newState = LogicState.Undefined;
            if (Input?.State == LogicState.High) newState = LogicState.Low;
            else if (Input?.State == LogicState.Low) newState = LogicState.High;

            Simulator.ScheduleEvent(PropagationDelay, () => {
                if (Output != null) Output.State = newState;
            });
        }
    }

    public class DFlipFlop : DigitalComponent
    {
        public DigitalNode? D { get; set; }
        public DigitalNode? Clk { get; set; }
        public DigitalNode? Q { get; set; }
        public DigitalNode? QNot { get; set; }

        private LogicState _lastClk = LogicState.Undefined;

        public DFlipFlop(string designator, DigitalSimulator sim) : base(designator, sim) { }

        public override void Evaluate()
        {
            // Rising edge detection
            if (Clk?.State == LogicState.High && _lastClk == LogicState.Low)
            {
                LogicState capturedD = D?.State ?? LogicState.Undefined;
                Simulator.ScheduleEvent(PropagationDelay, () => {
                    if (Q != null) Q.State = capturedD;
                    if (QNot != null) QNot.State = capturedD == LogicState.High ? LogicState.Low : (capturedD == LogicState.Low ? LogicState.High : LogicState.Undefined);
                });
            }

            _lastClk = Clk?.State ?? LogicState.Undefined;
        }
    }
}
