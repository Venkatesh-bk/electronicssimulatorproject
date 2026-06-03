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
    /// Checks: clearance, trace width, via annular ring, edge clearance, unconnected nets,
    /// courtyard overlaps, and out-of-board layout boundaries.
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
            CheckFootprintOverlap(pcb, result);
            CheckFootprintOutOfBounds(pcb, result);
            CheckClearances(pcb, result);

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

        private void CheckFootprintOverlap(PcbDocument pcb, PcbDrcResult result)
        {
            for (int i = 0; i < pcb.Footprints.Count; i++)
            {
                var fp1 = pcb.Footprints[i];
                for (int j = i + 1; j < pcb.Footprints.Count; j++)
                {
                    var fp2 = pcb.Footprints[j];
                    double dx = SMath.Abs(fp1.X - fp2.X);
                    double dy = SMath.Abs(fp1.Y - fp2.Y);
                    double minXDist = (fp1.CrtYd_Width_mm + fp2.CrtYd_Width_mm) / 2.0;
                    double minYDist = (fp1.CrtYd_Height_mm + fp2.CrtYd_Height_mm) / 2.0;

                    if (dx < minXDist && dy < minYDist)
                    {
                        result.Violations.Add(new DrcViolation
                        {
                            Severity = DrcSeverity.Error,
                            Rule     = "Component Overlap",
                            Message  = $"Footprints '{fp1.Designator}' and '{fp2.Designator}' overlap. Courtyards collide.",
                            X        = (fp1.X + fp2.X) / 2.0,
                            Y        = (fp1.Y + fp2.Y) / 2.0
                        });
                    }
                }
            }
        }

        private void CheckFootprintOutOfBounds(PcbDocument pcb, PcbDrcResult result)
        {
            var outline = pcb.Outline;
            foreach (var fp in pcb.Footprints)
            {
                double left   = fp.X - fp.CrtYd_Width_mm / 2.0;
                double right  = fp.X + fp.CrtYd_Width_mm / 2.0;
                double top    = fp.Y + fp.CrtYd_Height_mm / 2.0;
                double bottom = fp.Y - fp.CrtYd_Height_mm / 2.0;

                bool outOfBounds = left < outline.CornerX || 
                                   right > (outline.CornerX + outline.Width_mm) || 
                                   bottom < outline.CornerY || 
                                   top > (outline.CornerY + outline.Height_mm);

                if (outOfBounds)
                {
                    result.Violations.Add(new DrcViolation
                    {
                        Severity = DrcSeverity.Error,
                        Rule     = "Component Out of Bounds",
                        Message  = $"Footprint '{fp.Designator}' is outside or overlaps the board boundary.",
                        X        = fp.X,
                        Y        = fp.Y
                    });
                }
            }
        }

        private void CheckClearances(PcbDocument pcb, PcbDrcResult result)
        {
            double minClearance = pcb.Rules.MinClearance_mm;

            // 1. Trace to Trace Clearance (same layer, different nets)
            for (int i = 0; i < pcb.Traces.Count; i++)
            {
                var t1 = pcb.Traces[i];
                for (int j = i + 1; j < pcb.Traces.Count; j++)
                {
                    var t2 = pcb.Traces[j];
                    if (t1.Layer == t2.Layer && t1.NetName != t2.NetName)
                    {
                        double dist = DistanceSegmentToSegment(t1.StartX, t1.StartY, t1.EndX, t1.EndY, t2.StartX, t2.StartY, t2.EndX, t2.EndY);
                        double clearance = dist - (t1.Width_mm / 2.0) - (t2.Width_mm / 2.0);
                        if (clearance < minClearance)
                        {
                            result.Violations.Add(new DrcViolation
                            {
                                Severity = DrcSeverity.Error,
                                Rule     = "Clearance Violation",
                                Message  = $"Clearance between trace '{t1.NetName}' and trace '{t2.NetName}' is {clearance:F3}mm, below minimum {minClearance:F3}mm",
                                X        = (t1.StartX + t2.StartX) / 2.0,
                                Y        = (t1.StartY + t2.StartY) / 2.0
                            });
                        }
                    }
                }
            }

            // 2. Trace to Pad Clearance (same layer, different nets)
            var padsWithFootprints = pcb.Footprints
                .SelectMany(fp => fp.Pads.Select(pad => new { Fp = fp, Pad = pad, Pos = GetAbsolutePadPosition(fp, pad) }))
                .ToList();

            foreach (var trace in pcb.Traces)
            {
                foreach (var padInfo in padsWithFootprints)
                {
                    if (trace.Layer == padInfo.Pad.Layer && trace.NetName != padInfo.Pad.NetName)
                    {
                        double dist = DistancePointToSegment(padInfo.Pos.X, padInfo.Pos.Y, trace.StartX, trace.StartY, trace.EndX, trace.EndY);
                        double padRad = SMath.Min(padInfo.Pad.Width_mm, padInfo.Pad.Height_mm) / 2.0;
                        double clearance = dist - (trace.Width_mm / 2.0) - padRad;
                        if (clearance < minClearance)
                        {
                            result.Violations.Add(new DrcViolation
                            {
                                Severity = DrcSeverity.Error,
                                Rule     = "Clearance Violation",
                                Message  = $"Clearance between trace '{trace.NetName}' and pad '{padInfo.Fp.Designator}-{padInfo.Pad.PadNumber}' (net '{padInfo.Pad.NetName}') is {clearance:F3}mm, below minimum {minClearance:F3}mm",
                                X        = padInfo.Pos.X,
                                Y        = padInfo.Pos.Y
                            });
                        }
                    }
                }
            }

            // 3. Pad to Pad Clearance (same layer, different nets)
            for (int i = 0; i < padsWithFootprints.Count; i++)
            {
                var p1 = padsWithFootprints[i];
                for (int j = i + 1; j < padsWithFootprints.Count; j++)
                {
                    var p2 = padsWithFootprints[j];
                    if (p1.Pad.Layer == p2.Pad.Layer && p1.Pad.NetName != p2.Pad.NetName)
                    {
                        double dx = p1.Pos.X - p2.Pos.X;
                        double dy = p1.Pos.Y - p2.Pos.Y;
                        double dist = SMath.Sqrt(dx * dx + dy * dy);
                        double r1 = SMath.Min(p1.Pad.Width_mm, p1.Pad.Height_mm) / 2.0;
                        double r2 = SMath.Min(p2.Pad.Width_mm, p2.Pad.Height_mm) / 2.0;
                        double clearance = dist - r1 - r2;
                        if (clearance < minClearance)
                        {
                            result.Violations.Add(new DrcViolation
                            {
                                Severity = DrcSeverity.Error,
                                Rule     = "Clearance Violation",
                                Message  = $"Clearance between pad '{p1.Fp.Designator}-{p1.Pad.PadNumber}' (net '{p1.Pad.NetName}') and pad '{p2.Fp.Designator}-{p2.Pad.PadNumber}' (net '{p2.Pad.NetName}') is {clearance:F3}mm, below minimum {minClearance:F3}mm",
                                X        = (p1.Pos.X + p2.Pos.X) / 2.0,
                                Y        = (p1.Pos.Y + p2.Pos.Y) / 2.0
                            });
                        }
                    }
                }
            }
        }

        private static (double X, double Y) GetAbsolutePadPosition(PcbFootprint fp, PcbPad pad)
        {
            double rad = fp.Rotation * SMath.PI / 180.0;
            double cos = SMath.Cos(rad);
            double sin = SMath.Sin(rad);
            double ax = fp.X + (pad.X * cos - pad.Y * sin);
            double ay = fp.Y + (pad.X * sin + pad.Y * cos);
            return (ax, ay);
        }

        private static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            if (dx * dx + dy * dy < 1e-9)
            {
                return SMath.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
            }
            double t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
            t = SMath.Max(0, SMath.Min(1, t));
            double closestX = x1 + t * dx;
            double closestY = y1 + t * dy;
            return SMath.Sqrt((px - closestX) * (px - closestX) + (py - closestY) * (py - closestY));
        }

        private static bool SegmentsIntersect(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
        {
            double d = (x2 - x1) * (y4 - y3) - (y2 - y1) * (x4 - x3);
            if (SMath.Abs(d) < 1e-9) return false;
            double u = ((x3 - x1) * (y4 - y3) - (y3 - y1) * (x4 - x3)) / d;
            double v = ((x3 - x1) * (y2 - y1) - (y3 - y1) * (x2 - x1)) / d;
            return u >= 0 && u <= 1 && v >= 0 && v <= 1;
        }

        private static double DistanceSegmentToSegment(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
        {
            if (SegmentsIntersect(x1, y1, x2, y2, x3, y3, x4, y4)) return 0;
            double d1 = DistancePointToSegment(x1, y1, x3, y3, x4, y4);
            double d2 = DistancePointToSegment(x2, y2, x3, y3, x4, y4);
            double d3 = DistancePointToSegment(x3, y3, x1, y1, x2, y2);
            double d4 = DistancePointToSegment(x4, y4, x1, y1, x2, y2);
            return SMath.Min(SMath.Min(d1, d2), SMath.Min(d3, d4));
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
