// SMath alias resolves System.Math unambiguously
using SMath = System.Math;
using System;
using System.Collections.Generic;

namespace EdaSimulator.Engines.PCB
{
    // ──────────────────────────────────────────────────────────────────────────────
    // PCB Layer definitions (KiCad / IPC-7351B standard layer names)
    // ──────────────────────────────────────────────────────────────────────────────

    public enum PcbLayerType
    {
        FCu         = 0,    // Front copper (component side)
        BCu         = 31,   // Back copper (solder side)
        FMask       = 36,   // Front solder mask
        BMask       = 37,   // Back solder mask
        FSilkS      = 34,   // Front silkscreen
        BSilkS      = 35,   // Back silkscreen
        FPaste      = 32,   // Front solder paste
        BPaste      = 33,   // Back solder paste
        EdgeCuts    = 44,   // Board outline
        FCrtYd      = 52,   // Front courtyard
        BCrtYd      = 53,   // Back courtyard
        FFab        = 54,   // Front fabrication
        BFab        = 55,   // Back fabrication
        In1Cu       = 1,    // Inner copper layer 1
        In2Cu       = 2,    // Inner copper layer 2
        In3Cu       = 3,    // Inner copper layer 3
        In4Cu       = 4,    // Inner copper layer 4
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // PCB Trace
    // ──────────────────────────────────────────────────────────────────────────────

    public class PcbTrace
    {
        public Guid         Id      { get; } = Guid.NewGuid();
        public double       StartX  { get; set; }
        public double       StartY  { get; set; }
        public double       EndX    { get; set; }
        public double       EndY    { get; set; }
        public double       Width_mm { get; set; } = 0.25;
        public PcbLayerType Layer   { get; set; } = PcbLayerType.FCu;
        public string       NetName { get; set; } = "";

        public double Length_mm => SMath.Sqrt(
            SMath.Pow(EndX - StartX, 2) + SMath.Pow(EndY - StartY, 2));
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // PCB Via
    // ──────────────────────────────────────────────────────────────────────────────

    public class PcbVia
    {
        public Guid         Id          { get; } = Guid.NewGuid();
        public double       X           { get; set; }
        public double       Y           { get; set; }
        public double       DrillDia_mm { get; set; } = 0.3;
        public double       PadDia_mm   { get; set; } = 0.6;
        public PcbLayerType LayerFrom   { get; set; } = PcbLayerType.FCu;
        public PcbLayerType LayerTo     { get; set; } = PcbLayerType.BCu;
        public string       NetName     { get; set; } = "";

        public double AnnularRing_mm => (PadDia_mm - DrillDia_mm) / 2.0;
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // PCB Pad (single pad within a footprint)
    // ──────────────────────────────────────────────────────────────────────────────

    public enum PadType { SMD, THT, NPTH }

    public class PcbPad
    {
        public Guid         Id          { get; } = Guid.NewGuid();
        public string       PadNumber   { get; set; } = "1";
        public PadType      Type        { get; set; } = PadType.SMD;
        public double       X           { get; set; }
        public double       Y           { get; set; }
        public double       Width_mm    { get; set; } = 1.6;
        public double       Height_mm   { get; set; } = 1.6;
        public double       DrillDia_mm { get; set; }
        public PcbLayerType Layer       { get; set; } = PcbLayerType.FCu;
        public string       NetName     { get; set; } = "";
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // PCB Footprint (contains pads + courtyard + silkscreen outline)
    // ──────────────────────────────────────────────────────────────────────────────

    public class PcbFootprint
    {
        public Guid              Id          { get; } = Guid.NewGuid();
        public string            Designator  { get; set; } = "";
        public string            Library     { get; set; } = "";
        public string            FootprintId { get; set; } = "";
        public double            X           { get; set; }
        public double            Y           { get; set; }
        public double            Rotation    { get; set; }
        public bool              IsMirrored  { get; set; }
        public PcbLayerType      Layer       { get; set; } = PcbLayerType.FCu;
        public List<PcbPad>      Pads        { get; set; } = new();
        public string            Value       { get; set; } = "";
        public double            CrtYd_Width_mm  { get; set; } = 5.0;
        public double            CrtYd_Height_mm { get; set; } = 5.0;
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // PCB Ratsnest (unrouted connection)
    // ──────────────────────────────────────────────────────────────────────────────

    public class PcbRatsnestLine
    {
        public string FromDesignator { get; set; } = "";
        public string FromPadNumber  { get; set; } = "";
        public string ToDesignator   { get; set; } = "";
        public string ToPadNumber    { get; set; } = "";
        public string NetName        { get; set; } = "";
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // PCB Board Outline
    // ──────────────────────────────────────────────────────────────────────────────

    public class PcbBoardOutline
    {
        public double Width_mm  { get; set; } = 100.0;
        public double Height_mm { get; set; } = 80.0;
        public double CornerX   { get; set; } = 0.0;
        public double CornerY   { get; set; } = 0.0;
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // PCB Design Rules
    // ──────────────────────────────────────────────────────────────────────────────

    public class PcbDesignRules
    {
        /// <summary>Minimum trace width (mm). IPC-2221: 0.1mm for standard, 0.075mm for fine-pitch</summary>
        public double MinTraceWidth_mm      { get; set; } = 0.15;

        /// <summary>Minimum trace-to-trace clearance (mm)</summary>
        public double MinClearance_mm       { get; set; } = 0.15;

        /// <summary>Minimum via drill diameter (mm)</summary>
        public double MinViaDrill_mm        { get; set; } = 0.2;

        /// <summary>Minimum annular ring (mm) for THT vias. IPC-2221: 0.05mm min</summary>
        public double MinAnnularRing_mm     { get; set; } = 0.125;

        /// <summary>Default signal trace width (mm). 0.25mm ≈ 1A on external layer</summary>
        public double DefaultTraceWidth_mm  { get; set; } = 0.25;

        /// <summary>Default via settings (mm)</summary>
        public double DefaultViaDrill_mm    { get; set; } = 0.3;
        public double DefaultViaPad_mm      { get; set; } = 0.6;

        /// <summary>Edge-to-copper clearance (mm). IPC-2221: 0.508mm (20 mil) standard</summary>
        public double EdgeClearance_mm      { get; set; } = 0.5;
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Master PCB Document
    // ──────────────────────────────────────────────────────────────────────────────

    public class PcbDocument
    {
        public Guid                  Id          { get; } = Guid.NewGuid();
        public string                Title       { get; set; } = "Untitled PCB";
        public string                Version     { get; set; } = "1.0";
        public DateTime              CreatedAt   { get; set; } = DateTime.UtcNow;
        public int                   LayerCount  { get; set; } = 2;

        public PcbBoardOutline       Outline     { get; set; } = new();
        public PcbDesignRules        Rules       { get; set; } = new();
        public List<PcbFootprint>    Footprints  { get; set; } = new();
        public List<PcbTrace>        Traces      { get; set; } = new();
        public List<PcbVia>          Vias        { get; set; } = new();
        public List<PcbRatsnestLine> Ratsnest    { get; set; } = new();

        public int UnroutedCount => Ratsnest.Count;
    }
}

