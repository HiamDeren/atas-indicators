using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.Indicators;
using Atas_Indicators.Modules;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace Atas_Indicators
{
    [DisplayName("[₢] Overnight Range")]
    [Category("My Indicators")]
    public class OvernightRange : Indicator
    {
        // Session 18:00–9:30 EST (overnight, wraps midnight), inclusive of the 9:30 bar
        private static readonly TimeSpan SessionOpen  = new(18, 0,  0);
        private static readonly TimeSpan SessionClose = new( 9, 30, 0);
        // drawEnd defaults to 16:15 EST (RTH close)

        private readonly SessionTracker _tracker = new(SessionOpen, SessionClose);
        private readonly VpoRenderer _vpo = new();
        private int _vpoBuiltStartBar = -1;
        private int _vpoBuiltEndBar = -1;
        private RenderFont? _font;

        // ═══════════════════════════════════════════════════════════════════════
        //  EXTENSION
        // ═══════════════════════════════════════════════════════════════════════

        [Display(Name = "Mode",             GroupName = "Extension", Order = 0)]
        public ExtendMode Extension { get; set; } = ExtendMode.ToTime;

        [Display(Name = "Draw Until (EST)", GroupName = "Extension", Order = 1)]
        public TimeSpan DrawUntil { get; set; } = new(16, 15, 0);  // RTH close

        // ═══════════════════════════════════════════════════════════════════════
        //  SETTINGS
        // ═══════════════════════════════════════════════════════════════════════

        // ── High / Low ────────────────────────────────────────────────────────
        [Display(Name = "Show",  GroupName = "High / Low",    Order = 10)]
        public bool ShowHighLow { get; set; } = true;

        [Display(Name = "Style", GroupName = "High / Low",    Order = 11)]
        public LineSettings HighLow { get; set; } = new(Color.FromArgb(220, 220, 225), 2);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Volume Profile (session window only, 18:00–9:30)
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
        [Range(5, 200)]
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

        public OvernightRange() : base(true)
        {
            DenyToChangePanel   = true;
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

            _tracker.DrawEnd = Extension == ExtendMode.ToTime
                ? DrawUntil
                : new TimeSpan(23, 59, 59);

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
            // waiting for the 9:30 close. Sweep tracking only makes sense once the
            // range is finalized, so both sides just extend to CurrentBar while live.
            var live = _tracker.Active;
            if (live != null && live.Range > 0)
            {
                int lxEnd = chart.GetXByBar(CurrentBar);
                if (lxEnd < ctx.ClipBounds.Left) return;

                _font ??= new RenderFont("Arial", 6);

                if (ShowHighLow)
                {
                    var pen = HighLow.MakePen();
                    DrawHelper.HLine(ctx, chart, _font!, live.High, pen, HighLow.Color,
                        chart.GetXByBar(live.HighBar), lxEnd, "ONH");
                    DrawHelper.HLine(ctx, chart, _font!, live.Low,  pen, HighLow.Color,
                        chart.GetXByBar(live.LowBar),  lxEnd, "ONL");
                }

                if (ShowVpo)
                    PaintVpo(ctx, chart, live, lxEnd);
                return;
            }

            var s = _tracker.Last;
            if (s == null || !s.IsReady) return;

            int x1     = chart.GetXByBar(s.EndBar);
            int xHigh2 = ComputeX2(ctx, chart, s, s.HighSweepBar);
            int xLow2  = ComputeX2(ctx, chart, s, s.LowSweepBar);

            if (x1 > ctx.ClipBounds.Right || Math.Max(xHigh2, xLow2) < ctx.ClipBounds.Left) return;

            _font ??= new RenderFont("Arial", 6);

            PaintBoundary(ctx, chart, s, x1);

            if (ShowHighLow)
            {
                var pen = HighLow.MakePen();
                DrawHelper.HLine(ctx, chart, _font!, s.High, pen, HighLow.Color,
                    chart.GetXByBar(s.HighBar), xHigh2, "ONH");
                DrawHelper.HLine(ctx, chart, _font!, s.Low,  pen, HighLow.Color,
                    chart.GetXByBar(s.LowBar),  xLow2,  "ONL");
            }

            if (ShowVpo)
            {
                int vpoLineX2 = ComputeX2(ctx, chart, s, -1);
                PaintVpo(ctx, chart, s, vpoLineX2);
            }
        }

        // Computes the right edge for one side (High or Low) of the range.
        // Regardless of Extension mode, once that side has been swept the line
        // freezes at the sweep bar instead of continuing to extend — the other
        // side (if unswept) keeps extending independently.
        private int ComputeX2(RenderContext ctx, IChart chart, SessionSnapshot s, int sweepBar)
        {
            int xRight = Extension switch
            {
                ExtendMode.ToAxis  => ctx.ClipBounds.Right,
                ExtendMode.ToSweep => sweepBar >= 0
                    ? chart.GetXByBar(sweepBar)
                    : chart.GetXByBar(CurrentBar),
                _ => s.DayEndBar >= 0
                    ? chart.GetXByBar(s.DayEndBar)
                    : chart.GetXByBar(CurrentBar),
            };

            if (Extension != ExtendMode.ToSweep && sweepBar >= 0)
                xRight = Math.Min(xRight, chart.GetXByBar(sweepBar));

            return Math.Min(xRight, ctx.ClipBounds.Right);
        }

        private void PaintBoundary(RenderContext ctx, IChart chart, SessionSnapshot s, int x1)
            => DrawHelper.VLine(ctx, chart,
                DrawHelper.MakePen(HighLow.Color, 1, LineStyle.Dotted), x1, s.High, s.Low);

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
