using System;
using System.IO;
using System.Linq;
using Xunit;
using EdaSimulator.Engines.Simulation;
using EdaSimulator.Engines.PCB;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.IO;
using EdaSimulator.Engines.Models.BlockDiagram;
using EdaSimulator.Engines.Physics;

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

        [Fact]
        public void McuComponent_SerializationAndRestoration_PreservesProperties()
        {
            var schematic = new EdaSimulator.Engines.Models.Schematic("MCU Test Project");
            var mcu = new McuComponent("MCU1", "ESP32-WROOM-32")
            {
                FirmwarePath = @"C:\firmware\blink.bin"
            };
            schematic.AddComponent(mcu);

            // Serialize
            var placements = new[]
            {
                new ComponentPlacementRecord { Designator = "MCU1", X = 150, Y = 200 }
            };
            var doc = ProjectFileService.ToDocument(schematic, placements, schematic.Title);

            // Deserialize
            var restoredSchematic = ProjectFileService.FromDocument(doc);
            
            Assert.Single(restoredSchematic.Components);
            var restoredMcu = restoredSchematic.Components.Values.First() as McuComponent;
            
            Assert.NotNull(restoredMcu);
            Assert.Equal("MCU1", restoredMcu.Designator);
            Assert.Equal("ESP32-WROOM-32", restoredMcu.McuType);
            Assert.Equal(@"C:\firmware\blink.bin", restoredMcu.FirmwarePath);
            Assert.Equal(mcu.Pins.Count, restoredMcu.Pins.Count);
        }

        [Fact]
        public void ProjectFileService_SerializationRestoresNetConnectivity_Successfully()
        {
            var schematic = new EdaSimulator.Engines.Models.Schematic("Connectivity Test");
            var r1 = new Resistor("R1", "10k");
            var r2 = new Resistor("R2", "20k");
            schematic.AddComponent(r1);
            schematic.AddComponent(r2);

            var net = schematic.CreateNet("TEST_NET");
            schematic.ConnectPinToNet(r1.Pins[0], net.Id);
            schematic.ConnectPinToNet(r2.Pins[0], net.Id);

            // Serialize
            var placements = new[]
            {
                new ComponentPlacementRecord { Designator = "R1", X = 10, Y = 10 },
                new ComponentPlacementRecord { Designator = "R2", X = 20, Y = 20 }
            };
            var doc = ProjectFileService.ToDocument(schematic, placements, schematic.Title);

            // Deserialize
            var restored = ProjectFileService.FromDocument(doc);

            // Verify connectivity is fully restored
            var restoredR1 = restored.Components.Values.First(c => c.Designator == "R1");
            var restoredR2 = restored.Components.Values.First(c => c.Designator == "R2");

            Assert.Equal("TEST_NET", restored.GetNetNameForPin(restoredR1.Pins[0]));
            Assert.Equal("TEST_NET", restored.GetNetNameForPin(restoredR2.Pins[0]));
        }

        [Fact]
        public void McuComponent_SpiceNetlistExport_GeneratesSubcircuit()
        {
            var schematic = new EdaSimulator.Engines.Models.Schematic("MCU Netlist Test");
            var mcu = new McuComponent("MCU1", "ESP32-WROOM-32");
            schematic.AddComponent(mcu);

            var exporter = new SpiceNetlistExporter();
            string netlist = exporter.GenerateNetlist(schematic, ".op");

            // Verify instance line is correct
            Assert.Contains("XMCU1", netlist);
            Assert.Contains("McuModel_ESP32_WROOM_32", netlist);

            // Verify subcircuit definition exists and is complete
            Assert.Contains(".SUBCKT McuModel_ESP32_WROOM_32", netlist);
            Assert.Contains("R_P_3V3 P_3V3 0 1G", netlist);
            Assert.Contains("R_GPIO0 GPIO0 0 1G", netlist);
            Assert.Contains(".ENDS McuModel_ESP32_WROOM_32", netlist);
        }

        [Fact]
        public void GerberWriter_GenerateAllLayers_ReturnsValidOutputs()
        {
            var pcb = new PcbDocument();
            pcb.Title = "TestBoard";
            pcb.Outline = new PcbBoardOutline { Width_mm = 80, Height_mm = 60, CornerX = 0, CornerY = 0 };

            // Add a footprint
            var fp = new PcbFootprint
            {
                Designator = "U1",
                X = 40,
                Y = 30,
                CrtYd_Width_mm = 10,
                CrtYd_Height_mm = 10
            };
            fp.Pads.Add(new PcbPad { X = -2, Y = 0, Type = PadType.SMD, Layer = PcbLayerType.FCu });
            fp.Pads.Add(new PcbPad { X = 2, Y = 0, Type = PadType.THT, DrillDia_mm = 0.8 });
            pcb.Footprints.Add(fp);

            // Add trace and via
            pcb.Traces.Add(new PcbTrace { StartX = 10, StartY = 10, EndX = 20, EndY = 20, Width_mm = 0.3, Layer = PcbLayerType.FCu, NetName = "Net1" });
            pcb.Vias.Add(new PcbVia { X = 20, Y = 20, DrillDia_mm = 0.4, PadDia_mm = 0.7 });

            var writer = new GerberWriter();
            var files = writer.GenerateAllLayers(pcb);

            Assert.NotNull(files);
            Assert.True(files.ContainsKey("TestBoard-F_Cu.gbr"));
            Assert.True(files.ContainsKey("TestBoard-B_Cu.gbr"));
            Assert.True(files.ContainsKey("TestBoard-Edge_Cuts.gbr"));
            Assert.True(files.ContainsKey("TestBoard.drl"));

            var edgeCuts = files["TestBoard-Edge_Cuts.gbr"];
            Assert.Contains("%MOMM*%", edgeCuts); // Metric unit mode
            Assert.Contains("M02*", edgeCuts);    // End of file

            var drillFile = files["TestBoard.drl"];
            Assert.Contains("METRIC,TZ", drillFile); // Metric, trailing zeros format
            Assert.Contains("M30", drillFile);       // End of program
        }

        [Fact]
        public void RenameNet_MergesNetsAndReconnectsPins_Successfully()
        {
            var schematic = new Schematic("Net Merge Test");
            var comp1 = new Resistor("R1", "1k");
            var comp2 = new Resistor("R2", "2k");
            schematic.AddComponent(comp1);
            schematic.AddComponent(comp2);

            var net1 = schematic.CreateNet("NET_A");
            var net2 = schematic.CreateNet("NET_B");

            schematic.ConnectPinToNet(comp1.Pins[0], net1.Id);
            schematic.ConnectPinToNet(comp2.Pins[0], net2.Id);

            Assert.Equal("NET_A", schematic.GetNetNameForPin(comp1.Pins[0]));
            Assert.Equal("NET_B", schematic.GetNetNameForPin(comp2.Pins[0]));

            // Rename NET_B to NET_A: this should merge NET_B into NET_A
            var survivingId = schematic.RenameNet(net2.Id, "NET_A");

            Assert.Equal(net1.Id, survivingId);
            Assert.Equal("NET_A", schematic.GetNetNameForPin(comp1.Pins[0]));
            Assert.Equal("NET_A", schematic.GetNetNameForPin(comp2.Pins[0]));
            
            // The original net2 should be removed from the schematic
            Assert.Null(schematic.GetNetById(net2.Id));
            Assert.NotNull(schematic.GetNetById(net1.Id));
        }

        [Fact]
        public void BlockDiagramSimulator_IntegratorFeedbackLoop_ComputesCorrectStepResponse()
        {
            var sim = new BlockDiagramSimulator();

            // Step input (starts at t=0, value=1.0)
            var step = new SourceBlock("StepInput")
            {
                Type = SourceType.Constant,
                Amplitude = 1.0,
                Offset = 0.0
            };

            // Summing junction: Error = Input - Output
            var sum = new SumBlock("Sum", new[] { "+", "-" });

            // Integrator: dy/dt = Error
            var integrator = new IntegratorBlock("Integrator", 0.0);

            sim.AddBlock(step);
            sim.AddBlock(sum);
            sim.AddBlock(integrator);

            // Connect Step -> Sum port 0
            sim.Connect(step, 0, sum, 0);
            // Connect Sum -> Integrator
            sim.Connect(sum, 0, integrator, 0);
            // Feedback: Connect Integrator -> Sum port 1
            sim.Connect(integrator, 0, sum, 1);

            // Run simulation for 5 seconds with 1ms step
            double lastOutput = 0.0;
            double outputAt1s = 0.0;

            sim.Run(5.0, 0.001, (t, s) =>
            {
                double outputVal = integrator.GetOutput(0);
                if (Math.Abs(t - 1.0) < 0.0005)
                {
                    outputAt1s = outputVal;
                }
                lastOutput = outputVal;
            });

            // Analytically: y(t) = 1 - e^-t
            // At t = 1.0, y(1) = 1 - e^-1 = 0.63212
            // At t = 5.0, y(5) = 1 - e^-5 = 0.99326
            Assert.True(Math.Abs(outputAt1s - 0.63212) < 0.01, $"Expected ~0.632 at t=1, got {outputAt1s}");
            Assert.True(Math.Abs(lastOutput - 0.99326) < 0.01, $"Expected ~0.993 at t=5, got {lastOutput}");
        }

        [Fact]
        public void BlockDiagramSimulator_TransferFunction_ComputesCorrectResponse()
        {
            var sim = new BlockDiagramSimulator();

            // Step input (starts at t=0, value=1.0)
            var step = new SourceBlock("StepInput")
            {
                Type = SourceType.Constant,
                Amplitude = 1.0,
                Offset = 0.0
            };

            // G(s) = 1 / (s + 1) -> Numerator: [1.0], Denominator: [1.0, 1.0]
            var tf = new TransferFunctionBlock("LowPassFilter", new[] { 1.0 }, new[] { 1.0, 1.0 });

            sim.AddBlock(step);
            sim.AddBlock(tf);

            sim.Connect(step, 0, tf, 0);

            double lastOutput = 0.0;
            double outputAt1s = 0.0;

            sim.Run(5.0, 0.001, (t, s) =>
            {
                double outputVal = tf.GetOutput(0);
                if (Math.Abs(t - 1.0) < 0.0005)
                {
                    outputAt1s = outputVal;
                }
                lastOutput = outputVal;
            });

            // G(s) = 1 / (s + 1) step response is 1 - e^-t
            Assert.True(Math.Abs(outputAt1s - 0.63212) < 0.01, $"Expected ~0.632 at t=1, got {outputAt1s}");
            Assert.True(Math.Abs(lastOutput - 0.99326) < 0.01, $"Expected ~0.993 at t=5, got {lastOutput}");
        }

        [Fact]
        public void SpiceNetlistExport_WithBlockComponents_GeneratesLaplaceAndSubcircuitsCorrectly()
        {
            var schematic = new Schematic("Co-Sim Block Test");
            
            var source = new BlockSourceComponent("XSO1", "Constant 2.5");
            var gain = new BlockGainComponent("XG1", "4.0");
            var integrator = new BlockIntegratorComponent("XI1", "0.0");
            var tf = new BlockTransferFunctionComponent("XTF1", "1 / 1 2 1"); // 1 / (s^2 + 2s + 1)

            schematic.AddComponent(source);
            schematic.AddComponent(gain);
            schematic.AddComponent(integrator);
            schematic.AddComponent(tf);

            // Connect using nets
            var net1 = schematic.CreateNet("NET_SRC");
            var net2 = schematic.CreateNet("NET_GAIN");
            var net3 = schematic.CreateNet("NET_INT");
            var net4 = schematic.CreateNet("NET_OUT");

            schematic.ConnectPinToNet(source.Pins[0], net1.Id);       // OUT -> NET_SRC
            schematic.ConnectPinToNet(gain.Pins[0], net1.Id);         // IN -> NET_SRC
            schematic.ConnectPinToNet(gain.Pins[1], net2.Id);         // OUT -> NET_GAIN
            schematic.ConnectPinToNet(integrator.Pins[0], net2.Id);   // IN -> NET_GAIN
            schematic.ConnectPinToNet(integrator.Pins[1], net3.Id);   // OUT -> NET_INT
            schematic.ConnectPinToNet(tf.Pins[0], net3.Id);           // IN -> NET_INT
            schematic.ConnectPinToNet(tf.Pins[1], net4.Id);           // OUT -> NET_OUT

            var exporter = new SpiceNetlistExporter();
            string netlist = exporter.GenerateNetlist(schematic, ".tran 1u 10m");

            // Verify BlockSource line
            Assert.Contains("XSO1 NET_SRC BlockSourceConst params: val=2.5", netlist);
            
            // Verify BlockGain line
            Assert.Contains("XG1 NET_SRC NET_GAIN BlockGain params: gain=4.0", netlist);

            // Verify BlockIntegrator line
            Assert.Contains("XI1 NET_GAIN NET_INT BlockIntegrator params: ic=0.0", netlist);

            // Verify BlockTransferFunction line (translates to E-source with Laplace)
            // Designator "XTF1" has prefix 'X' stripped when translated to E source -> ETF1
            Assert.Contains("ETF1 NET_OUT 0 laplace {V(NET_INT)} = { 1 / (s^2 + 2*s + 1) }", netlist);

            // Verify subcircuit library output is appended
            Assert.Contains(".SUBCKT BlockGain IN OUT params: gain=1", netlist);
            Assert.Contains(".SUBCKT BlockIntegrator IN OUT params: ic=0", netlist);
            Assert.Contains(".SUBCKT BlockSourceConst OUT params: val=1", netlist);
        }

        [Fact]
        public void ResearchDatabaseService_LoadsConsolidatedJsonCorrectly()
        {
            var service = ResearchDatabaseService.Instance;
            service.LoadDatabase(); // ensure fresh load

            Assert.True(service.IsLoaded, "Database should load successfully");
            Assert.NotEmpty(service.Content.WideBandgapSemiconductors);
            Assert.NotEmpty(service.Content.BsimNodeStatistics);
            Assert.NotEmpty(service.Content.ChipletThermalProfiles);
            Assert.NotEmpty(service.Content.OpenSourcePdkDistributions);

            // Assert specific items are present
            var gan = service.Content.WideBandgapSemiconductors.FirstOrDefault(m => m.Material == "GaN");
            Assert.NotNull(gan);
            Assert.Equal(3.4, gan.Bandgap_eV);

            var node3 = service.Content.BsimNodeStatistics.FirstOrDefault(n => n.NodeNm == 3);
            Assert.NotNull(node3);
            Assert.Equal("GAAFET", node3.GeometryType);

            var pdkSky = service.Content.OpenSourcePdkDistributions["SKY130_NMOS_1V8"];
            Assert.NotNull(pdkSky);
            Assert.True(pdkSky.Vth0Mean > 0);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Phase 9: FreeRouting / Specctra Integration Tests
        // ─────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void SpecctraDsnExporter_GeneratesValidDsnHeader()
        {
            var pcb = new EdaSimulator.Engines.PCB.PcbDocument
            {
                Title   = "TestBoard",
                Outline = new EdaSimulator.Engines.PCB.PcbBoardOutline { Width_mm = 100, Height_mm = 80 }
            };

            string dsn = EdaSimulator.Engines.PCB.SpecctraDsnExporter.Export(pcb);

            Assert.NotNull(dsn);
            // Must start with (pcb …) block
            Assert.Contains("(pcb TestBoard", dsn);
            // Must contain resolution declaration in micrometers
            Assert.Contains("(resolution um 1)", dsn);
            // Must contain F.Cu and B.Cu layer definitions
            Assert.Contains("(layer F.Cu", dsn);
            Assert.Contains("(layer B.Cu", dsn);
            // Must contain board boundary
            Assert.Contains("(boundary", dsn);
            Assert.Contains("(rect pcb", dsn);
        }

        [Fact]
        public void SpecctraDsnExporter_EmitsNetworkAndPinsForRatsnest()
        {
            var pcb = new EdaSimulator.Engines.PCB.PcbDocument
            {
                Title   = "NetBoard",
                Outline = new EdaSimulator.Engines.PCB.PcbBoardOutline { Width_mm = 50, Height_mm = 40 }
            };

            // Add two footprints with one pad each
            var fp1 = new EdaSimulator.Engines.PCB.PcbFootprint { Designator = "R1", FootprintId = "R_0402", X = 10, Y = 10 };
            fp1.Pads.Add(new EdaSimulator.Engines.PCB.PcbPad { PadNumber = "1", X = 0, Y = 0 });
            pcb.Footprints.Add(fp1);

            var fp2 = new EdaSimulator.Engines.PCB.PcbFootprint { Designator = "R2", FootprintId = "R_0402", X = 30, Y = 10 };
            fp2.Pads.Add(new EdaSimulator.Engines.PCB.PcbPad { PadNumber = "1", X = 0, Y = 0 });
            pcb.Footprints.Add(fp2);

            // Ratsnest connection between R1-pad1 and R2-pad1 on net "VCC"
            pcb.Ratsnest.Add(new EdaSimulator.Engines.PCB.PcbRatsnestLine
            {
                NetName        = "VCC",
                FromDesignator = "R1",
                FromPadNumber  = "1",
                ToDesignator   = "R2",
                ToPadNumber    = "1"
            });

            string dsn = EdaSimulator.Engines.PCB.SpecctraDsnExporter.Export(pcb);

            // Network section must appear
            Assert.Contains("(network", dsn);
            // Net name VCC must be declared
            Assert.Contains("(net VCC", dsn);
            // Both pins must appear inside the net
            Assert.Contains("R1-1", dsn);
            Assert.Contains("R2-1", dsn);
            // Placement section must appear with both footprints
            Assert.Contains("(placement", dsn);
            Assert.Contains("R1", dsn);
            Assert.Contains("R2", dsn);
        }

        [Fact]
        public void SpecctraSessionImporter_ParsesRoutedTracesAndVias()
        {
            // Simulate a minimal FreeRouting .ses output
            const string sesContent =
                "(session board\n" +
                "  (base_design board.dsn)\n" +
                "  (routes\n" +
                "    (resolution um 1)\n" +
                "    (parser\n" +
                "      (host_cad \"FreeRouting\")\n" +
                "    )\n" +
                "    (library_out)\n" +
                "    (network_out\n" +
                "      (net VCC\n" +
                "        (route\n" +
                "          (wire (path F.Cu 250 10000 10000 30000 10000) (net VCC) (type route))\n" +
                "          (via \"Via[0-1]_800:400\" 30000 15000 (net VCC) (type route))\n" +
                "        )\n" +
                "      )\n" +
                "    )\n" +
                "  )\n" +
                ")\n";

            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ses");
            try
            {
                File.WriteAllText(tempPath, sesContent);

                var pcb = new EdaSimulator.Engines.PCB.PcbDocument();
                // Pre-populate ratsnest to verify it gets cleared
                pcb.Ratsnest.Add(new EdaSimulator.Engines.PCB.PcbRatsnestLine { NetName = "VCC" });

                int count = EdaSimulator.Engines.PCB.SpecctraSessionImporter.Import(tempPath, pcb);

                // 1 trace should be imported
                Assert.True(count >= 1, $"Expected at least 1 segment, got {count}");
                Assert.NotEmpty(pcb.Traces);

                // The trace coordinates should be converted from µm to mm correctly
                var trace = pcb.Traces[0];
                Assert.Equal(10.0, trace.StartX, 3);  // 10000 µm = 10 mm
                Assert.Equal(10.0, trace.StartY, 3);
                Assert.Equal(30.0, trace.EndX,   3);  // 30000 µm = 30 mm
                Assert.Equal(10.0, trace.EndY,   3);
                Assert.Equal("VCC", trace.NetName);
                Assert.Equal(EdaSimulator.Engines.PCB.PcbLayerType.FCu, trace.Layer);

                // Via should be imported
                Assert.NotEmpty(pcb.Vias);
                var via = pcb.Vias[0];
                Assert.Equal(30.0, via.X, 3);  // 30000 µm = 30 mm
                Assert.Equal(15.0, via.Y, 3);  // 15000 µm = 15 mm

                // Ratsnest must be cleared after routing
                Assert.Empty(pcb.Ratsnest);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public void Schematic_GetNetById_ReturnsCorrectNet()
        {
            var schematic = new Schematic("Test Schematic");
            var resistor = new Resistor("R1", "1k");
            schematic.AddComponent(resistor);
            var net = schematic.CreateNet("VCC");
            schematic.ConnectPinToNet(resistor.Pins.First(), net.Id);

            var retrieved = schematic.GetNetById(net.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("VCC", retrieved.Name);
        }

        [Fact]
        public void Net_Name_Change_UpdatesCorrectName()
        {
            var net = new Net("VCC");
            net.Name = "VDD";
            Assert.Equal("VDD", net.Name);
        }

        [Fact]
        public void Net_Name_Change_ThrowsOnWhitespaceOrGround()
        {
            var net = new Net("VCC");
            Assert.Throws<ArgumentException>(() => net.Name = "V D D");
            
            var gnd = new Net("0");
            Assert.Throws<InvalidOperationException>(() => gnd.Name = "GND");
        }

        [Fact]
        public void CentroidExporter_GeneratePickAndPlace_ExportsValidCsv()
        {
            var pcb = new EdaSimulator.Engines.PCB.PcbDocument();
            pcb.Title = "TestBoard";
            pcb.Footprints.Add(new EdaSimulator.Engines.PCB.PcbFootprint
            {
                Designator = "R1",
                Value = "10k",
                FootprintId = "R_0805",
                X = 12.3456,
                Y = 56.789,
                Rotation = 90.0,
                Layer = EdaSimulator.Engines.PCB.PcbLayerType.FCu
            });

            string csv = EdaSimulator.Engines.PCB.CentroidExporter.GeneratePickAndPlace(pcb);

            Assert.Contains("Designator,Value,Package,MidX,MidY,Rotation,Layer", csv);
            Assert.Contains("R1,10k,R_0805,12.346,56.789,90.0,FCu", csv);
        }

        [Fact]
        public void SpiceExecutionService_ParseSpiceErrors_ParsesDiagnosticsCorrectly()
        {
            var netlist = "* Test Netlist\n.options savecurrents\nR1 N_1 0 10k\nV1 N_1 0 DC 5\n.tran 1n 10n\n.end";
            
            // Test 1: Line number syntax error
            var result1 = new EdaSimulator.Engines.Simulation.SpiceExecutionResult();
            var output1 = "Error on line 3 : r1 n_1 0 10k\nUnknown device parameter";
            EdaSimulator.Engines.Simulation.SpiceExecutionService.ParseSpiceErrors(output1, "", netlist, result1);
            Assert.Equal(3, result1.ErrorLineNumber);
            Assert.Equal("R1", result1.AffectedDesignator);

            // Test 2: Singular matrix check node
            var result2 = new EdaSimulator.Engines.Simulation.SpiceExecutionResult();
            var output2 = "Warning: singular matrix:  check node N_1\nSimulation aborted";
            EdaSimulator.Engines.Simulation.SpiceExecutionService.ParseSpiceErrors(output2, "", netlist, result2);
            Assert.Equal("N_1", result2.AffectedNetName);

            // Test 3: Unknown device type
            var result3 = new EdaSimulator.Engines.Simulation.SpiceExecutionResult();
            var output3 = "Error: Unknown device type - X123\nCould not compile netlist";
            EdaSimulator.Engines.Simulation.SpiceExecutionService.ParseSpiceErrors(output3, "", netlist, result3);
            Assert.Equal("X123", result3.AffectedDesignator);
        }

        [Fact]
        public void ModelLibraryService_ImportLibrary_LoadsModelsCorrectly()
        {
            var tempLibFile = Path.Combine(Path.GetTempPath(), "temp_test_lib.lib");
            var sandboxTargetFile = Path.Combine(Path.GetTempPath(), "sandbox_eda_components.lib");
            
            var libContent = ".model TEST_DIODE D(Is=1e-14 Rs=0.1 Cjo=2p)\n.subckt TEST_OPAMP IN+ IN- OUT\n.ends TEST_OPAMP";
            File.WriteAllText(tempLibFile, libContent);
            File.WriteAllText(sandboxTargetFile, "* Sandbox Library");

            var service = EdaSimulator.Engines.Simulation.ModelLibraryService.Instance;
            var originalPath = service.LibraryFilePath;
            service.OverrideLibraryFilePath(sandboxTargetFile);

            try
            {
                var initialCount = service.Models.Count;

                service.ImportLibrary(tempLibFile);

                var newCount = service.Models.Count;
                Assert.True(newCount > initialCount);

                var diodeModel = service.FindModel("TEST_DIODE");
                Assert.NotNull(diodeModel);
                Assert.Equal(EdaSimulator.Engines.Simulation.SpiceModelType.Model, diodeModel.Type);

                var opampModel = service.FindModel("TEST_OPAMP");
                Assert.NotNull(opampModel);
                Assert.Equal(EdaSimulator.Engines.Simulation.SpiceModelType.Subcircuit, opampModel.Type);
                Assert.Equal(3, opampModel.Pins.Count);
            }
            finally
            {
                service.OverrideLibraryFilePath(originalPath);
                if (File.Exists(tempLibFile)) File.Delete(tempLibFile);
                if (File.Exists(sandboxTargetFile)) File.Delete(sandboxTargetFile);
            }
        }

        [Fact]
        public void KiCadImporter_CanParseKicadPcbFile()
        {
            string kicadPcbContent = @"(kicad_pcb (version 20211014) (generator pcbnew)
  (net 0 """")
  (net 1 ""GND"")
  (net 2 ""+5V"")
  (net 3 ""Net-(R1-Pad2)"")

  (gr_line (start 10 20) (end 110 20) (stroke (width 0.1)) (layer ""Edge.Cuts""))
  (gr_line (start 110 20) (end 110 100) (stroke (width 0.1)) (layer ""Edge.Cuts""))
  (gr_line (start 110 100) (end 10 100) (stroke (width 0.1)) (layer ""Edge.Cuts""))
  (gr_line (start 10 100) (end 10 20) (stroke (width 0.1)) (layer ""Edge.Cuts""))

  (footprint ""Resistor_SMD:R_0805_2012Metric"" (at 50 60 90)
    (descr ""Resistor SMD 0805 (2012 Metric)"")
    (property ""Reference"" ""R1"" (at 0 -1.65 90))
    (property ""Value"" ""10k"" (at 0 1.65 90))
    (pad ""1"" smd roundrect (at -0.95 0 90) (size 0.7 1.3) (drill 0.2) (layers ""F.Cu"" ""F.Paste"" ""F.Mask"") (net 2 ""+5V""))
    (pad ""2"" smd roundrect (at 0.95 0 90) (size 0.7 1.3) (layers ""F.Cu"" ""F.Paste"" ""F.Mask"") (net 3 ""Net-(R1-Pad2)""))
  )

  (segment (start 50 60) (end 75 60) (width 0.25) (layer ""F.Cu"") (net 2))
  (segment (start 75 60) (end 75 80) (width 0.25) (layer ""B.Cu"") (net 2))

  (via (at 75 60) (size 0.6) (drill 0.3) (layers ""F.Cu"" ""B.Cu"") (net 2))
)";

            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".kicad_pcb");
            try
            {
                File.WriteAllText(tempPath, kicadPcbContent);
                var pcb = KiCadImporter.Import(tempPath);

                Assert.NotNull(pcb);
                
                // Verify Outline
                Assert.Equal(10, pcb.Outline.CornerX);
                Assert.Equal(20, pcb.Outline.CornerY);
                Assert.Equal(100, pcb.Outline.Width_mm);
                Assert.Equal(80, pcb.Outline.Height_mm);

                // Verify Footprint
                Assert.Single(pcb.Footprints);
                var fp = pcb.Footprints[0];
                Assert.Equal("Resistor_SMD", fp.Library);
                Assert.Equal("R_0805_2012Metric", fp.FootprintId);
                Assert.Equal("R1", fp.Designator);
                Assert.Equal("10k", fp.Value);
                Assert.Equal(50, fp.X);
                Assert.Equal(60, fp.Y);
                Assert.Equal(90, fp.Rotation);

                // Verify Pads (with rotation logic)
                Assert.Equal(2, fp.Pads.Count);
                
                // Pad 1 relative position is (-0.95, 0).
                // Footprint rotation is 90 degrees.
                // Rotated offset: dx * cos(90) - dy * sin(90) = 0 - 0 = 0.
                // dy: dx * sin(90) + dy * cos(90) = -0.95 * 1 + 0 = -0.95.
                // So absolute position should be (50, 60 - 0.95) = (50, 59.05).
                var pad1 = fp.Pads.First(p => p.PadNumber == "1");
                Assert.Equal(50.0, pad1.X, 3);
                Assert.Equal(59.05, pad1.Y, 3);
                Assert.Equal(PadType.SMD, pad1.Type);
                Assert.Equal("+5V", pad1.NetName);

                // Verify Traces
                Assert.Equal(2, pcb.Traces.Count);
                var trace1 = pcb.Traces[0];
                Assert.Equal(50, trace1.StartX);
                Assert.Equal(60, trace1.StartY);
                Assert.Equal(75, trace1.EndX);
                Assert.Equal(60, trace1.EndY);
                Assert.Equal(0.25, trace1.Width_mm);
                Assert.Equal(PcbLayerType.FCu, trace1.Layer);
                Assert.Equal("+5V", trace1.NetName);

                // Verify Vias
                Assert.Single(pcb.Vias);
                var via = pcb.Vias[0];
                Assert.Equal(75, via.X);
                Assert.Equal(60, via.Y);
                Assert.Equal(0.6, via.PadDia_mm);
                Assert.Equal(0.3, via.DrillDia_mm);
                Assert.Equal(PcbLayerType.FCu, via.LayerFrom);
                Assert.Equal(PcbLayerType.BCu, via.LayerTo);
                Assert.Equal("+5V", via.NetName);
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
        public void BomGenerator_ExportBomToPdf_CanGeneratePdfFile()
        {
            var schematic = new Schematic("PDF Test Project");
            
            var r1 = new Resistor("R1", "10k");
            var r2 = new Resistor("R2", "10k");
            var c1 = new Capacitor("C1", "100nF");
            
            schematic.AddComponent(r1);
            schematic.AddComponent(r2);
            schematic.AddComponent(c1);

            var bom = BomGenerator.GenerateBom(schematic);
            string tempPdfPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");

            try
            {
                BomGenerator.ExportBomToPdf(bom, schematic.Title, tempPdfPath);

                Assert.True(File.Exists(tempPdfPath));
                
                string content = File.ReadAllText(tempPdfPath);
                Assert.StartsWith("%PDF-1.4", content);
                Assert.Contains("PDF Test Project", content);
                Assert.Contains("BILL OF MATERIALS", content);
                Assert.Contains("R1, R2", content);
                Assert.Contains("C1", content);
                Assert.Contains("%%EOF", content);
            }
            finally
            {
                if (File.Exists(tempPdfPath))
                {
                    File.Delete(tempPdfPath);
                }
            }
        }

        [Fact]
        public void ProjectFileService_NetLabelRecordsSerialization_Successfully()
        {
            var schematic = new EdaSimulator.Engines.Models.Schematic("NetLabel Test");
            var net = schematic.CreateNet("MY_CUSTOM_NET");

            var netLabels = new List<NetLabelRecord>
            {
                new NetLabelRecord { NetName = "MY_CUSTOM_NET", NetId = net.Id, X = 120, Y = 340 }
            };

            // Serialize
            var doc = ProjectFileService.ToDocument(
                schematic,
                Enumerable.Empty<ComponentPlacementRecord>(),
                schematic.Title,
                netLabels);

            // Verify NetLabels are in doc
            Assert.Single(doc.NetLabels);
            Assert.Equal("MY_CUSTOM_NET", doc.NetLabels[0].NetName);
            Assert.Equal(net.Id, doc.NetLabels[0].NetId);
            Assert.Equal(120, doc.NetLabels[0].X);
            Assert.Equal(340, doc.NetLabels[0].Y);

            // Deserialize (ProjectFileService.FromDocument restores schematic, NetLabels are checked from doc directly)
            var restoredSchematic = ProjectFileService.FromDocument(doc);
            Assert.NotNull(restoredSchematic);
            Assert.Single(restoredSchematic.Nets.Values.Where(n => n.Name == "MY_CUSTOM_NET"));
        }

        [Fact]
        public void PcbFootprint_CustomizationAndPadUpdating_Successfully()
        {
            var fp = new EdaSimulator.Engines.PCB.PcbFootprint
            {
                Designator = "R1",
                Value = "10k",
                FootprintId = "R_0805",
                CrtYd_Width_mm = 5.0,
                CrtYd_Height_mm = 4.0
            };

            // Assert defaults
            Assert.Empty(fp.Pads);
            Assert.Equal(5.0, fp.CrtYd_Width_mm);

            // Add pad
            var pad = new EdaSimulator.Engines.PCB.PcbPad
            {
                PadNumber = "1",
                Type = EdaSimulator.Engines.PCB.PadType.SMD,
                X = -1.0,
                Y = 0.0,
                Width_mm = 1.0,
                Height_mm = 1.2,
                Layer = EdaSimulator.Engines.PCB.PcbLayerType.FCu
            };
            fp.Pads.Add(pad);

            Assert.Single(fp.Pads);
            Assert.Equal("1", fp.Pads[0].PadNumber);
            Assert.Equal(EdaSimulator.Engines.PCB.PadType.SMD, fp.Pads[0].Type);

            // Modify pad properties
            fp.Pads[0].X = -1.5;
            fp.Pads[0].DrillDia_mm = 0.5;
            Assert.Equal(-1.5, fp.Pads[0].X);
            Assert.Equal(0.5, fp.Pads[0].DrillDia_mm);

            // Verify courtyard dimensions
            Assert.Equal(5.0, fp.CrtYd_Width_mm);
            Assert.Equal(4.0, fp.CrtYd_Height_mm);
        }

        [Fact]
        public void McuCoSimulation_ArduinoFirmware_RunsSuccessfully()
        {
            string tempFirmware = Path.Combine(Path.GetTempPath(), "test_firmware.ino");
            string code = @"
void setup() {
  Serial.begin(9600);
  Serial.println(""System Init Successful"");
}
void loop() {
  Serial.println(""Reading Temperature Sensor"");
  delay(100);
}
";
            File.WriteAllText(tempFirmware, code);

            try
            {
                string output = EdaSimulator.Engines.Simulation.VirtualMcuSimulationEngine.RunCoSimulation(tempFirmware, 0.5);
                Assert.Contains("System Init Successful", output);
                Assert.Contains("Reading Temperature Sensor", output);
                Assert.Contains("Co-simulation finished", output);
            }
            finally
            {
                if (File.Exists(tempFirmware)) File.Delete(tempFirmware);
            }
        }

        [Fact]
        public void McuCoSimulation_PythonFirmware_RunsSuccessfully()
        {
            string tempFirmware = Path.Combine(Path.GetTempPath(), "test_firmware.py");
            string code = @"
print(""ESP32 Booting Python"")
time.sleep(0.2)
print(""Connected to Wifi"")
";
            File.WriteAllText(tempFirmware, code);

            try
            {
                string output = EdaSimulator.Engines.Simulation.VirtualMcuSimulationEngine.RunCoSimulation(tempFirmware, 0.5);
                Assert.Contains("ESP32 Booting Python", output);
                Assert.Contains("Connected to Wifi", output);
            }
            finally
            {
                if (File.Exists(tempFirmware)) File.Delete(tempFirmware);
            }
        }

        [Fact]
        public void AnnotationNote_NetlistComment_GeneratesSuccessfully()
        {
            var note = new EdaSimulator.Engines.Models.Components.AnnotationNote("NOTE1", "Verify input coupling capacitor C1 value");
            var netlistLine = note.GenerateSpiceNetlistLine(new EdaSimulator.Engines.Models.Schematic("TestSchematic"));
            Assert.Equal("* NOTE: Verify input coupling capacitor C1 value", netlistLine);
            Assert.Equal("Verify input coupling capacitor C1 value", note.Value);
        }

        [Fact]
        public void ComponentCopy_StatePreservation_CopiesProperties()
        {
            // Verify potentiometer wiper position preservation
            var pot1 = new EdaSimulator.Engines.Models.Components.Potentiometer("POT1", "10k") { WiperPosition = 0.25 };
            var pot2 = new EdaSimulator.Engines.Models.Components.Potentiometer("POT2", "10k");
            pot2.WiperPosition = pot1.WiperPosition;
            Assert.Equal(0.25, pot2.WiperPosition);

            // Verify switch open/closed status preservation
            var sw1 = new EdaSimulator.Engines.Models.Components.Switch("SW1") { IsClosed = true };
            var sw2 = new EdaSimulator.Engines.Models.Components.Switch("SW2");
            sw2.IsClosed = sw1.IsClosed;
            Assert.True(sw2.IsClosed);

            // Verify MCU firmware path preservation
            var mcu1 = new EdaSimulator.Engines.Models.Components.McuComponent("MCU1", "Arduino Uno R3") { FirmwarePath = "C:\\test\\blink.ino" };
            var mcu2 = new EdaSimulator.Engines.Models.Components.McuComponent("MCU2", "Arduino Uno R3");
            mcu2.FirmwarePath = mcu1.FirmwarePath;
            Assert.Equal("C:\\test\\blink.ino", mcu2.FirmwarePath);
        }

        [Fact]
        public void SpecctraSessionImporter_QuotedNetNamesWithSpaces_ParsesSuccessfully()
        {
            string tempSes = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_quoted.ses");
            string sesContent = @"
(session test_quoted
  (placement
    (component U1
      (place R1 1000 1000 front 0)
    )
  )
  (wiring
    (wire (path F.Cu 250 1000 1000 2000 1000) (net ""VCC 5V"") (type route))
    (via ""Via[0-1]_800:400"" 1500 1500 (net ""GND NET"") (type route))
  )
)
";
            try
            {
                File.WriteAllText(tempSes, sesContent);
                var pcb = new EdaSimulator.Engines.PCB.PcbDocument();
                
                int count = EdaSimulator.Engines.PCB.SpecctraSessionImporter.Import(tempSes, pcb);
                
                Assert.Equal(1, count);
                Assert.Single(pcb.Traces);
                Assert.Equal("VCC 5V", pcb.Traces[0].NetName);
                Assert.Single(pcb.Vias);
                Assert.Equal("GND NET", pcb.Vias[0].NetName);
            }
            finally
            {
                if (File.Exists(tempSes)) File.Delete(tempSes);
            }
        }

        [Fact]
        public void ApplyWindow_ShortOrNullArray_DoesNotCrashOrDivideByZero()
        {
            double[] dataSingle = new double[] { 1.5 };
            EdaSimulator.Engines.Math.SignalProcessing.ApplyWindow(dataSingle, "Hanning");
            Assert.Equal(1.5, dataSingle[0]); // should remain unchanged

            double[] dataEmpty = new double[0];
            EdaSimulator.Engines.Math.SignalProcessing.ApplyWindow(dataEmpty, "Hamming");
            Assert.Empty(dataEmpty);

            EdaSimulator.Engines.Math.SignalProcessing.ApplyWindow(null!, "Blackman"); // should not throw null ref
        }

        [Fact]
        public void LogicGates_ThreeStateTruthTable_EvaluatesCorrectly()
        {
            var sim = new EdaSimulator.Engines.Simulation.Digital.DigitalSimulator();
            
            // Test AND Gate: Low overrides Undefined, but High + Undefined = Undefined
            var andGate = new EdaSimulator.Engines.Simulation.Digital.AndGate("AND1", sim);
            var nodeA = new EdaSimulator.Engines.Simulation.Digital.DigitalNode("A");
            var nodeB = new EdaSimulator.Engines.Simulation.Digital.DigitalNode("B");
            var outAnd = new EdaSimulator.Engines.Simulation.Digital.DigitalNode("Out");
            andGate.InputA = nodeA;
            andGate.InputB = nodeB;
            andGate.Output = outAnd;

            // Scenario 1: Low + Undefined => Low
            nodeA.State = EdaSimulator.Engines.Simulation.Digital.LogicState.Low;
            nodeB.State = EdaSimulator.Engines.Simulation.Digital.LogicState.Undefined;
            andGate.Evaluate();
            sim.Run(10);
            Assert.Equal(EdaSimulator.Engines.Simulation.Digital.LogicState.Low, outAnd.State);

            // Scenario 2: High + Undefined => Undefined
            nodeA.State = EdaSimulator.Engines.Simulation.Digital.LogicState.High;
            nodeB.State = EdaSimulator.Engines.Simulation.Digital.LogicState.Undefined;
            andGate.Evaluate();
            sim.Run(20);
            Assert.Equal(EdaSimulator.Engines.Simulation.Digital.LogicState.Undefined, outAnd.State);

            // Test OR Gate: High overrides Undefined, but Low + Undefined = Undefined
            var orGate = new EdaSimulator.Engines.Simulation.Digital.OrGate("OR1", sim);
            var nodeC = new EdaSimulator.Engines.Simulation.Digital.DigitalNode("C");
            var nodeD = new EdaSimulator.Engines.Simulation.Digital.DigitalNode("D");
            var outOr = new EdaSimulator.Engines.Simulation.Digital.DigitalNode("OutOr");
            orGate.InputA = nodeC;
            orGate.InputB = nodeD;
            orGate.Output = outOr;

            // Scenario 3: High + Undefined => High
            nodeC.State = EdaSimulator.Engines.Simulation.Digital.LogicState.High;
            nodeD.State = EdaSimulator.Engines.Simulation.Digital.LogicState.Undefined;
            orGate.Evaluate();
            sim.Run(30);
            Assert.Equal(EdaSimulator.Engines.Simulation.Digital.LogicState.High, outOr.State);

            // Scenario 4: Low + Undefined => Undefined
            nodeC.State = EdaSimulator.Engines.Simulation.Digital.LogicState.Low;
            nodeD.State = EdaSimulator.Engines.Simulation.Digital.LogicState.Undefined;
            orGate.Evaluate();
            sim.Run(40);
            Assert.Equal(EdaSimulator.Engines.Simulation.Digital.LogicState.Undefined, outOr.State);
        }

        [Fact]
        public async System.Threading.Tasks.Task Simulation_SallenKeyFilter_RunsSuccessfully()
        {
            var schematic = new EdaSimulator.Engines.Models.Schematic("Sallen-Key Active Low-Pass Filter");
            var u1 = new EdaSimulator.Engines.Models.Components.OpAmp("X1", "LM358");
            var r1 = new EdaSimulator.Engines.Models.Components.Resistor("R1", "10k");
            var r2 = new EdaSimulator.Engines.Models.Components.Resistor("R2", "10k");
            var c1 = new EdaSimulator.Engines.Models.Components.Capacitor("C1", "1n");
            var c2 = new EdaSimulator.Engines.Models.Components.Capacitor("C2", "1n");
            
            var vIn = new EdaSimulator.Engines.Models.Components.VoltageSource("V_IN", "SINE(0 5 15.9k)");
            var vcc = new EdaSimulator.Engines.Models.Components.VoltageSource("V_CC", "DC 15");
            var vee = new EdaSimulator.Engines.Models.Components.VoltageSource("V_EE", "DC -15");

            schematic.AddComponent(vIn);
            schematic.AddComponent(r1);
            schematic.AddComponent(r2);
            schematic.AddComponent(c1);
            schematic.AddComponent(c2);
            schematic.AddComponent(u1);
            schematic.AddComponent(vcc);
            schematic.AddComponent(vee);

            var netVin = schematic.CreateNet("VIN_NODE");
            var netMid = schematic.CreateNet("MID_NODE");
            var netP   = schematic.CreateNet("POS_NODE");
            var netOut = schematic.CreateNet("OUT_NODE");
            var netVcc = schematic.CreateNet("VCC_NET");
            var netVee = schematic.CreateNet("VEE_NET");

            schematic.ConnectPinToNet(vIn.GetPinByName("+"), netVin.Id);
            schematic.ConnectPinToNet(r1.GetPinByName("1"), netVin.Id);
            schematic.ConnectPinToNet(r1.GetPinByName("2"), netMid.Id);
            schematic.ConnectPinToNet(r2.GetPinByName("1"), netMid.Id);
            schematic.ConnectPinToNet(c1.GetPinByName("1"), netMid.Id);
            schematic.ConnectPinToNet(r2.GetPinByName("2"), netP.Id);
            schematic.ConnectPinToNet(c2.GetPinByName("1"), netP.Id);
            schematic.ConnectPinToNet(u1.GetPinByName("IN+"), netP.Id);
            schematic.ConnectPinToNet(u1.GetPinByName("OUT"), netOut.Id);
            schematic.ConnectPinToNet(c1.GetPinByName("2"), netOut.Id);
            schematic.ConnectPinToNet(u1.GetPinByName("IN-"), netOut.Id);
            schematic.ConnectPinToNet(vcc.GetPinByName("+"), netVcc.Id);
            schematic.ConnectPinToNet(u1.GetPinByName("V+"), netVcc.Id);
            schematic.ConnectPinToNet(vee.GetPinByName("-"), netVee.Id);
            schematic.ConnectPinToNet(u1.GetPinByName("V-"), netVee.Id);
            schematic.ConnectPinToNet(vIn.GetPinByName("-"), schematic.MasterGroundNet.Id);
            schematic.ConnectPinToNet(c2.GetPinByName("2"), schematic.MasterGroundNet.Id);
            schematic.ConnectPinToNet(vcc.GetPinByName("-"), schematic.MasterGroundNet.Id);
            schematic.ConnectPinToNet(vee.GetPinByName("+"), schematic.MasterGroundNet.Id);

            var exporter = new EdaSimulator.Engines.Simulation.SpiceNetlistExporter();
            var netlist = exporter.GenerateNetlist(schematic, ".tran 1u 1m");

            var ngSpicePath = EdaSimulator.Engines.Simulation.NgSpiceLocator.FindNgSpice();
            Assert.NotNull(ngSpicePath);

            var service = new EdaSimulator.Engines.Simulation.SpiceExecutionService(ngSpicePath);
            var result = await service.RunSimulationAsync(netlist, System.Threading.CancellationToken.None);

            Assert.True(result.Success, $"Simulation failed: {result.ErrorMessage}\nNetlist:\n{netlist}");
        }

        [Fact]
        public async System.Threading.Tasks.Task Simulation_StepSourceBlock_RunsSuccessfully()
        {
            var schematic = new EdaSimulator.Engines.Models.Schematic("Block Diagram Step Source Test");
            
            // XSO1 OUT BlockSourceStep params: offset=0 stepval=1 steptime=1m
            var stepSrc = new EdaSimulator.Engines.Models.Components.BlockSourceComponent("XSO1", "step 0 5 0.5u");
            var res = new EdaSimulator.Engines.Models.Components.Resistor("R1", "1k");
            
            schematic.AddComponent(stepSrc);
            schematic.AddComponent(res);
            
            var netOut = schematic.CreateNet("OUT_NET");
            
            schematic.ConnectPinToNet(stepSrc.GetPinByName("OUT"), netOut.Id);
            schematic.ConnectPinToNet(res.GetPinByName("1"), netOut.Id);
            schematic.ConnectPinToNet(res.GetPinByName("2"), schematic.MasterGroundNet.Id);
            
            var exporter = new EdaSimulator.Engines.Simulation.SpiceNetlistExporter();
            var netlist = exporter.GenerateNetlist(schematic, ".tran 10n 1u");

            var ngSpicePath = EdaSimulator.Engines.Simulation.NgSpiceLocator.FindNgSpice();
            Assert.NotNull(ngSpicePath);

            var service = new EdaSimulator.Engines.Simulation.SpiceExecutionService(ngSpicePath);
            var result = await service.RunSimulationAsync(netlist, System.Threading.CancellationToken.None);

            Assert.True(result.Success, $"Simulation failed: {result.ErrorMessage}\nNetlist:\n{netlist}");
        }

        [Fact]
        public async System.Threading.Tasks.Task Simulation_PoleZeroAnalysis_RunsSuccessfully()
        {
            var schematic = new EdaSimulator.Engines.Models.Schematic("RC Low-Pass Filter Pole-Zero Test");
            var r1 = new EdaSimulator.Engines.Models.Components.Resistor("R1", "1k");
            var c1 = new EdaSimulator.Engines.Models.Components.Capacitor("C1", "1u");

            schematic.AddComponent(r1);
            schematic.AddComponent(c1);

            var netIn = schematic.CreateNet("VIN_NODE");
            var netOut = schematic.CreateNet("OUT_NODE");

            schematic.ConnectPinToNet(r1.GetPinByName("1"), netIn.Id);
            schematic.ConnectPinToNet(r1.GetPinByName("2"), netOut.Id);
            schematic.ConnectPinToNet(c1.GetPinByName("1"), netOut.Id);
            schematic.ConnectPinToNet(c1.GetPinByName("2"), schematic.MasterGroundNet.Id);

            var exporter = new EdaSimulator.Engines.Simulation.SpiceNetlistExporter();
            var netlist = exporter.GenerateNetlist(schematic, ".pz VIN_NODE 0 OUT_NODE 0 vol pz");

            var ngSpicePath = EdaSimulator.Engines.Simulation.NgSpiceLocator.FindNgSpice();
            Assert.NotNull(ngSpicePath);

            var service = new EdaSimulator.Engines.Simulation.SpiceExecutionService(ngSpicePath);
            var result = await service.RunSimulationAsync(netlist, System.Threading.CancellationToken.None);

            Assert.True(result.Success, $"Simulation failed: {result.ErrorMessage}\nNetlist:\n{netlist}");
            Assert.Contains("pole", result.OutputLog.ToLower());
        }
    }
}

