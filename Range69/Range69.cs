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
        // Session 6:00–8:59 EST  (CloseSession uses bar-1 so EndBar = 8:59)
        private static readonly TimeSpan SessionOpen = new(6, 0, 0);
        private static readonly TimeSpan SessionClose = new(9, 0, 0);

        private readonly SessionTracker _tracker = new(SessionOpen, SessionClose);
        private RenderFont? _font;

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Extension — how far lines draw past session close
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Mode", GroupName = "Extension", Order = 0)]
        public ExtendMode Extension { get; set; } = ExtendMode.ToTime;

        [Display(Name = "Draw Until (EST)", GroupName = "Extension", Order = 1)]
        public TimeSpan DrawUntil { get; set; } = new(10, 0, 0);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Core Levels
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show", GroupName = "High / Low", Order = 10)]
        public bool ShowHighLow { get; set; } = true;
        [Display(Name = "Style", GroupName = "High / Low", Order = 11)]
        public LineSettings HighLow { get; set; } = new(Color.White, 2);

        [Display(Name = "Show", GroupName = "EQ", Order = 20)]
        public bool ShowEQ { get; set; } = true;
        [Display(Name = "Style", GroupName = "EQ", Order = 21)]
        public LineSettings EQ { get; set; } = new(Color.DodgerBlue, 1);

        [Display(Name = "Show", GroupName = "Open", Order = 25)]
        public bool ShowOpen { get; set; } = false;
        [Display(Name = "Style", GroupName = "Open", Order = 26)]
        public LineSettings Open { get; set; } = new(Color.Gold, 1);

        [Display(Name = "Show", GroupName = "Quadrant 25/75%", Order = 30)]
        public bool ShowQuadrant { get; set; } = true;
        [Display(Name = "Style", GroupName = "Quadrant 25/75%", Order = 31)]
        public LineSettings Quadrant { get; set; } = new(Color.Gray, 1, LineStyle.Dotted);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Standard Deviations  (±0.1/0.2/0.3, ±1, ±2)
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show Exhausted", GroupName = "Standard Deviations", Order = 50)]
        public bool ShowExhausted { get; set; } = true;
        [Display(Name = "Exhausted Style", GroupName = "Standard Deviations", Order = 51)]
        public LineSettings Exhausted { get; set; } = new(Color.DimGray, 1, LineStyle.Dotted);

        [Display(Name = "Show ±1 SD", GroupName = "Standard Deviations", Order = 60)]
        public bool ShowSD1 { get; set; } = true;
        [Display(Name = "±1 SD Style", GroupName = "Standard Deviations", Order = 61)]
        public LineSettings SD1 { get; set; } = new(Color.Silver, 1, LineStyle.Dotted);

        [Display(Name = "Show ±2 SD", GroupName = "Standard Deviations", Order = 70)]
        public bool ShowSD2 { get; set; } = true;
        [Display(Name = "±2 SD Style", GroupName = "Standard Deviations", Order = 71)]
        public LineSettings SD2 { get; set; } = new(Color.Silver, 1, LineStyle.Dotted);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Extended SD (Fib bands ±0.33/0.66, ±1.33/1.66, ±2.33/2.66)
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show Lines", GroupName = "±0.33 / ±0.66", Order = 80)]
        public bool ShowFib033Lines { get; set; } = true;
        [Display(Name = "Show Box", GroupName = "±0.33 / ±0.66", Order = 81)]
        public bool ShowFib033Box { get; set; } = true;
        [Display(Name = "Style", GroupName = "±0.33 / ±0.66", Order = 82)]
        public FibBandSettings Fib033 { get; set; } = new(Color.MediumSeaGreen);

        [Display(Name = "Show Lines", GroupName = "±1.33 / ±1.66", Order = 90)]
        public bool ShowFib133Lines { get; set; } = true;
        [Display(Name = "Show Box", GroupName = "±1.33 / ±1.66", Order = 91)]
        public bool ShowFib133Box { get; set; } = true;
        [Display(Name = "Style", GroupName = "±1.33 / ±1.66", Order = 92)]
        public FibBandSettings Fib133 { get; set; } = new(Color.MediumSeaGreen);

        [Display(Name = "Show Lines", GroupName = "±2.33 / ±2.66", Order = 100)]
        public bool ShowFib233Lines { get; set; } = true;
        [Display(Name = "Show Box", GroupName = "±2.33 / ±2.66", Order = 101)]
        public bool ShowFib233Box { get; set; } = true;
        [Display(Name = "Style", GroupName = "±2.33 / ±2.66", Order = 102)]
        public FibBandSettings Fib233 { get; set; } = new(Color.MediumSeaGreen);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Volume Profile
        //  Histogram draws inside session bars (6:00-8:59).
        //  POC / VAH / VAL lines extend into the draw zone.
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show Histogram", GroupName = "Volume Profile", Order = 110)]
        public bool ShowHistogram { get; set; } = true;
        [Display(Name = "Outside VA Color", GroupName = "Volume Profile", Order = 111)]
        public Color HistBarColor { get; set; } = Color.FromArgb(70, 100, 149, 237);
        [Display(Name = "Value Area Color", GroupName = "Volume Profile", Order = 112)]
        public Color HistVAColor { get; set; } = Color.FromArgb(130, 100, 149, 237);
        [Display(Name = "POC Bar Color", GroupName = "Volume Profile", Order = 113)]
        public Color HistPOCColor { get; set; } = Color.FromArgb(220, 255, 165, 0);
        [Display(Name = "Max Width %",   GroupName = "Volume Profile", Order = 113)]
        [Range(1, 100)]
        public int HistWidthPct { get; set; } = 80;

        [Display(Name = "Value Area %",     GroupName = "Volume Profile", Order = 114)]
        [Range(1, 100)]
        public int ValueAreaPct { get; set; } = 70;

        [Display(Name = "Show POC/VAH/VAL", GroupName = "Volume Profile", Order = 115)]
        public bool ShowVPOLines { get; set; } = true;
        [Display(Name = "POC Style", GroupName = "Volume Profile", Order = 115)]
        public LineSettings VpoPOC { get; set; } = new(Color.Orange, 2);
        [Display(Name = "VA Style", GroupName = "Volume Profile", Order = 116)]
        public LineSettings VpoVA { get; set; } = new(Color.CornflowerBlue, 1, LineStyle.Dotted);
        [Display(Name = "VA Fill Color", GroupName = "Volume Profile", Order = 117)]
        public Color VpoFill { get; set; } = Color.FromArgb(20, 100, 149, 237);

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
                : new TimeSpan(23, 59, 59);

            var c = GetCandle(bar);
            if (_tracker.Process(bar, c.Time, c.Open, c.High, c.Low) && _tracker.Last != null)
                ComputeVPO(_tracker.Last);
        }

        private void ComputeVPO(SessionSnapshot s)
        {
            // VPO is calculated over the exact session bars (StartBar to EndBar = 6:00-8:59)
            var bars = Enumerable
                .Range(s.StartBar, s.EndBar - s.StartBar + 1)
                .Select(b => { var cb = GetCandle(b); return (cb.High, cb.Low, cb.Volume); });

            s.SetVPO(VpoCalculator.Calculate(bars, TickSize, ValueAreaPct / 100m));
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  VIEW
        // ═══════════════════════════════════════════════════════════════════════
        protected override void OnRender(RenderContext ctx, DrawingLayouts layout)
        {
            if (layout != DrawingLayouts.Final) return;

            var s = _tracker.Last;
            if (s == null || !s.IsReady) return;

            _font ??= new RenderFont("Arial", 8);

            // x0 = session start (6:00), x1 = session end (8:59), x2 = draw end
            int x0 = ChartInfo.GetXByBar(s.StartBar);
            int x1 = ChartInfo.GetXByBar(s.EndBar);
            int x2 = ComputeX2(ctx, s);

            if (x1 > ctx.ClipBounds.Right || x2 < ctx.ClipBounds.Left) return;

            // Histogram drawn inside the session bars (x0 → x1)
            if (ShowHistogram && s.VPO.IsReady)
                DrawHelper.VolumeHistogram(ctx, ChartInfo, s.VPO,
                    HistBarColor, HistVAColor, HistPOCColor, x0,
                    x0 + (int)((x1 - x0) * HistWidthPct / 100.0));

            // Vertical boundary at session close (8:59)
            DrawHelper.VLine(ctx, ChartInfo,
                DrawHelper.MakePen(HighLow.Color, 1, LineStyle.Dotted), x1, s.High, s.Low);

            // Horizontal levels extending from x1 → x2
            PaintCore(ctx, s, x1, x2);
            PaintStdDev(ctx, s, x1, x2);
            PaintExtFib(ctx, s, x1, x2);

            // VPO lines (POC / VAH / VAL) inside session bars x0 → x1
            if (ShowVPOLines)
                DrawHelper.Vpo(ctx, ChartInfo, _font!, s.VPO, VpoPOC, VpoVA, VpoFill, x0, x1);
        }

        private int ComputeX2(RenderContext ctx, SessionSnapshot s)
        {
            int xRight = Extension switch
            {
                ExtendMode.ToAxis => ctx.ClipBounds.Right,
                ExtendMode.ToSweep => s.SweepBar >= 0
                    ? ChartInfo.GetXByBar(s.SweepBar)
                    : ChartInfo.GetXByBar(CurrentBar),
                _ => s.DayEndBar >= 0
                    ? ChartInfo.GetXByBar(s.DayEndBar)
                    : ChartInfo.GetXByBar(CurrentBar),
            };
            return Math.Min(xRight, ctx.ClipBounds.Right);
        }

        private void PaintCore(RenderContext ctx, SessionSnapshot s, int x1, int x2)
        {
            if (ShowHighLow)
            {
                var pen = HighLow.MakePen();
                // H/L lines originate from the bar where the extreme was made
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.High, pen, HighLow.Color,
                    ChartInfo.GetXByBar(s.HighBar), x2, "HIGH");
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.Low, pen, HighLow.Color,
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
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.L75, pen, Quadrant.Color, x1, x2, "75%");
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.L25, pen, Quadrant.Color, x1, x2, "25%");
            }
        }

        private void PaintStdDev(RenderContext ctx, SessionSnapshot s, int x1, int x2)
        {
            if (ShowExhausted)
            {
                var pen = Exhausted.MakePen();
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D01U, pen, Exhausted.Color, x1, x2);
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D01L, pen, Exhausted.Color, x1, x2);
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D02U, pen, Exhausted.Color, x1, x2);
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D02L, pen, Exhausted.Color, x1, x2);
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D03U, pen, Exhausted.Color, x1, x2);
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D03L, pen, Exhausted.Color, x1, x2);
            }
            if (ShowSD1)
            {
                var pen = SD1.MakePen();
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D10U, pen, SD1.Color, x1, x2, "+1");
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D10L, pen, SD1.Color, x1, x2, "-1");
            }
            if (ShowSD2)
            {
                var pen = SD2.MakePen();
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D20U, pen, SD2.Color, x1, x2, "+2");
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.D20L, pen, SD2.Color, x1, x2, "-2");
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
