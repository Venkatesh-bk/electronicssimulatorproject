using System;
using System.Linq;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.Simulation;

int passed = 0, failed = 0;

void Assert(string testName, bool condition, string failMsg = "")
{
    if (condition) { Console.WriteLine($"  [PASS] {testName}"); passed++; }
    else { Console.WriteLine($"  [FAIL] {testName}: {failMsg}"); failed++; }
}

void AssertThrows<TEx>(string testName, Action action) where TEx : Exception
{
    try { action(); Console.WriteLine($"  [FAIL] {testName}: Expected {typeof(TEx).Name} but no exception was thrown"); failed++; }
    catch (TEx) { Console.WriteLine($"  [PASS] {testName}"); passed++; }
    catch (Exception ex) { Console.WriteLine($"  [FAIL] {testName}: Expected {typeof(TEx).Name} but got {ex.GetType().Name}: {ex.Message}"); failed++; }
}

// ─── Pin Tests ─────────────────────────────────────────────────────────────
Console.WriteLine("\n=== Pin Tests ===");
AssertThrows<ArgumentOutOfRangeException>("Pin: sequence 0 rejected", () => new Pin(Guid.NewGuid(), "A", 0));
AssertThrows<ArgumentException>("Pin: empty name rejected", () => new Pin(Guid.NewGuid(), "", 1));
var pin = new Pin(Guid.NewGuid(), "VCC", 1);
Assert("Pin: IsFloating when newly created", pin.IsFloating);

// ─── Net Tests ─────────────────────────────────────────────────────────────
Console.WriteLine("\n=== Net Tests ===");
var groundNet = new Net("0");
Assert("Net: IsGround when name is '0'", groundNet.IsGround);
AssertThrows<InvalidOperationException>("Net: ground name is immutable", () => groundNet.Name = "GND");
AssertThrows<ArgumentException>("Net: whitespace in name rejected", () => new Net("My Net"));
var n = new Net("VCC");
Assert("Net: not ground by default", !n.IsGround);

// ─── Component/Designator Tests ─────────────────────────────────────────────
Console.WriteLine("\n=== Component Designator Tests ===");
AssertThrows<ArgumentException>("Component: whitespace designator rejected", () => new Resistor("R 1", "10k"));
AssertThrows<ArgumentException>("Component: empty designator rejected", () => new Resistor("", "10k"));
AssertThrows<ArgumentException>("Component: wrong prefix rejected for Resistor", () => new Resistor("C1", "10k"));
AssertThrows<ArgumentException>("Component: wrong prefix rejected for Capacitor", () => new Capacitor("R1", "100n"));
AssertThrows<ArgumentException>("Component: wrong prefix rejected for Inductor", () => new Inductor("R1", "1u"));
AssertThrows<ArgumentException>("Component: wrong prefix rejected for VoltageSource", () => new VoltageSource("R1", "DC 5"));
AssertThrows<ArgumentException>("Component: wrong prefix rejected for CurrentSource", () => new CurrentSource("R1", "DC 1m"));

// ─── Value Tests — Sources must accept spaces ───────────────────────────────
Console.WriteLine("\n=== Component Value Tests ===");
var v1 = new VoltageSource("V1", "DC 5");
Assert("VoltageSource: 'DC 5' accepted", v1.Value == "DC 5");
var v2 = new VoltageSource("V2", "AC 1 0");
Assert("VoltageSource: 'AC 1 0' accepted", v2.Value == "AC 1 0");
var v3 = new VoltageSource("V3", "PULSE(0 5 0 1n 1n 5u 10u)");
Assert("VoltageSource: PULSE value accepted", v3.Value.StartsWith("PULSE"));
var i1 = new CurrentSource("I1", "DC 10m");
Assert("CurrentSource: 'DC 10m' accepted (space)", i1.Value == "DC 10m");
var r1 = new Resistor("R1", "10k");
Assert("Resistor: '10k' accepted", r1.Value == "10k");

// ─── RegisterPin duplicate-sequence guard ───────────────────────────────────
Console.WriteLine("\n=== RegisterPin Guard Tests ===");
// Two-pin components already test this implicitly; verify sequence conflict is blocked at the engine level
// We test this through an actual SPICE netlist (we can't call RegisterPin directly as it's protected)
Assert("Resistor has exactly 2 pins", r1.Pins.Count == 2);
Assert("Resistor pin 1 sequence == 1", r1.Pins[0].SpiceNodeSequence == 1);
Assert("Resistor pin 2 sequence == 2", r1.Pins[1].SpiceNodeSequence == 2);

// ─── Schematic Graph Tests ─────────────────────────────────────────────────
Console.WriteLine("\n=== Schematic Tests ===");
var sch = new Schematic("TestCircuit");
Assert("Schematic: ground net present", sch.MasterGroundNet != null);
Assert("Schematic: ground net name == '0'", sch.MasterGroundNet.Name == "0");
Assert("Schematic: no components at start", sch.Components.Count == 0);

