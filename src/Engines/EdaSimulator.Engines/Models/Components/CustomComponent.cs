using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdaSimulator.Engines.Models.Components
{
    /// <summary>
    /// Represents a user-imported or custom database component that maps to a specific SPICE .model or .subckt.
    /// </summary>
    public class CustomComponent : Component
    {
        public Library.LibraryComponent LibraryModel { get; }

        public CustomComponent(string designator, string value, Library.LibraryComponent libraryModel)
            : base(designator, value)
        {
            LibraryModel = libraryModel ?? throw new ArgumentNullException(nameof(libraryModel));

            // Parse pin mappings or default pins
            string[] pinNames = !string.IsNullOrWhiteSpace(libraryModel.PinMappings)
                ? libraryModel.PinMappings.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                : Enumerable.Range(1, System.Math.Max(1, libraryModel.Pins)).Select(i => i.ToString()).ToArray();

            for (int i = 0; i < pinNames.Length; i++)
            {
                string pinName = pinNames[i].Trim();
                // If it contains a mapping format like "1:GND", extract the name
                if (pinName.Contains(':'))
                {
                    var parts = pinName.Split(':');
                    pinName = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
                }
                RegisterPin(pinName, i + 1);
            }
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            var pins = GetPinsInSpiceOrder().ToList();
            var netNames = pins.Select(p => schematic.GetNetNameForPin(p)).ToList();
            string joinedNets = string.Join(" ", netNames);

            // Determine if the model is a subcircuit (contains .subckt)
            string cleanedModel = LibraryModel.SpiceModel;
            bool isSubCkt = Regex.IsMatch(cleanedModel, @"\.subckt\s+", RegexOptions.IgnoreCase);

            if (isSubCkt)
            {
                // Subcircuit instantiation format: X<designator> <nodes> <subckt_name>
                string name = LibraryModel.Name;
                var match = Regex.Match(cleanedModel, @"\.subckt\s+(\S+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    name = match.Groups[1].Value;
                }

                string desig = Designator;
                if (!desig.StartsWith("X", StringComparison.OrdinalIgnoreCase))
                {
                    desig = "X" + desig;
                }
                return $"{desig} {joinedNets} {name}";
            }
            else
            {
                // Simple model card format (diodes, transistors, etc.)
                string prefix = "";
                string cat = LibraryModel.Category.ToLowerInvariant();
                if (cat.Contains("diode") || Designator.StartsWith("D", StringComparison.OrdinalIgnoreCase))
                    prefix = "D";
                else if (cat.Contains("npn") || cat.Contains("pnp") || Designator.StartsWith("Q", StringComparison.OrdinalIgnoreCase))
                    prefix = "Q";
                else if (cat.Contains("mosfet") || Designator.StartsWith("M", StringComparison.OrdinalIgnoreCase))
                    prefix = "M";
                else
                    prefix = "X"; // Default subcircuit

                string desig = Designator;
                if (!string.IsNullOrEmpty(prefix) && !desig.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    desig = prefix + desig;
                }

                return $"{desig} {joinedNets} {LibraryModel.Name}";
            }
        }
    }
}
