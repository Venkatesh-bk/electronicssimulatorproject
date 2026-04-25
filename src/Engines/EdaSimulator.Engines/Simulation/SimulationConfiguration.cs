namespace EdaSimulator.Engines.Simulation
{
    /// <summary>
    /// Holds the mathematical/physics execution instructions appended to the netlist 
    /// determining how an external SPICE kernel will process the graph logic.
    /// </summary>
    public class SimulationConfiguration
    {
        public string AnalysisType { get; set; } = "Transient"; 
        
        // Time-Domain analysis configurations
        public string TStep { get; set; } = "1u";
        public string TStop { get; set; } = "10m";

        public string GetSpiceDirective()
        {
            switch (AnalysisType)
            {
                case "Transient":
                    return $".tran {TStep} {TStop}";
                case "DC Sweep":
                    // Fallback stub for later
                    return $".dc";
                default:
                    return string.Empty;
            }
        }
    }
}
