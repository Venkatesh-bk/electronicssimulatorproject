using System;
using System.IO;
using Xunit;
using EdaSimulator.Engines.Simulation;
using EdaSimulator.Engines.PCB;

namespace EdaSimulator.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void RawFileParser_CanParseDcSweepFormat()
        {
            // Simulate a SPICE raw file containing DC sweep data
            string rawFileContent = 
                "Title: Test DC Sweep Circuit\n" +
                "Date: Sat May 30 00:00:00 2026\n" +
                "Plotname: DC transfer characteristic\n" +
                "Flags: real\n" +
                "No. Variables: 3\n" +
                "No. Points: 2\n" +
                "Variables:\n" +
                "\t0\tv-sweep\tvoltage\n" +
                "\t1\tv(1)\tvoltage\n" +
                "\t2\tv(2)\tvoltage\n" +
                "Values:\n" +
                "  0\t0.000000e+00\n" +
                "\t1.234500e+00\n" +
                "\t2.345600e+00\n" +
                "  1\t1.000000e+00\n" +
                "\t2.469000e+00\n" +
                "\t4.691200e+00\n";

            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".raw");
            try
            {
                File.WriteAllText(tempPath, rawFileContent);
                var data = RawFileParser.Parse(tempPath);

                Assert.NotNull(data);
                Assert.Equal(3, data.Variables.Count);
                Assert.Equal("v-sweep", data.Variables[0]);
                Assert.Equal("v(1)", data.Variables[1]);
                Assert.Equal("v(2)", data.Variables[2]);

                Assert.True(data.DataPoints.ContainsKey("v-sweep"));
                Assert.True(data.DataPoints.ContainsKey("v(1)"));
                Assert.True(data.DataPoints.ContainsKey("v(2)"));

                var sweepVals = data.DataPoints["v-sweep"];
                var v1Vals = data.DataPoints["v(1)"];
                var v2Vals = data.DataPoints["v(2)"];

                Assert.Equal(2, sweepVals.Count);
                Assert.Equal(0.0, sweepVals[0]);
                Assert.Equal(1.0, sweepVals[1]);

                Assert.Equal(1.2345, v1Vals[0]);
                Assert.Equal(2.469, v1Vals[1]);

                Assert.Equal(2.3456, v2Vals[0]);
                Assert.Equal(4.6912, v2Vals[1]);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [Fact]
        public void RawFileParser_CanParseAcSweepFormat()
        {
            // Simulate a complex (AC) raw file containing frequency, magnitude and phase
            string rawFileContent = 
                "Title: Test AC Sweep Circuit\n" +
                "Date: Sat May 30 00:00:00 2026\n" +
                "Plotname: AC Analysis\n" +
                "Flags: complex\n" +
                "No. Variables: 2\n" +
                "No. Points: 2\n" +
                "Variables:\n" +
                "\t0\tfrequency\tfrequency\n" +
                "\t1\tv(out)\tvoltage\n" +
                "Values:\n" +
                "  0\t1.000000e+03,0.000000e+00\n" +
                "\t3.000000e+00,4.000000e+00\n" + // magnitude should be sqrt(3^2 + 4^2) = 5
                "  1\t1.000000e+04,0.000000e+00\n" +
                "\t5.000000e+00,1.200000e+01\n"; // magnitude should be sqrt(5^2 + 12^2) = 13

            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".raw");
            try
            {
                File.WriteAllText(tempPath, rawFileContent);
                var data = RawFileParser.Parse(tempPath);

                Assert.NotNull(data);
                Assert.Equal(2, data.Variables.Count);
                Assert.Equal("frequency", data.Variables[0]);
                Assert.Equal("v(out)", data.Variables[1]);

                Assert.True(data.DataPoints.ContainsKey("frequency"));
                Assert.True(data.DataPoints.ContainsKey("v(out)"));

                var freqVals = data.DataPoints["frequency"];
                var outVals = data.DataPoints["v(out)"];

                Assert.Equal(2, freqVals.Count);
                Assert.Equal(1000.0, freqVals[0]);
                Assert.Equal(10000.0, freqVals[1]);

                Assert.Equal(2, outVals.Count);
                Assert.True(Math.Abs(outVals[0] - 5.0) < 1e-6, $"First mag should be 5, got {outVals[0]}");
                Assert.True(Math.Abs(outVals[1] - 13.0) < 1e-6, $"Second mag should be 13, got {outVals[1]}");
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }


        [Fact]
        public void PcbDrcEngine_DetectsFootprintOverlaps()
        {
            var pcb = new PcbDocument();
            pcb.Outline = new PcbBoardOutline { Width_mm = 100, Height_mm = 80, CornerX = 0, CornerY = 0 };

            // Place two footprints overlapping in the center
            var fp1 = new PcbFootprint
            {
                Designator = "R1",
                X = 50,
                Y = 40,
                CrtYd_Width_mm = 10,
                CrtYd_Height_mm = 8
            };

            var fp2 = new PcbFootprint
            {
                Designator = "R2",
                X = 52, // dx = 2, which is less than minXDist = (10+10)/2 = 10
                Y = 41, // dy = 1, which is less than minYDist = (8+8)/2 = 8
                CrtYd_Width_mm = 10,
                CrtYd_Height_mm = 8
            };

            pcb.Footprints.Add(fp1);
            pcb.Footprints.Add(fp2);

            var engine = new PcbDrcEngine();
            var drcResult = engine.RunDrc(pcb);

            Assert.NotNull(drcResult);
            // Overlap should be flagged
            Assert.Contains(drcResult.Violations, v => v.Rule == "Component Overlap" && v.Message.Contains("R1") && v.Message.Contains("R2"));
        }

        [Fact]
        public void PcbDrcEngine_DetectsFootprintsOutOfBounds()
        {
            var pcb = new PcbDocument();
            pcb.Outline = new PcbBoardOutline { Width_mm = 100, Height_mm = 80, CornerX = 0, CornerY = 0 };

            // Place a footprint overlapping the right edge boundary
            var fp = new PcbFootprint
            {
                Designator = "U1",
                X = 98, // Right edge is at 98 + 5 = 103 (board boundary is 100)
                Y = 40,
                CrtYd_Width_mm = 10,
                CrtYd_Height_mm = 8
            };

            pcb.Footprints.Add(fp);

            var engine = new PcbDrcEngine();
            var drcResult = engine.RunDrc(pcb);

            Assert.NotNull(drcResult);
            // Out of bounds violation should be flagged
            Assert.Contains(drcResult.Violations, v => v.Rule == "Component Out of Bounds" && v.Message.Contains("U1"));
        }
    }
}