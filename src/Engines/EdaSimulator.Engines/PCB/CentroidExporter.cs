using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace EdaSimulator.Engines.PCB
{
    /// <summary>
    /// Exports PCB footprint positions and rotation details to a standard Centroid (Pick-and-Place) CSV file.
    /// Used by automated PCB pick-and-place assembly machines.
    /// </summary>
    public class CentroidExporter
    {
        public static string GeneratePickAndPlace(PcbDocument doc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Designator,Value,Package,MidX,MidY,Rotation,Layer");

            foreach (var fp in doc.Footprints)
            {
                string designator = EscapeCsv(fp.Designator);
                string value = EscapeCsv(fp.Value);
                string package = EscapeCsv(fp.FootprintId);
                string layer = fp.Layer.ToString(); // e.g. "FCu" or "BCu"
                
                string midXStr = fp.X.ToString("F3", CultureInfo.InvariantCulture);
                string midYStr = fp.Y.ToString("F3", CultureInfo.InvariantCulture);
                string rotStr = fp.Rotation.ToString("F1", CultureInfo.InvariantCulture);

                sb.AppendLine($"{designator},{value},{package},{midXStr},{midYStr},{rotStr},{layer}");
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            if (str.Contains(",") || str.Contains("\"") || str.Contains("\n") || str.Contains("\r"))
            {
                return "\"" + str.Replace("\"", "\"\"") + "\"";
            }
            return str;
        }
    }
}