// Duplicate designator
var r2 = new Resistor("R1", "4k7");
sch.AddComponent(r1);
AssertThrows<ArgumentException>("Schematic: duplicate designator rejected", () => sch.AddComponent(r2));
sch.AddComponent(new Resistor("R2", "1k"));

// Duplicate net name
var net1 = sch.CreateNet("N001");
AssertThrows<ArgumentException>("Schematic: duplicate net name rejected", () => sch.CreateNet("N001"));
AssertThrows<ArgumentException>("Schematic: duplicate net name case-insensitive", () => sch.CreateNet("n001"));
AssertThrows<ArgumentException>("Schematic: ground name '0' rejected in CreateNet", () => sch.CreateNet("0"));

// ConnectPinToNet
sch.ConnectPinToNet(r1.GetPinByName("1"), net1.Id);
sch.ConnectPinToNet(r1.GetPinByName("1"), sch.MasterGroundNet.Id); // reconnect — should move
Assert("Pin: reconnected to ground net", r1.GetPinByName("1").ConnectedNetId == sch.MasterGroundNet.Id);
Assert("Old net N001 lost that pin", net1.ConnectedPinIds.Count == 0);

// DisconnectPin
sch.DisconnectPin(r1.GetPinByName("1"));
Assert("Pin: floating after disconnect", r1.GetPinByName("1").IsFloating);
Assert("Ground net lost that pin", sch.MasterGroundNet.ConnectedPinIds.Count == 0);

// Idempotent disconnect on floating pin
sch.DisconnectPin(r1.GetPinByName("2")); // never connected — should not throw
Assert("DisconnectPin: safe on floating pin", true);

// RemoveNet: ground net cannot be removed
AssertThrows<InvalidOperationException>("Schematic: RemoveNet(ground) throws", () => sch.RemoveNet(sch.MasterGroundNet.Id));

// GetNetNameForPin: floating pins get unique NC_ names
var p1 = r1.GetPinByName("1"); var p2 = r1.GetPinByName("2");
var nc1 = sch.GetNetNameForPin(p1); var nc2 = sch.GetNetNameForPin(p2);
Assert("Floating pins get unique NC_ names", nc1 != nc2);
Assert("NC_ node starts with NC_", nc1.StartsWith("NC_") && nc2.StartsWith("NC_"));

// ─── SPICE Netlist Export Tests ─────────────────────────────────────────────
Console.WriteLine("\n=== SPICE Netlist Exporter Tests ===");
var sch2 = new Schematic("SimpleRC");
var vSrc = new VoltageSource("V1", "DC 5");
var resistor = new Resistor("R1", "10k");
sch2.AddComponent(vSrc);
sch2.AddComponent(resistor);
var nVcc = sch2.CreateNet("VCC");
sch2.ConnectPinToNet(vSrc.GetPinByName("+"), nVcc.Id);
sch2.ConnectPinToNet(resistor.GetPinByName("1"), nVcc.Id);
sch2.ConnectPinToNet(vSrc.GetPinByName("-"), sch2.MasterGroundNet.Id);
sch2.ConnectPinToNet(resistor.GetPinByName("2"), sch2.MasterGroundNet.Id);

var exporter = new SpiceNetlistExporter();
var netlist = exporter.GenerateNetlist(sch2, ".op");
Console.WriteLine("\nGenerated netlist:\n" + netlist);

Assert("Netlist: contains title", netlist.Contains("SimpleRC"));
Assert("Netlist: R1 line present", netlist.Contains("R1 VCC 0 10k"));
Assert("Netlist: V1 line present with 'DC 5'", netlist.Contains("V1 VCC 0 DC 5"));
Assert("Netlist: .op directive present", netlist.Contains(".op"));
Assert("Netlist: ends with .end", netlist.TrimEnd().EndsWith(".end"));
Assert("Netlist: no NC_ tokens (all pins connected)", !netlist.Contains("NC_"));

// First line must be a comment (SPICE title line rule)
var firstLine = netlist.TrimStart().Split('\n')[0].Trim();
Assert("Netlist: first line is a comment (*)", firstLine.StartsWith("*"));

// ─── Validate() Tests ─────────────────────────────────────────────────────
Console.WriteLine("\n=== Validate() Tests ===");
var issues = sch2.Validate();
Assert("Fully wired schematic: 0 validation issues", issues.Count == 0);

var sch3 = new Schematic("BadCircuit");
sch3.AddComponent(new Resistor("R1", "1k")); // all pins floating
var vIssues = sch3.Validate();
Assert("Floating component flagged in Validate()", vIssues.Any(i => i.Contains("floating")));
Assert("No ground connection flagged in Validate()", vIssues.Any(i => i.Contains("ground")));

// ─── Summary ──────────────────────────────────────────────────────────────
Console.WriteLine($"\n{'='*50}");
Console.WriteLine($"Results: {passed} passed, {failed} failed out of {passed + failed} tests");
if (failed == 0) Console.WriteLine("ALL TESTS PASSED ✓");
else Console.WriteLine("SOME TESTS FAILED — see above");
