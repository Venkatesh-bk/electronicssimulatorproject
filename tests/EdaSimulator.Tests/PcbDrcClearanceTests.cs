using System;
using System.Linq;
using Xunit;
using EdaSimulator.Engines.PCB;

namespace EdaSimulator.Tests
{
    public class PcbDrcClearanceTests
    {
        [Fact]
        public void RunDrc_ShouldReportTraceToTraceClearanceViolation()
        {
            var pcb = new PcbDocument
            {
                Title = "Clearance Test Board",
                Outline = new PcbBoardOutline { Width_mm = 50, Height_mm = 50 }
            };

            // Two parallel traces on F.Cu belonging to different nets, separated by 0.1 mm
            // Trace widths are 0.2 mm. Centerlines at Y = 10.0 and Y = 10.3 mm.
            // Distance between centerlines is 0.3 mm. 
            // Clearance = 0.3 - (0.2/2) - (0.2/2) = 0.1 mm.
            // Rule MinClearance_mm is 0.15 mm. Should violate!
            pcb.Rules.MinClearance_mm = 0.15;

            var t1 = new PcbTrace
            {
                StartX = 5.0, StartY = 10.0,
                EndX = 25.0, EndY = 10.0,
                Width_mm = 0.2,
                Layer = PcbLayerType.FCu,
                NetName = "NET_A"
            };

            var t2 = new PcbTrace
            {
                StartX = 5.0, StartY = 10.3,
                EndX = 25.0, EndY = 10.3,
                Width_mm = 0.2,
                Layer = PcbLayerType.FCu,
                NetName = "NET_B"
            };

            pcb.Traces.Add(t1);
            pcb.Traces.Add(t2);

            var drc = new PcbDrcEngine();
            var result = drc.RunDrc(pcb);

            Assert.False(result.Passed);
            var clearanceViolation = result.Violations.FirstOrDefault(v => v.Rule == "Clearance Violation");
            Assert.NotNull(clearanceViolation);
            Assert.Contains("trace 'NET_A' and trace 'NET_B'", clearanceViolation.Message);
        }

        [Fact]
        public void RunDrc_ShouldNotReportTraceToTraceClearanceViolation_IfOnDifferentLayers()
        {
            var pcb = new PcbDocument
            {
                Title = "Clearance Test Board",
                Outline = new PcbBoardOutline { Width_mm = 50, Height_mm = 50 }
            };

            pcb.Rules.MinClearance_mm = 0.15;

            // Same overlapping positions but different layers (FCu vs BCu)
            var t1 = new PcbTrace
            {
                StartX = 5.0, StartY = 10.0,
                EndX = 25.0, EndY = 10.0,
                Width_mm = 0.2,
                Layer = PcbLayerType.FCu,
                NetName = "NET_A"
            };

            var t2 = new PcbTrace
            {
                StartX = 5.0, StartY = 10.3,
                EndX = 25.0, EndY = 10.3,
                Width_mm = 0.2,
                Layer = PcbLayerType.BCu,
                NetName = "NET_B"
            };

            pcb.Traces.Add(t1);
            pcb.Traces.Add(t2);

            var drc = new PcbDrcEngine();
            var result = drc.RunDrc(pcb);

            Assert.True(result.Passed);
            Assert.Empty(result.Violations.Where(v => v.Rule == "Clearance Violation"));
        }
    }
}
