using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.Indicators;
using Atas_Indicators.Modules;
using OFT.Rendering.Context;

namespace Atas_Indicators
{
    [DisplayName("VPO Test")]
    [Category("My Indicators")]
    public class VpoTest : Indicator
    {
        private readonly VpoRenderer _vpo = new();

        // ═══════════════════════════════════════════════════════════════════════
        //  GROUP: General
        // ═══════════════════════════════════════════════════════════════════════
        [Display(Name = "Profile Width %", GroupName = "General", Order = 0)]
        [Range(5, 80)]
        public int ProfileWidthPct { get; set; } = 20;

        [Display(Name = "Value Area %", GroupName = "General", Order = 1)]
        [Range(10, 99)]
        public int ValueAreaPct { get; set; } = 70;

        [Display(Name = "Show Bid/Ask Split", GroupName = "General", Order = 2)]
        public bool ShowBidAskSplit { get; set; } = true;

        [Display(Name = "Show Labels", GroupName = "General", Order = 3)]
        public bool ShowLabels { get; set; } = true;

        public VpoTest() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0) { _vpo.Reset(); return; }
            if (ChartInfo == null) return;

            var candle = GetCandle(bar);
            if (candle.Time.Date != GetCandle(bar - 1).Time.Date)
                _vpo.Reset();

            _vpo.ValueAreaPct = ValueAreaPct / 100m;
            _vpo.Feed(candle, ChartInfo.PriceChartContainer.Step);
        }

        protected override void OnRender(RenderContext ctx, DrawingLayouts layout)
        {
            if (layout != DrawingLayouts.Final || ChartInfo == null) return;

            var style = new VpoRenderSettings
            {
                ProfileWidthPct = ProfileWidthPct,
                AnchorRight = false, // profile flush-left, grows left → right
                ShowBidAskSplit = ShowBidAskSplit,
                ShowLabels = ShowLabels,
            };

            _vpo.Draw(ctx, ChartInfo, style);
        }
    }
}
