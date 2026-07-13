using System.ComponentModel;
using System.Drawing;
using OFT.Rendering.Tools;

namespace Atas_Indicators.Modules
{
    /// <summary>
    /// Expandable style object: Color / Width / Style.
    /// Used as the "Style ▶" expandable entry inside each level's own settings group.
    /// Show toggle is a separate flat bool in the same group — not inside this class.
    /// </summary>
    [Serializable]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class LineSettings
    {
        [DisplayName("Color")]
        public Color Color { get; set; } = Color.Black;

        [DisplayName("Width")]
        public int Width { get; set; } = 1;

        [DisplayName("Style")]
        public LineStyle Style { get; set; } = LineStyle.Solid;

        public LineSettings() { }
        public LineSettings(Color color, int width = 1, LineStyle style = LineStyle.Solid)
        { Color = color; Width = width; Style = style; }

        public RenderPen MakePen() => DrawHelper.MakePen(Color, Width, Style);

        public override string ToString() => $"{Color.Name}, {Width}px, {Style}";
    }
}
