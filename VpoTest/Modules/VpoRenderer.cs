// ============================================================
// VpoRenderer.cs — Reusable Volume Profile Renderer cho ATAS
// Tách biệt hoàn toàn: Calculator + Renderer riêng
// Drop vào bất kỳ indicator nào, gọi 3 dòng là xong
//
// USAGE trong indicator của anh:
//   private readonly VpoRenderer _renderer = new();
//   // Trong OnCalculate: _renderer.Feed(candle, tickSize);
//   // Trong OnRender:    _renderer.Draw(context, ChartInfo, settings);
// ============================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace Atas_Indicators.Modules
{
    // ─────────────────────────────────────────────────────────────────────────
    // Settings bag — anh truyền vào khi gọi Draw(), không cần subclass
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class VpoRenderSettings
    {
        // Profile vị trí / kích thước
        public int    ProfileWidthPct  { get; set; } = 20;   // % chiều rộng visible area
        public bool   AnchorRight      { get; set; } = true;  // true = bám phải, false = bám trái

        // Màu sắc
        public Color  PocColor         { get; set; } = Color.FromArgb(255, 220, 50,  50);
        public Color  VaColor          { get; set; } = Color.FromArgb(180, 100, 180, 255);
        public Color  BodyColor        { get; set; } = Color.FromArgb(120, 80,  140, 200);
        public Color  PocLineColor     { get; set; } = Color.FromArgb(255, 220, 50,  50);
        public Color  VaLineColor      { get; set; } = Color.FromArgb(200, 255, 200, 60);

        // Hiển thị
        public bool   ShowPocLine      { get; set; } = true;
        public bool   ShowVaLines      { get; set; } = true;
        public bool   ShowLabels       { get; set; } = true;
        public bool   ShowBidAskSplit  { get; set; } = false; // Chia histogram bid/ask
        public Color  AskColor         { get; set; } = Color.FromArgb(160, 60,  180, 100);
        public Color  BidColor         { get; set; } = Color.FromArgb(160, 200, 60,  60);

        // POC line style
        public int    PocLineWidth     { get; set; } = 2;
        public int    VaLineWidth      { get; set; } = 1;

        // Label font size
        public int    FontSize         { get; set; } = 10;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VpoRenderer — tích lũy cluster data + vẽ histogram
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class VpoRenderer
    {
        // Cluster maps
        private readonly Dictionary<decimal, decimal> _volMap  = new(512);
        private readonly Dictionary<decimal, decimal> _askMap  = new(512);
        private readonly Dictionary<decimal, decimal> _bidMap  = new(512);

        // Kết quả tính VA (cập nhật sau mỗi Feed)
        public decimal POC          { get; private set; }
        public decimal VAH          { get; private set; }
        public decimal VAL          { get; private set; }
        public decimal TotalVolume  { get; private set; }
        public decimal MaxVolume    { get; private set; }
        public bool    IsReady      => _volMap.Count > 0 && POC > 0;

        // VA percent (default 70%)
        public decimal ValueAreaPct { get; set; } = 0.70m;

        // ── Reset session ──────────────────────────────────────
        public void Reset()
        {
            _volMap.Clear();
            _askMap.Clear();
            _bidMap.Clear();
            POC = VAH = VAL = TotalVolume = MaxVolume = 0;
        }

        // ── Feed một bar vào profile ───────────────────────────
        // Gọi trong OnCalculate cho mỗi bar trong session
        public void Feed(IndicatorCandle candle, decimal tickSize)
        {
            if (tickSize <= 0) return;

            // Dùng GetAllPriceLevels() — ATAS API chính thức, không iterate thủ công
            // Trả về IEnumerable<PriceVolumeInfo> cho tất cả price level có trade trong bar
            foreach (var pv in candle.GetAllPriceLevels())
            {
                if (pv == null || pv.Volume <= 0) continue;

                // Snap price về nearest tick (đề phòng floating point)
                var price = Math.Round(pv.Price / tickSize) * tickSize;

                _volMap.TryGetValue(price, out var ev);
                _volMap[price] = ev + pv.Volume;

                _askMap.TryGetValue(price, out var ea);
                _askMap[price] = ea + pv.Ask;

                _bidMap.TryGetValue(price, out var eb);
                _bidMap[price] = eb + pv.Bid;
            }

            RecalcKeyLevels();
        }

        // ── Tính POC / VAH / VAL (CME standard method) ────────
        private void RecalcKeyLevels()
        {
            if (_volMap.Count == 0) return;

            TotalVolume = 0m;
            decimal pocPrice = 0m, pocVol = 0m;

            foreach (var (price, vol) in _volMap)
            {
                TotalVolume += vol;
                if (vol > pocVol) { pocVol = vol; pocPrice = price; }
            }

            POC       = pocPrice;
            MaxVolume = pocVol;

            if (TotalVolume <= 0) return;

            // VA expansion từ POC ra 2 hướng
            var sorted = _volMap.Keys.Order().ToArray();
            int ui = Array.IndexOf(sorted, POC);
            int di = ui;
            decimal acc    = pocVol;
            decimal target = TotalVolume * ValueAreaPct;

            while (acc < target)
            {
                bool canU = ui + 1 < sorted.Length;
                bool canD = di - 1 >= 0;
                if (!canU && !canD) break;

                decimal uv = canU ? _volMap[sorted[ui + 1]] : 0m;
                decimal dv = canD ? _volMap[sorted[di - 1]] : 0m;

                if (uv >= dv && canU) { ui++; acc += uv; }
                else if (canD)        { di--; acc += dv; }
            }

            VAH = sorted[ui];
            VAL = sorted[di];
        }

        // ─────────────────────────────────────────────────────────────────────
        // Draw — gọi trong OnRender của indicator
        // chartInfo = this.ChartInfo (property của Indicator base class, kiểu IChart)
        // ─────────────────────────────────────────────────────────────────────
        public void Draw(
            RenderContext      context,
            IChart             chartInfo,
            VpoRenderSettings  s)
        {
            if (!IsReady) return;

            var container = chartInfo.PriceChartContainer;
            var tickSize = container.Step;
            if (tickSize <= 0) return;

            // ── Tính vùng profile trên chart ──────────────────
            int xLeft  = chartInfo.GetXByBar(container.FirstVisibleBarNumber);
            int xRight = chartInfo.GetXByBar(container.LastVisibleBarNumber)
                         + (int)container.BarsWidth;

            int profW = Math.Max(10, (int)((xRight - xLeft) * s.ProfileWidthPct / 100.0));
            int profR = s.AnchorRight ? xRight : xLeft + profW;
            int profL = profR - profW;

            if (MaxVolume <= 0) return;

            // ── Tính pixel height mỗi bar (1 tick) ────────────
            // Lấy 2 giá trị Y liên tiếp để đo height thực tế trên chart hiện tại
            int yTick0 = chartInfo.GetYByPrice(POC,           false);
            int yTick1 = chartInfo.GetYByPrice(POC + tickSize, false);
            int barH   = Math.Max(1, Math.Abs(yTick0 - yTick1));

            // ── Vẽ từng price level ────────────────────────────
            foreach (var (price, vol) in _volMap)
            {
                int yCenter = chartInfo.GetYByPrice(price, false);
                int y       = yCenter - barH / 2;
                int fullW   = (int)(profW * vol / MaxVolume);
                if (fullW <= 0) continue;

                bool isPoc = price == POC;
                bool isVa  = price >= VAL && price <= VAH;

                // AnchorRight = true  → bar flush at profR, grows LEFTWARD (right → left)
                // AnchorRight = false → bar flush at profL, grows RIGHTWARD (left → right)
                int barLeft = s.AnchorRight ? profR - fullW : profL;

                if (s.ShowBidAskSplit && !isPoc)
                {
                    // Bid luôn nằm bên trái, Ask luôn nằm bên phải trong mỗi bar —
                    // giữ nguyên quy ước bất kể hướng grow của toàn bộ profile
                    _askMap.TryGetValue(price, out var askVol);
                    _bidMap.TryGetValue(price, out var bidVol);
                    decimal totalAtLevel = askVol + bidVol;

                    if (totalAtLevel > 0)
                    {
                        int askW = (int)(fullW * askVol / totalAtLevel);
                        int bidW = fullW - askW;

                        context.FillRectangle(s.BidColor, new Rectangle(barLeft,        y, bidW, barH));
                        context.FillRectangle(s.AskColor, new Rectangle(barLeft + bidW, y, askW, barH));
                    }
                    else
                    {
                        context.FillRectangle(s.BodyColor, new Rectangle(barLeft, y, fullW, barH));
                    }
                }
                else
                {
                    // Single color mode
                    var clr = isPoc ? s.PocColor
                            : isVa  ? s.VaColor
                            :         s.BodyColor;
                    context.FillRectangle(clr, new Rectangle(barLeft, y, fullW, barH));
                }
            }

            // ── POC line (solid, ngang toàn bộ profile) ───────
            if (s.ShowPocLine && POC > 0)
            {
                int yPoc = chartInfo.GetYByPrice(POC, false);
                var pocPen = new RenderPen(s.PocLineColor, s.PocLineWidth);
                context.DrawLine(pocPen, profL, yPoc, profR, yPoc);

                if (s.ShowLabels)
                {
                    var font = new RenderFont("Arial", s.FontSize);
                    context.DrawString("POC", font, s.PocLineColor, profL - 38, yPoc - s.FontSize - 1);
                }
            }

            // ── VAH / VAL lines (dashed) ───────────────────────
            if (s.ShowVaLines && VAH > 0 && VAL > 0)
            {
                int yVah = chartInfo.GetYByPrice(VAH, false);
                int yVal = chartInfo.GetYByPrice(VAL, false);

                var vaPen = new RenderPen(s.VaLineColor, s.VaLineWidth);
                DrawDashedHLine(context, vaPen, profL, profR, yVah);
                DrawDashedHLine(context, vaPen, profL, profR, yVal);

                if (s.ShowLabels)
                {
                    var font = new RenderFont("Arial", s.FontSize);
                    context.DrawString($"VAH {VAH:F2}", font, s.VaLineColor, profL - 70, yVah - s.FontSize - 1);
                    context.DrawString($"VAL {VAL:F2}", font, s.VaLineColor, profL - 70, yVal + 2);
                }
            }
        }

        // ── Helper: vẽ dashed horizontal line ─────────────────
        // ATAS RenderContext không có DrawLine với DashStyle trực tiếp,
        // nên ta tự vẽ đoạn ngắt quãng
        private static void DrawDashedHLine(
            RenderContext context,
            RenderPen pen,
            int x1, int x2, int y,
            int dashLen = 6, int gapLen = 4)
        {
            int x = x1;
            bool drawing = true;
            while (x < x2)
            {
                int segEnd = Math.Min(x + (drawing ? dashLen : gapLen), x2);
                if (drawing)
                    context.DrawLine(pen, x, y, segEnd, y);
                x = segEnd;
                drawing = !drawing;
            }
        }
    }
}
