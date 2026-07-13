using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.Indicators;
using Atas_Indicators.Modules;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace Atas_Indicators
{
    [DisplayName("[₢] TimeBase Range")]
    [Category("My Indicators")]
    public class TimeBaseRange : Indicator
    {
        // Window is fully user-configurable via RangeStart/RangeEnd below, kept in
        // sync with the tracker every OnCalculate call.
        private readonly SessionTracker _tracker;
        private readonly VpoRenderer _vpo = new();
        private int _vpoBuiltStartBar = -1;
        private int _vpoBuiltEndBar = -1;
        private RenderFont? _font;
        private string _fontFamily = "Arial";
        private int _fontSize = 6;

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: General
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Range Start (EST)", GroupName = "General", Order = 0)]
        public TimeSpan RangeStart { get; set; } = new(6, 0, 0);

        [Display(Name = "Range End (EST)", GroupName = "General", Order = 1)]
        public TimeSpan RangeEnd { get; set; } = new(9, 0, 0);

        [Display(Name = "Font Family", GroupName = "General", Order = 2)]
        public string FontFamily
        {
            get => _fontFamily;
            set { if (_fontFamily != value) { _fontFamily = value; _font = null; } }
        }

        [Display(Name = "Font Size", GroupName = "General", Order = 3)]
        [Range(6, 32)]
        public int FontSize
        {
            get => _fontSize;
            set { if (_fontSize != value) { _fontSize = value; _font = null; } }
        }

        [Display(Name = "Label Color", GroupName = "General", Order = 4)]
        public Color LabelColor { get; set; } = Color.FromArgb(210, 210, 210);

        [Display(Name = "Extension Mode", GroupName = "General", Order = 5)]
        public ExtendMode Extension { get; set; } = ExtendMode.ToTime;

        [Display(Name = "Draw Until (EST)", GroupName = "General", Order = 6)]
        public TimeSpan DrawUntil { get; set; } = new(10, 0, 0);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Core Levels
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show High / Low", GroupName = "Core Levels", Order = 10)]
        public bool ShowHighLow { get; set; } = true;
        [Display(Name = "High / Low Style", GroupName = "Core Levels", Order = 11)]
        public LineSettings HighLow { get; set; } = new(Color.FromArgb(220, 220, 225), 1);

        [Display(Name = "Show EQ", GroupName = "Core Levels", Order = 20)]
        public bool ShowEQ { get; set; } = true;
        [Display(Name = "EQ Style", GroupName = "Core Levels", Order = 21)]
        public LineSettings EQ { get; set; } = new(Color.FromArgb(192, 80, 77), 1, LineStyle.Dotted);

        [Display(Name = "Show Open", GroupName = "Core Levels", Order = 25)]
        public bool ShowOpen { get; set; } = true;
        [Display(Name = "Open Style", GroupName = "Core Levels", Order = 26)]
        public LineSettings Open { get; set; } = new(Color.FromArgb(155, 187, 89), 1, LineStyle.Dotted);

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
        public LineSettings SD1 { get; set; } = new(Color.FromArgb(150, 150, 160), 1, LineStyle.Dotted);

        [Display(Name = "Show ±2 SD", GroupName = "Standard Deviations", Order = 70)]
        public bool ShowSD2 { get; set; } = false;
        [Display(Name = "±2 SD Style", GroupName = "Standard Deviations", Order = 71)]
        public LineSettings SD2 { get; set; } = new(Color.FromArgb(110, 110, 130), 1, LineStyle.Dotted);

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
        public bool ShowFib233Lines { get; set; } = false;
        [Display(Name = "±2.33/2.66 Box", GroupName = "Fib Extensions", Order = 101)]
        public bool ShowFib233Box { get; set; } = false;
        [Display(Name = "±2.33/2.66 Style", GroupName = "Fib Extensions", Order = 102)]
        public FibBandSettings Fib233 { get; set; } = new(Color.FromArgb(57, 107, 167));

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Volume Profile (scoped to the RangeStart–RangeEnd window only)
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show Volume Profile", GroupName = "Volume Profile", Order = 100)]
        public bool ShowVpo { get; set; } = true;

        [Display(Name = "Value Area %", GroupName = "Volume Profile", Order = 101)]
        [Range(10, 99)]
        public int ValueAreaPct { get; set; } = 70;

        [Display(Name = "Profile Width %", GroupName = "Volume Profile", Order = 102)]
        [Range(5, 150)]
        public int ProfileWidthPct { get; set; } = 50;

        [Display(Name = "Show Delta", GroupName = "Volume Profile", Order = 103)]
        public bool ShowDelta { get; set; } = true;

        [Display(Name = "Delta Width %", GroupName = "Volume Profile", Order = 104)]
        [Range(5, 100)]
        public int DeltaWidthPct { get; set; } = 80;

        [Display(Name = "Show POC / VA Lines", GroupName = "Volume Profile", Order = 105)]
        public bool ShowVpoLines { get; set; } = true;

        [Display(Name = "Extend POC / VA Lines", GroupName = "Volume Profile", Order = 106)]
        public bool ExtendVpoLines { get; set; } = false;

        // Gray outside the value area, teal inside it — kept far apart in hue
        // so the two zones read as distinct at a glance (not just light/dark blue).
        [Display(Name = "Profile Color", GroupName = "Volume Profile", Order = 107)]
        public Color VpoBodyColor { get; set; } = Color.FromArgb(160, 130, 130, 130);

        [Display(Name = "Value Area Color", GroupName = "Volume Profile", Order = 108)]
        public Color VpoValueAreaColor { get; set; } = Color.FromArgb(190, 21, 137, 148);

        [Display(Name = "POC Line Color", GroupName = "Volume Profile", Order = 109)]
        public Color VpoPocLineColor { get; set; } = Color.FromArgb(255, 235, 164, 63);

        [Display(Name = "VA Line Color", GroupName = "Volume Profile", Order = 110)]
        public Color VpoValueAreaLineColor { get; set; } = Color.FromArgb(255, 79, 195, 247);

        [Display(Name = "Delta Positive Color", GroupName = "Volume Profile", Order = 111)]
        public Color DeltaPositiveColor { get; set; } = Color.FromArgb(210, 38, 194, 129);

        [Display(Name = "Delta Negative Color", GroupName = "Volume Profile", Order = 112)]
        public Color DeltaNegativeColor { get; set; } = Color.FromArgb(210, 231, 76, 60);

        // ═══════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════════════
        public TimeBaseRange() : base(true)
        {
            _tracker = new SessionTracker(RangeStart, RangeEnd);
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════════════════════════════════════════
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
            {
                _tracker.Reset();
                _vpo.Reset();
                _vpoBuiltStartBar = -1;
                _vpoBuiltEndBar = -1;
                return;
            }

            // Re-applying every bar is cheap and picks up live Settings edits —
            // Start/End setters auto-reset the tracker when the user changes them.
            _tracker.Start = RangeStart;
            _tracker.End = RangeEnd;
            _tracker.DrawEnd = Extension == ExtendMode.ToTime
                ? DrawUntil
                : new TimeSpan(16, 15, 0); // market close — freezes DayEndBar at 16:14

            var c = GetCandle(bar);
            _tracker.Process(bar, c.Time, c.Open, c.High, c.Low);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  VIEW
        // ═══════════════════════════════════════════════════════════════════════
        protected override void OnRender(RenderContext ctx, DrawingLayouts layout)
        {
            var chart = ChartInfo;
            if (layout != DrawingLayouts.Final || chart == null) return;

            // Live session in progress — draw it growing in real time instead of
            // waiting for RangeEnd. Falls back to the last closed session
            // (extended forward per Extension mode) once nothing is active.
            var live = _tracker.Active;
            if (live != null && live.Range > 0)
            {
                int lx1 = chart.GetXByBar(live.StartBar);
                int lx2 = chart.GetXByBar(CurrentBar);
                PaintSession(ctx, chart, live, lx1, lx2, drawBoundary: false);
                return;
            }

            var s = _tracker.Last;
            if (s == null || !s.IsReady) return;

            // x1 = first bar after RangeEnd, x2 = draw end
            int x1 = chart.GetXByBar(s.EndBar + 1);
            int x2 = ComputeX2(ctx, chart, s);
            PaintSession(ctx, chart, s, x1, x2, drawBoundary: true);
        }

        private void PaintSession(RenderContext ctx, IChart chart, SessionSnapshot s, int x1, int x2, bool drawBoundary)
        {
            if (x1 > ctx.ClipBounds.Right || x2 < ctx.ClipBounds.Left) return;

            _font ??= new RenderFont(FontFamily, FontSize);

            // Vertical boundary at session close (closed sessions only — a live
            // session has no close boundary yet)
            if (drawBoundary)
                DrawHelper.VLine(ctx, chart,
                    DrawHelper.MakePen(HighLow.Color, 1, LineStyle.Dotted), x1, s.High, s.Low);

            // Horizontal levels extending x1 → x2
            PaintCore(ctx, chart, s, x1, x2);
            PaintStdDev(ctx, chart, s, x1, x2);
            PaintExtFib(ctx, chart, s, x1, x2);

            if (ShowVpo)
                PaintVpo(ctx, chart, s, x2);
        }

        private int ComputeX2(RenderContext ctx, IChart chart, SessionSnapshot s)
        {
            // ToTime:        freeze at DrawUntil (DayEndBar set by tracker when time reached)
            // ToCurrentBar:  always follow CurrentBar — naturally stops at last bar of data
            int xRight = Extension == ExtendMode.ToTime
                ? (s.DayEndBar >= 0 ? chart.GetXByBar(s.DayEndBar) : chart.GetXByBar(CurrentBar))
                : chart.GetXByBar(CurrentBar);
            return Math.Min(xRight, ctx.ClipBounds.Right);
        }

        private void PaintCore(RenderContext ctx, IChart chart, SessionSnapshot s, int x1, int x2)
        {
            if (ShowHighLow)
            {
                var pen = HighLow.MakePen();
                DrawHelper.HLine(ctx, chart, _font!, s.High, pen, LabelColor,
                    chart.GetXByBar(s.HighBar), x2, "HIGH");
                DrawHelper.HLine(ctx, chart, _font!, s.Low, pen, LabelColor,
                    chart.GetXByBar(s.LowBar), x2, "LOW");
            }

            if (ShowOpen)
                DrawHelper.HLine(ctx, chart, _font!, s.Open,
                    Open.MakePen(), Open.Color, x1, x2, "OPEN");

            if (ShowEQ)
                DrawHelper.HLine(ctx, chart, _font!, s.EQ,
                    EQ.MakePen(), EQ.Color, x1, x2, "EQ");

            if (ShowQuadrant)
            {
                var pen = Quadrant.MakePen();
                DrawHelper.HLine(ctx, chart, _font!, s.L75, pen, LabelColor, x1, x2, "75%");
                DrawHelper.HLine(ctx, chart, _font!, s.L25, pen, LabelColor, x1, x2, "25%");
            }
        }

        private void PaintStdDev(RenderContext ctx, IChart chart, SessionSnapshot s, int x1, int x2)
        {
            if (ShowExhausted)
            {
                var pen = Exhausted.MakePen();
                DrawHelper.HLine(ctx, chart, _font!, s.D01U, pen, LabelColor, x1, x2);
                DrawHelper.HLine(ctx, chart, _font!, s.D01L, pen, LabelColor, x1, x2);
                DrawHelper.HLine(ctx, chart, _font!, s.D02U, pen, LabelColor, x1, x2);
                DrawHelper.HLine(ctx, chart, _font!, s.D02L, pen, LabelColor, x1, x2);
                DrawHelper.HLine(ctx, chart, _font!, s.D03U, pen, LabelColor, x1, x2);
                DrawHelper.HLine(ctx, chart, _font!, s.D03L, pen, LabelColor, x1, x2);
            }
            if (ShowSD1)
            {
                var pen = SD1.MakePen();
                DrawHelper.HLine(ctx, chart, _font!, s.D10U, pen, LabelColor, x1, x2, "+1");
                DrawHelper.HLine(ctx, chart, _font!, s.D10L, pen, LabelColor, x1, x2, "-1");
            }
            if (ShowSD2)
            {
                var pen = SD2.MakePen();
                DrawHelper.HLine(ctx, chart, _font!, s.D20U, pen, LabelColor, x1, x2, "+2");
                DrawHelper.HLine(ctx, chart, _font!, s.D20L, pen, LabelColor, x1, x2, "-2");
            }
        }

        private void PaintExtFib(RenderContext ctx, IChart chart, SessionSnapshot s, int x1, int x2)
        {
            DrawHelper.FibBand(ctx, chart, _font!, s.F033U, s.F066U, Fib033, ShowFib033Lines, ShowFib033Box, "+0.33", "+0.66", x1, x2);
            DrawHelper.FibBand(ctx, chart, _font!, s.F033L, s.F066L, Fib033, ShowFib033Lines, ShowFib033Box, "-0.33", "-0.66", x1, x2);
            DrawHelper.FibBand(ctx, chart, _font!, s.F133U, s.F166U, Fib133, ShowFib133Lines, ShowFib133Box, "+1.33", "+1.66", x1, x2);
            DrawHelper.FibBand(ctx, chart, _font!, s.F133L, s.F166L, Fib133, ShowFib133Lines, ShowFib133Box, "-1.33", "-1.66", x1, x2);
            DrawHelper.FibBand(ctx, chart, _font!, s.F233U, s.F266U, Fib233, ShowFib233Lines, ShowFib233Box, "+2.33", "+2.66", x1, x2);
            DrawHelper.FibBand(ctx, chart, _font!, s.F233L, s.F266L, Fib233, ShowFib233Lines, ShowFib233Box, "-2.33", "-2.66", x1, x2);
        }

        private void PaintVpo(RenderContext ctx, IChart chart, SessionSnapshot s, int extendX2)
        {
            // Rebuild once per distinct session (identified by its bar range) instead of
            // feeding incrementally from OnCalculate — OnCalculate can re-fire multiple
            // times for the same bar (live ticks), which would double-count volume there.
            if (s.StartBar != _vpoBuiltStartBar || s.EndBar != _vpoBuiltEndBar)
            {
                _vpo.Reset();
                _vpo.ValueAreaPct = ValueAreaPct / 100m;
                for (int i = s.StartBar; i <= s.EndBar; i++)
                    _vpo.Feed(GetCandle(i), chart.PriceChartContainer.Step);

                _vpoBuiltStartBar = s.StartBar;
                _vpoBuiltEndBar = s.EndBar;
            }

            int vpoX1 = chart.GetXByBar(s.StartBar);
            int vpoX2 = chart.GetXByBar(s.EndBar + 1);

            var style = new VpoRenderSettings
            {
                ProfileWidthPct = ProfileWidthPct,
                AnchorRight = false, // profile flush-left, grows left → right across the session box
                ShowDelta = ShowDelta,
                DeltaWidthPct = DeltaWidthPct,
                ShowPocLine = ShowVpoLines,
                ShowVaLines = ShowVpoLines,
                ShowLabels = ShowVpoLines,
                BodyColor = VpoBodyColor,
                VaColor = VpoValueAreaColor,
                PocColor = VpoValueAreaColor, // no separate POC-bar tint — matches uniform profile look
                PocLineColor = VpoPocLineColor,
                VaLineColor = VpoValueAreaLineColor,
                DeltaPositiveColor = DeltaPositiveColor,
                DeltaNegativeColor = DeltaNegativeColor,
            };

            _vpo.Draw(ctx, chart, style, vpoX1, vpoX2, ExtendVpoLines ? extendX2 : (int?)null);
        }

        protected override void OnDispose() => _font = null;
    }
}
