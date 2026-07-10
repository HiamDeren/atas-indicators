using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.Indicators;
using Atas_Indicators.Modules;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace Atas_Indicators
{
    [DisplayName("Initial Balance")]
    [Category("My Indicators")]
    public class InitialBalance : Indicator
    {
        // Session 9:30–10:30 EST (first hour of RTH — Initial Balance)
        private static readonly TimeSpan SessionOpen = new(9, 30, 0);
        private static readonly TimeSpan SessionClose = new(10, 30, 0);

        private readonly SessionTracker _tracker = new(SessionOpen, SessionClose);
        private readonly VpoRenderer _vpo = new();
        private int _vpoBuiltStartBar = -1;
        private int _vpoBuiltEndBar = -1;

        private RenderFont? _font;
        private string _fontFamily = "Arial";
        private int _fontSize = 7;

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
        public Color LabelColor { get; set; } = Color.FromArgb(210, 210, 210);

        [Display(Name = "Extension Mode", GroupName = "General", Order = 3)]
        public ExtendMode Extension { get; set; } = ExtendMode.ToTime;

        // Default = end of AM session (NY intraday convention). Adjust freely per-user.
        [Display(Name = "Draw Until (EST)", GroupName = "General", Order = 4)]
        public TimeSpan DrawUntil { get; set; } = new(12, 0, 0);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: IB Range
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show High / Low", GroupName = "IB Range", Order = 10)]
        public bool ShowHighLow { get; set; } = true;
        [Display(Name = "High / Low Style", GroupName = "IB Range", Order = 11)]
        public LineSettings HighLow { get; set; } = new(Color.FromArgb(220, 220, 225), 1);

        [Display(Name = "Show EQ", GroupName = "IB Range", Order = 20)]
        public bool ShowEQ { get; set; } = true;
        [Display(Name = "EQ Style", GroupName = "IB Range", Order = 21)]
        public LineSettings EQ { get; set; } = new(Color.FromArgb(233, 105, 105), 1, LineStyle.Dotted);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Standard Deviations  (±0.5, ±1, ±1.5, ±2)
        //  Lighter = closer to the range, dimmer = further out — but still
        //  visible against a near-black chart background.
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show ±0.5 SD", GroupName = "Standard Deviations", Order = 50)]
        public bool ShowSD05 { get; set; } = true;
        [Display(Name = "±0.5 SD Style", GroupName = "Standard Deviations", Order = 51)]
        public LineSettings SD05 { get; set; } = new(Color.FromArgb(150, 150, 160), 1, LineStyle.Dotted);

        [Display(Name = "Show ±1 SD", GroupName = "Standard Deviations", Order = 60)]
        public bool ShowSD10 { get; set; } = true;
        [Display(Name = "±1 SD Style", GroupName = "Standard Deviations", Order = 61)]
        public LineSettings SD10 { get; set; } = new(Color.FromArgb(130, 130, 145), 1, LineStyle.Dotted);

        [Display(Name = "Show ±1.5 SD", GroupName = "Standard Deviations", Order = 70)]
        public bool ShowSD15 { get; set; } = false;
        [Display(Name = "±1.5 SD Style", GroupName = "Standard Deviations", Order = 71)]
        public LineSettings SD15 { get; set; } = new(Color.FromArgb(110, 110, 130), 1, LineStyle.Dotted);

        [Display(Name = "Show ±2 SD", GroupName = "Standard Deviations", Order = 80)]
        public bool ShowSD20 { get; set; } = false;
        [Display(Name = "±2 SD Style", GroupName = "Standard Deviations", Order = 81)]
        public LineSettings SD20 { get; set; } = new(Color.FromArgb(95, 95, 115), 1, LineStyle.Dotted);

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: Volume Profile (IB window only, 9:30–10:30)
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Show Volume Profile", GroupName = "Volume Profile", Order = 100)]
        public bool ShowVpo { get; set; } = true;

        [Display(Name = "Value Area %", GroupName = "Volume Profile", Order = 101)]
        [Range(10, 99)]
        public int ValueAreaPct { get; set; } = 70;

        [Display(Name = "Profile Width %", GroupName = "Volume Profile", Order = 102)]
        [Range(5, 150)]
        public int ProfileWidthPct { get; set; } = 100;

        [Display(Name = "Show Delta", GroupName = "Volume Profile", Order = 103)]
        public bool ShowDelta { get; set; } = true;

        [Display(Name = "Delta Width %", GroupName = "Volume Profile", Order = 104)]
        [Range(5, 100)]
        public int DeltaWidthPct { get; set; } = 45;

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
        public InitialBalance() : base(true)
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

            var s = _tracker.Last;
            if (s == null || !s.IsReady) return;

            _font ??= new RenderFont(FontFamily, FontSize);

            // x1 = first bar after IB closes (10:30), x2 = draw end
            int x1 = chart.GetXByBar(s.EndBar + 1);
            int x2 = ComputeX2(ctx, chart, s);

            if (x1 > ctx.ClipBounds.Right || x2 < ctx.ClipBounds.Left) return;

            // Vertical boundary at IB close
            DrawHelper.VLine(ctx, chart,
                DrawHelper.MakePen(HighLow.Color, 1, LineStyle.Dotted), x1, s.High, s.Low);

            PaintRange(ctx, chart, s, x1, x2);
            PaintStdDev(ctx, chart, s, x1, x2);

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

        private void PaintRange(RenderContext ctx, IChart chart, SessionSnapshot s, int x1, int x2)
        {
            if (ShowHighLow)
            {
                var pen = HighLow.MakePen();
                DrawHelper.HLine(ctx, chart, _font!, s.High, pen, LabelColor,
                    chart.GetXByBar(s.HighBar), x2, "IB HIGH");
                DrawHelper.HLine(ctx, chart, _font!, s.Low, pen, LabelColor,
                    chart.GetXByBar(s.LowBar), x2, "IB LOW");
            }

            if (ShowEQ)
                DrawHelper.HLine(ctx, chart, _font!, s.EQ,
                    EQ.MakePen(), EQ.Color, x1, x2, "EQ");
        }

        private void PaintStdDev(RenderContext ctx, IChart chart, SessionSnapshot s, int x1, int x2)
        {
            if (ShowSD05)
            {
                var pen = SD05.MakePen();
                DrawHelper.HLine(ctx, chart, _font!, s.D05U, pen, LabelColor, x1, x2, "+0.5");
                DrawHelper.HLine(ctx, chart, _font!, s.D05L, pen, LabelColor, x1, x2, "-0.5");
            }
            if (ShowSD10)
            {
                var pen = SD10.MakePen();
                DrawHelper.HLine(ctx, chart, _font!, s.D10U, pen, LabelColor, x1, x2, "+1");
                DrawHelper.HLine(ctx, chart, _font!, s.D10L, pen, LabelColor, x1, x2, "-1");
            }
            if (ShowSD15)
            {
                var pen = SD15.MakePen();
                DrawHelper.HLine(ctx, chart, _font!, s.D15U, pen, LabelColor, x1, x2, "+1.5");
                DrawHelper.HLine(ctx, chart, _font!, s.D15L, pen, LabelColor, x1, x2, "-1.5");
            }
            if (ShowSD20)
            {
                var pen = SD20.MakePen();
                DrawHelper.HLine(ctx, chart, _font!, s.D20U, pen, LabelColor, x1, x2, "+2");
                DrawHelper.HLine(ctx, chart, _font!, s.D20L, pen, LabelColor, x1, x2, "-2");
            }
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
                AnchorRight = false, // profile flush-left, grows left → right across the IB box
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
