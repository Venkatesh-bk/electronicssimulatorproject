using System;
using System.Collections.Generic;
using System.Linq;

namespace EdaSimulator.Engines.Simulation.Digital
{
    public enum LogicState
    {
        Low = 0,
        High = 1,
        HighZ = 2,
        Undefined = 3
    }

    public class DigitalEvent
    {
        public long TimeTicks { get; set; }
        public Action? Action { get; set; }
    }

    /// <summary>
    /// Event-driven logic simulator.
    /// Processes discrete digital events chronologically.
    /// </summary>
    public class DigitalSimulator
    {
        private SortedList<long, List<DigitalEvent>> _eventQueue = new SortedList<long, List<DigitalEvent>>();
        public long CurrentTimeTicks { get; private set; } = 0;

        public void ScheduleEvent(long delayTicks, Action action)
        {
            long targetTime = CurrentTimeTicks + delayTicks;
            if (!_eventQueue.ContainsKey(targetTime))
            {
                _eventQueue[targetTime] = new List<DigitalEvent>();
            }
            _eventQueue[targetTime].Add(new DigitalEvent { TimeTicks = targetTime, Action = action });
        }

        public void Run(long maxTicks = 100000)
        {
            while (_eventQueue.Count > 0 && CurrentTimeTicks <= maxTicks)
            {
                var kvp = _eventQueue.First();
                CurrentTimeTicks = kvp.Key;
                
                // Process all events at this exact time tick
                foreach (var ev in kvp.Value)
                {
                    ev.Action?.Invoke();
                }

                _eventQueue.RemoveAt(0);
            }
        }
    }
}
