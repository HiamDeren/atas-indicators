using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.Indicators;
using Atas_Indicators.Modules;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace Atas_Indicators
{
    [DisplayName("Range69")]
    [Category("My Indicators")]
    public class Range69 : Indicator
    {
        // Session 6:00–8:59 EST  (EndBar = last bar with time < 9:00, tracked via _lastInBar)
        private static readonly TimeSpan SessionOpen  = new(6, 0, 0);
        private static readonly TimeSpan SessionClose = new(9, 0, 0);

        private readonly SessionTracker _tracker = new(SessionOpen, SessionClose);
        private RenderFont? _font;
        private string _fontFamily = "Arial";
        private int    _fontSize   = 7;

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: General
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Font Family", GroupName = "General", Order = 0)]
        public string FontFamily
        {
            get => _fontFamily;
            set { if (_fontFamily != value) { _fontFamily = value; _font = null; } }
        }

        [Display(Name = "Font Size", GroupName = "General", Order = 1)]
        [Range(6, 32)]
        public int FontSize
        {
            get => _fontSize;
            set { if (_fontSize != value) { _fontSize = value; _font = null; } }
        }

        [Display(Name = "Label Color", GroupName = "General", Order = 2)]
        public Color LabelColor { get; set; } = Color.FromArgb(63, 63, 63);

        [Display(Name = "Extension Mode", GroupName = "General", Order = 3)]
        public ExtendMode Extension { get; set; } = ExtendMode.ToTime;

        [Display(Name = "Draw Until (EST)", GroupName = "General", Order = 4)]
        public TimeSpan DrawUntil { get; set; } = new(10, 0, 0);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Core Levels
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show High / Low", GroupName = "Core Levels", Order = 10)]
        public bool ShowHighLow { get; set; } = true;
        [Display(Name = "High / Low Style", GroupName = "Core Levels", Order = 11)]
        public LineSettings HighLow { get; set; } = new(Color.FromArgb(40, 40, 40), 2);

        [Display(Name = "Show EQ", GroupName = "Core Levels", Order = 20)]
        public bool ShowEQ { get; set; } = true;
        [Display(Name = "EQ Style", GroupName = "Core Levels", Order = 21)]
        public LineSettings EQ { get; set; } = new(Color.FromArgb(192, 80, 77), 1);

        [Display(Name = "Show Open", GroupName = "Core Levels", Order = 25)]
        public bool ShowOpen { get; set; } = true;
        [Display(Name = "Open Style", GroupName = "Core Levels", Order = 26)]
        public LineSettings Open { get; set; } = new(Color.FromArgb(155, 187, 89), 1);

        [Display(Name = "Show 25% / 75%", GroupName = "Core Levels", Order = 30)]
        public bool ShowQuadrant { get; set; } = true;
        [Display(Name = "25% / 75% Style", GroupName = "Core Levels", Order = 31)]
        public LineSettings Quadrant { get; set; } = new(Color.FromArgb(140, 140, 140), 1, LineStyle.Dotted);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Standard Deviations  (±0.1/0.2/0.3, ±1, ±2)
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show Exhausted (±0.1–0.3)", GroupName = "Standard Deviations", Order = 50)]
        public bool ShowExhausted { get; set; } = true;
        [Display(Name = "Exhausted Style", GroupName = "Standard Deviations", Order = 51)]
        public LineSettings Exhausted { get; set; } = new(Color.FromArgb(180, 180, 180), 1, LineStyle.Dotted);

        [Display(Name = "Show ±1 SD", GroupName = "Standard Deviations", Order = 60)]
        public bool ShowSD1 { get; set; } = true;
        [Display(Name = "±1 SD Style", GroupName = "Standard Deviations", Order = 61)]
        public LineSettings SD1 { get; set; } = new(Color.FromArgb(110, 110, 110), 1, LineStyle.Dotted);

        [Display(Name = "Show ±2 SD", GroupName = "Standard Deviations", Order = 70)]
        public bool ShowSD2 { get; set; } = true;
        [Display(Name = "±2 SD Style", GroupName = "Standard Deviations", Order = 71)]
        public LineSettings SD2 { get; set; } = new(Color.FromArgb(110, 110, 110), 1, LineStyle.Dotted);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Fib Extensions  (±0.33/0.66, ±1.33/1.66, ±2.33/2.66)
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "±0.33/0.66 Lines", GroupName = "Fib Extensions", Order = 80)]
        public bool ShowFib033Lines { get; set; } = true;
        [Display(Name = "±0.33/0.66 Box", GroupName = "Fib Extensions", Order = 81)]
        public bool ShowFib033Box { get; set; } = true;
        [Display(Name = "±0.33/0.66 Style", GroupName = "Fib Extensions", Order = 82)]
        public FibBandSettings Fib033 { get; set; } = new(Color.FromArgb(57, 107, 167));

        [Display(Name = "±1.33/1.66 Lines", GroupName = "Fib Extensions", Order = 90)]
        public bool ShowFib133Lines { get; set; } = true;
        [Display(Name = "±1.33/1.66 Box", GroupName = "Fib Extensions", Order = 91)]
        public bool ShowFib133Box { get; set; } = true;
        [Display(Name = "±1.33/1.66 Style", GroupName = "Fib Extensions", Order = 92)]
        public FibBandSettings Fib133 { get; set; } = new(Color.FromArgb(57, 107, 167));

        [Display(Name = "±2.33/2.66 Lines", GroupName = "Fib Extensions", Order = 100)]
        public bool ShowFib233Lines { get; set; } = true;
        [Display(Name = "±2.33/2.66 Box", GroupName = "Fib Extensions", Order = 101)]
        public bool ShowFib233Box { get; set; } = true;
        [Display(Name = "±2.33/2.66 Style", GroupName = "Fib Extensions", Order = 102)]
        public FibBandSettings Fib233 { get; set; } = new(Color.FromArgb(57, 107, 167));

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Volume Profile
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show Histogram", GroupName = "Volume Profile", Order = 110)]
        public bool ShowHistogram { get; set; } = true;
        [Display(Name = "Direction", GroupName = "Volume Profile", Order = 111)]
        public HistogramAlign HistAlign { get; set; } = HistogramAlign.LeftToRight;
        [Display(Name = "Max Width %", GroupName = "Volume Profile", Order = 112)]
        [Range(1, 100)]
        public int HistWidthPct { get; set; } = 60;
        [Display(Name = "Outside VA Color", GroupName = "Volume Profile", Order = 113)]
        public Color HistBarColor { get; set; } = Color.FromArgb(90, 89, 89, 89);
        [Display(Name = "Value Area Color", GroupName = "Volume Profile", Order = 114)]
        public Color HistVAColor { get; set; } = Color.FromArgb(150, 57, 107, 167);
        [Display(Name = "POC Bar Color", GroupName = "Volume Profile", Order = 115)]
        public Color HistPOCColor { get; set; } = Color.FromArgb(230, 255, 200, 50);

        [Display(Name = "Value Area %", GroupName = "Volume Profile", Order = 116)]
        [Range(1, 100)]
        public int ValueAreaPct { get; set; } = 70;

        [Display(Name = "Show vPOC / VAH / VAL", GroupName = "Volume Profile", Order = 120)]
        public bool ShowVPOLines { get; set; } = true;
        [Display(Name = "vPOC Style", GroupName = "Volume Profile", Order = 121)]
        public LineSettings VpoPOC { get; set; } = new(Color.FromArgb(200, 140, 0), 2);
        [Display(Name = "VA Style", GroupName = "Volume Profile", Order = 122)]
        public LineSettings VpoVA { get; set; } = new(Color.FromArgb(57, 107, 167), 2);
        [Display(Name = "VA Fill Color", GroupName = "Volume Profile", Order = 123)]
        public Color VpoFill { get; set; } = Color.Transparent;

        // ═══════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════════════
        public Range69() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════════════════════════════════════════
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0) { _tracker.Reset(); return; }

            _tracker.DrawEnd = Extension == ExtendMode.ToTime
                ? DrawUntil
                : new TimeSpan(16, 15, 0); // market close — freezes DayEndBar at 16:14

            var c = GetCandle(bar);
            if (_tracker.Process(bar, c.Time, c.Open, c.High, c.Low) && _tracker.Last != null)
                ComputeVPO(_tracker.Last);
        }

        private void ComputeVPO(SessionSnapshot s)
        {
            var bars = Enumerable
                .Range(s.StartBar, s.EndBar - s.StartBar + 1)
                .Select(b => { var cb = GetCandle(b); return (cb.High, cb.Low, cb.Volume); });

            if (InstrumentInfo == null) return;
            s.SetVPO(VpoCalculator.CalculateFromBars(bars, InstrumentInfo.TickSize, ValueAreaPct / 100m));
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  VIEW
        // ═══════════════════════════════════════════════════════════════════════
        protected override void OnRender(RenderContext ctx, DrawingLayouts layout)
        {
            if (layout != DrawingLayouts.Final) return;

            var s = _tracker.Last;
            if (s == null || !s.IsReady) return;

            _font ??= new RenderFont(FontFamily, FontSize);

            // x0 = session start (6:00), x1 = session end (8:59), x2 = draw end
            int x0 = ChartInfo.GetXByBar(s.StartBar);
            int x1 = ChartInfo.GetXByBar(s.EndBar);
            int x2 = ComputeX2(ctx, s);

            if (x1 > ctx.ClipBounds.Right || x2 < ctx.ClipBounds.Left) return;

            // Histogram inside session (x0 → x1)
            if (ShowHistogram && s.VPO.IsReady)
                DrawHelper.VolumeHistogram(ctx, ChartInfo, s.VPO,
                    HistBarColor, HistVAColor, HistPOCColor, x0, x1, HistWidthPct, HistAlign);

            // Vertical boundary at session close
            DrawHelper.VLine(ctx, ChartInfo,
                DrawHelper.MakePen(HighLow.Color, 1, LineStyle.Dotted), x1, s.High, s.Low);

            // Horizontal levels extending x1 → x2
            PaintCore(ctx, s, x1, x2);
            PaintStdDev(ctx, s, x1, x2);
            PaintExtFib(ctx, s, x1, x2);

            // vPOC / VAH / VAL inside session x0 → x1
            if (ShowVPOLines)
                DrawHelper.Vpo(ctx, ChartInfo, _font!, s.VPO, VpoPOC, VpoVA, VpoFill, LabelColor, x0, x1);
        }

        private int ComputeX2(RenderContext ctx, SessionSnapshot s)
        {
            // ToTime:        freeze at DrawUntil (DayEndBar set by tracker when time reached)
            // ToCurrentBar:  always follow CurrentBar — naturally stops at last bar of data
            int xRight = Extension == ExtendMode.ToTime
                ? (s.DayEndBar >= 0 ? ChartInfo.GetXByBar(s.DayEndBar) : ChartInfo.GetXByBar(CurrentBar))
                : ChartInfo.GetXByBar(CurrentBar);
            return Math.Min(xRight, ctx.ClipBounds.Right);
        }

        private void PaintCore(RenderContext ctx, SessionSnapshot s, int x1, int x2)
        {
            if (ShowHighLow)
            {
                var pen = HighLow.MakePen();
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.High, pen, LabelColor,
                    ChartInfo.GetXByBar(s.HighBar), x2, "HIGH");
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.Low, pen, LabelColor,
                    ChartInfo.GetXByBar(s.LowBar), x2, "LOW");
            }

            if (ShowOpen)
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.Open,
                    Open.MakePen(), Open.Color, x1, x2, "OPEN");

            if (ShowEQ)
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.EQ,
                    EQ.MakePen(), EQ.Color, x1, x2, "EQ");

            if (ShowQuadrant)
            {
                var pen = Quadrant.MakePen();
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.L75, pen, LabelColor, x1, x2, "75%");
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.L25, pen, LabelColor, x1, x2, "25%");
            }
        }

        private void PaintStdDev(RenderContext ctx, SessionSnapshot s, int x1, int x2)
        {
            if (ShowExhausted)
            {
                var pen = Exhausted.MakePen();
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D01U, pen, LabelColor, x1, x2);
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D01L, pen, LabelColor, x1, x2);
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D02U, pen, LabelColor, x1, x2);
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D02L, pen, LabelColor, x1, x2);
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D03U, pen, LabelColor, x1, x2);
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D03L, pen, LabelColor, x1, x2);
            }
            if (ShowSD1)
            {
                var pen = SD1.MakePen();
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D10U, pen, LabelColor, x1, x2, "+1");
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D10L, pen, LabelColor, x1, x2, "-1");
            }
            if (ShowSD2)
            {
                var pen = SD2.MakePen();
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D20U, pen, LabelColor, x1, x2, "+2");
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D20L, pen, LabelColor, x1, x2, "-2");
            }
        }

        private void PaintExtFib(RenderContext ctx, SessionSnapshot s, int x1, int x2)
        {
            DrawHelper.FibBand(ctx, ChartInfo, _font!, s.F033U, s.F066U, Fib033, ShowFib033Lines, ShowFib033Box, "+0.33", "+0.66", x1, x2);
            DrawHelper.FibBand(ctx, ChartInfo, _font!, s.F033L, s.F066L, Fib033, ShowFib033Lines, ShowFib033Box, "-0.33", "-0.66", x1, x2);
            DrawHelper.FibBand(ctx, ChartInfo, _font!, s.F133U, s.F166U, Fib133, ShowFib133Lines, ShowFib133Box, "+1.33", "+1.66", x1, x2);
            DrawHelper.FibBand(ctx, ChartInfo, _font!, s.F133L, s.F166L, Fib133, ShowFib133Lines, ShowFib133Box, "-1.33", "-1.66", x1, x2);
            DrawHelper.FibBand(ctx, ChartInfo, _font!, s.F233U, s.F266U, Fib233, ShowFib233Lines, ShowFib233Box, "+2.33", "+2.66", x1, x2);
            DrawHelper.FibBand(ctx, ChartInfo, _font!, s.F233L, s.F266L, Fib233, ShowFib233Lines, ShowFib233Box, "-2.33", "-2.66", x1, x2);
        }

        protected override void OnDispose() => _font = null;
    }
}
