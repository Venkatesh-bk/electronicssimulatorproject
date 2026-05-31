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
    }
}

