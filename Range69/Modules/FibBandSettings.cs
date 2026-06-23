using System.ComponentModel;
using System.Drawing;
using OFT.Rendering.Tools;

namespace Atas_Indicators.Modules
{
    /// <summary>
    /// Expandable style for Fib extension bands.
    /// ShowLines / ShowBox are flat bools in the indicator group — not inside this class.
    /// </summary>
    [Serializable]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class FibBandSettings
    {
        [DisplayName("Color")]
        public Color Color { get; set; } = Color.Green;

        [DisplayName("Box Color")]
        public Color BoxColor { get; set; } = Color.FromArgb(25, 0, 128, 0);

        [DisplayName("Width")]
        public int Width { get; set; } = 1;

        [DisplayName("Style")]
        public LineStyle Style { get; set; } = LineStyle.Dotted;

        public FibBandSettings() { }
        public FibBandSettings(Color color)
        {
            Color    = color;
            BoxColor = Color.FromArgb(25, color.R, color.G, color.B);
        }

        public RenderPen MakePen() => DrawHelper.MakePen(Color, Width, Style);

        public override string ToString() => $"{Color.Name}, {Style}";
    }
}
