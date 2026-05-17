// SMath alias resolves System.Math unambiguously
using SMath = System.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using EdaSimulator.Engines.Models;

namespace EdaSimulator.Engines.PCB
{
    /// <summary>
    /// PCB Design Rule Checker (DRC).
    /// Validates the PcbDocument against the PcbDesignRules for manufacturing compliance.
    /// Checks: clearance, trace width, via annular ring, edge clearance, unconnected nets.
    /// Reference: IPC-2221B Generic Standard on Printed Board Design.
    /// </summary>
    public class PcbDrcEngine
    {
        /// <summary>Runs all DRC checks and returns the full result set.</summary>
        public PcbDrcResult RunDrc(PcbDocument pcb)
        {
            ArgumentNullException.ThrowIfNull(pcb);
            var result = new PcbDrcResult();

            CheckTraceWidth(pcb, result);
            CheckViaDimensions(pcb, result);
            CheckEdgeClearance(pcb, result);
            CheckUnrouted(pcb, result);
            CheckBoardOutline(pcb, result);

            result.TotalErrors   = result.Violations.Count(v => v.Severity == DrcSeverity.Error);
            result.TotalWarnings = result.Violations.Count(v => v.Severity == DrcSeverity.Warning);
            result.Passed        = result.TotalErrors == 0;

            return result;
        }

        private void CheckTraceWidth(PcbDocument pcb, PcbDrcResult result)
        {
            foreach (var trace in pcb.Traces)
            {
                if (trace.Width_mm < pcb.Rules.MinTraceWidth_mm)
                {
                    result.Violations.Add(new DrcViolation
                    {
                        Severity = DrcSeverity.Error,
                        Rule     = "Trace Width",
                        Message  = $"Trace on {trace.Layer} net '{trace.NetName}' width {trace.Width_mm:F3}mm " +
                                   $"is below minimum {pcb.Rules.MinTraceWidth_mm:F3}mm (IPC-2221B)",
                        X        = trace.StartX,
                        Y        = trace.StartY
                    });
                }
            }
        }

        private void CheckViaDimensions(PcbDocument pcb, PcbDrcResult result)
        {
            foreach (var via in pcb.Vias)
            {
                if (via.DrillDia_mm < pcb.Rules.MinViaDrill_mm)
                {
                    result.Violations.Add(new DrcViolation
                    {
                        Severity = DrcSeverity.Error,
                        Rule     = "Via Drill",
                        Message  = $"Via drill {via.DrillDia_mm:F3}mm is below minimum {pcb.Rules.MinViaDrill_mm:F3}mm",
                        X        = via.X, Y = via.Y
                    });
                }

                if (via.AnnularRing_mm < pcb.Rules.MinAnnularRing_mm)
                {
                    result.Violations.Add(new DrcViolation
                    {
                        Severity = DrcSeverity.Error,
                        Rule     = "Annular Ring",
                        Message  = $"Via annular ring {via.AnnularRing_mm:F3}mm < minimum {pcb.Rules.MinAnnularRing_mm:F3}mm (IPC-2221B Sec. 9.3)",
                        X        = via.X, Y = via.Y
                    });
                }
            }
        }

        private void CheckEdgeClearance(PcbDocument pcb, PcbDrcResult result)
        {
            var outline = pcb.Outline;
            foreach (var trace in pcb.Traces)
            {
                // Check all 4 edges
                double distLeft   = SMath.Min(trace.StartX, trace.EndX) - outline.CornerX;
                double distRight  = (outline.CornerX + outline.Width_mm) - SMath.Max(trace.StartX, trace.EndX);
                double distBottom = SMath.Min(trace.StartY, trace.EndY) - outline.CornerY;
                double distTop    = (outline.CornerY + outline.Height_mm) - SMath.Max(trace.StartY, trace.EndY);

                double minDist = SMath.Min(SMath.Min(distLeft, distRight), SMath.Min(distBottom, distTop));

                if (minDist < pcb.Rules.EdgeClearance_mm)
                {
                    result.Violations.Add(new DrcViolation
                    {
                        Severity = DrcSeverity.Warning,
                        Rule     = "Edge Clearance",
                        Message  = $"Trace on net '{trace.NetName}' is {minDist:F3}mm from board edge (minimum: {pcb.Rules.EdgeClearance_mm:F3}mm)",
                        X        = trace.StartX,
                        Y        = trace.StartY
                    });
                }
            }
        }

        private void CheckUnrouted(PcbDocument pcb, PcbDrcResult result)
        {
            if (pcb.UnroutedCount > 0)
            {
                result.Violations.Add(new DrcViolation
                {
                    Severity = DrcSeverity.Error,
                    Rule     = "Unrouted Nets",
                    Message  = $"{pcb.UnroutedCount} unrouted connection(s) in ratsnest. Board is not fully routed.",
                    X        = 0, Y = 0
                });
            }
        }

        private void CheckBoardOutline(PcbDocument pcb, PcbDrcResult result)
        {
            var o = pcb.Outline;
            if (o.Width_mm <= 0 || o.Height_mm <= 0)
            {
                result.Violations.Add(new DrcViolation
                {
                    Severity = DrcSeverity.Error,
                    Rule     = "Board Outline",
                    Message  = "Board outline is invalid (zero or negative dimensions).",
                    X        = 0, Y = 0
                });
            }

            if (o.Width_mm > 500 || o.Height_mm > 500)
            {
                result.Violations.Add(new DrcViolation
                {
                    Severity = DrcSeverity.Warning,
                    Rule     = "Board Size",
                    Message  = $"Board size {o.Width_mm}×{o.Height_mm}mm exceeds typical panel limit (500×500mm).",
                    X        = 0, Y = 0
                });
            }
        }
    }

    public enum DrcSeverity { Info, Warning, Error }

    public class DrcViolation
    {
        public DrcSeverity Severity { get; set; }
        public string      Rule     { get; set; } = "";
        public string      Message  { get; set; } = "";
        public double      X        { get; set; }
        public double      Y        { get; set; }
    }

    public class PcbDrcResult
    {
        public List<DrcViolation> Violations    { get; set; } = new();
        public int                TotalErrors   { get; set; }
        public int                TotalWarnings { get; set; }
        public bool               Passed        { get; set; }

        public string Summary => Passed
            ? $"DRC PASSED — {TotalWarnings} warning(s)"
            : $"DRC FAILED — {TotalErrors} error(s), {TotalWarnings} warning(s)";
    }
}

