using System;

namespace EdaSimulator.Engines.Models.Components
{
    /// <summary>
    /// Represents a graphical text note/annotation placed on the schematic canvas.
    /// Does not produce SPICE components (exported as a SPICE comment).
    /// </summary>
    public class AnnotationNote : Component
    {
        public AnnotationNote(string designator, string noteText) : base(designator, noteText)
        {
            Value = noteText;
        }

        public override string GenerateSpiceNetlistLine(Schematic schematic)
        {
            return $"* NOTE: {Value}";
        }
    }
}
