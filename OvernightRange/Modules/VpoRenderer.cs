// ============================================================
// VpoRenderer.cs — Reusable Volume Profile Renderer cho ATAS
// Vẽ VPO cho MỘT KHOẢNG THỜI GIAN bất kỳ (session-scoped), không
// còn phụ thuộc vào visible chart area — caller tự quyết định
// vùng pixel [x1, x2] cần vẽ (vd: đúng khung giờ session).
//
// USAGE trong indicator của anh:
//   private readonly VpoRenderer _vpo = new();
//   // Trong OnCalculate: gọi Reset() khi session mới bắt đầu,
//   //                    rồi Feed(candle, tickSize) cho mỗi bar TRONG khung giờ đó.
//   // Trong OnRender:    _vpo.Draw(ctx, ChartInfo, style, x1, x2);
//   //                    x1 = GetXByBar(session.StartBar)
//   //                    x2 = GetXByBar(session.EndBar + 1)
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
        // Profile độ rộng (% của khoảng [x1, x2] được truyền vào Draw)
        public int    ProfileWidthPct  { get; set; } = 100;
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

        // Delta lane — dải riêng mọc ngược hướng VPO, độ rộng scale theo % của profW
        public bool   ShowDelta            { get; set; } = false;
        public int    DeltaWidthPct        { get; set; } = 40;
        public Color  DeltaPositiveColor   { get; set; } = Color.FromArgb(210, 38,  194, 129);
        public Color  DeltaNegativeColor   { get; set; } = Color.FromArgb(210, 231, 76,  60);

        // POC line style
        public int    PocLineWidth     { get; set; } = 2;
        public int    VaLineWidth      { get; set; } = 1;

        // Label font size
        public int    FontSize         { get; set; } = 8;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VpoRenderer — tích lũy cluster data + vẽ histogram cho 1 session
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
        public decimal MaxAbsDelta  { get; private set; }
        public bool    IsReady      => _volMap.Count > 0 && POC > 0;

        // VA percent (default 70%)
        public decimal ValueAreaPct { get; set; } = 0.70m;

        // ── Reset session ──────────────────────────────────────
        public void Reset()
        {
            _volMap.Clear();
            _askMap.Clear();
            _bidMap.Clear();
            POC = VAH = VAL = TotalVolume = MaxVolume = MaxAbsDelta = 0;
        }

        // ── Feed một bar vào profile ───────────────────────────
        // Gọi trong OnCalculate cho mỗi bar nằm TRONG khung giờ cần vẽ VPO
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
            decimal pocPrice = 0m, pocVol = 0m, maxAbsDelta = 0m;

            foreach (var (price, vol) in _volMap)
            {
                TotalVolume += vol;
                if (vol > pocVol) { pocVol = vol; pocPrice = price; }

                _askMap.TryGetValue(price, out var askVol);
                _bidMap.TryGetValue(price, out var bidVol);
                var absDelta = Math.Abs(askVol - bidVol);
                if (absDelta > maxAbsDelta) maxAbsDelta = absDelta;
            }

            POC         = pocPrice;
            MaxVolume   = pocVol;
            MaxAbsDelta = maxAbsDelta;

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
        //   chartInfo   = ChartInfo (property của Indicator base class, kiểu IChart)
        //   x1, x2      = vùng pixel cần vẽ histogram (vd: GetXByBar(StartBar)..GetXByBar(EndBar+1))
        //   lineEndX    = POC/VAH/VAL line kéo dài tới đâu (mặc định = x2, tức chỉ nằm trong khung giờ)
        // ─────────────────────────────────────────────────────────────────────
        public void Draw(
            RenderContext      context,
            IChart             chartInfo,
            VpoRenderSettings  s,
            int                x1,
            int                x2,
            int?               lineEndX = null)
        {
            if (!IsReady || MaxVolume <= 0) return;
            if (x2 <= x1) return;

            var tickSize = chartInfo.PriceChartContainer.Step;
            if (tickSize <= 0) return;

            // ── Tính vùng histogram bên trong [x1, x2] ─────────
            int profW = Math.Max(4, (int)((x2 - x1) * s.ProfileWidthPct / 100.0));
            int profR = s.AnchorRight ? x2 : x1 + profW;
            int profL = profR - profW;

            // Delta lane KHÔNG scale theo profW (độ rộng box session) — nếu scale
            // theo box thì session ngắn (IB, 1 tiếng) luôn ra delta nhỏ hơn hẳn session
            // dài (Range69 3 tiếng, Overnight 15.5 tiếng) dù cùng DeltaWidthPct%.
            // Thay vào đó DeltaWidthPct% áp trực tiếp lên 1 trần cố định theo % độ rộng
            // khung nhìn chart — cùng DeltaWidthPct thì mọi indicator ra cùng 1 độ rộng
            // tuyệt đối, bất kể session dài ngắn khác nhau.
            int deltaCap = Math.Max(20, (int)(context.ClipBounds.Width * 0.08));
            int deltaMaxW = Math.Max(2, (int)(deltaCap * s.DeltaWidthPct / 100.0));

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

                var clr = isPoc ? s.PocColor
                        : isVa  ? s.VaColor
                        :         s.BodyColor;
                context.FillRectangle(clr, new Rectangle(barLeft, y, fullW, barH));

                // Delta lane — mirrored off the SAME anchor edge, growing the
                // OPPOSITE direction from the volume profile (outside the box)
                if (s.ShowDelta && MaxAbsDelta > 0)
                {
                    _askMap.TryGetValue(price, out var askVol);
                    _bidMap.TryGetValue(price, out var bidVol);
                    decimal delta = askVol - bidVol;
                    int deltaW = (int)(deltaMaxW * Math.Abs(delta) / MaxAbsDelta);
                    if (deltaW > 0)
                    {
                        int deltaLeft = s.AnchorRight ? profR : profL - deltaW;
                        var deltaColor = delta >= 0 ? s.DeltaPositiveColor : s.DeltaNegativeColor;
                        context.FillRectangle(deltaColor, new Rectangle(deltaLeft, y, deltaW, barH));
                    }
                }
            }

            int lineR = lineEndX ?? x2;

            // ── POC line (solid, từ x1 tới lineR) ─────────────
            if (s.ShowPocLine && POC > 0)
            {
                int yPoc = chartInfo.GetYByPrice(POC, false);
                var pocPen = new RenderPen(s.PocLineColor, s.PocLineWidth);
                context.DrawLine(pocPen, x1, yPoc, lineR, yPoc);

                if (s.ShowLabels)
                {
                    var font = new RenderFont("Arial", s.FontSize);
                    context.DrawString("POC", font, s.PocLineColor, lineR + 3, yPoc - (int)(font.Size * 0.7f));
                }
            }

            // ── VAH / VAL lines (solid, giống POC) ─────────────
            if (s.ShowVaLines && VAH > 0 && VAL > 0)
            {
                int yVah = chartInfo.GetYByPrice(VAH, false);
                int yVal = chartInfo.GetYByPrice(VAL, false);

                var vaPen = new RenderPen(s.VaLineColor, s.VaLineWidth);
                context.DrawLine(vaPen, x1, yVah, lineR, yVah);
                context.DrawLine(vaPen, x1, yVal, lineR, yVal);

                if (s.ShowLabels)
                {
                    var font = new RenderFont("Arial", s.FontSize);
                    context.DrawString("VAH", font, s.VaLineColor, lineR + 3, yVah - (int)(font.Size * 0.7f));
                    context.DrawString("VAL", font, s.VaLineColor, lineR + 3, yVal - (int)(font.Size * 0.7f));
                }
            }
        }
    }
}
