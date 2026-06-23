using System.Drawing;
using System.Drawing.Drawing2D;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace Atas_Indicators.Modules
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  LineStyle — shared style enum for all indicators in this project
    // ═══════════════════════════════════════════════════════════════════════════
    public enum LineStyle { Solid, Dotted, Dashed }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DrawHelper — VIEW primitives (static, reusable across all indicators)
    //
    //  All methods accept IChart so they work inside any Indicator's OnRender.
    //  Pass ChartInfo (from the base Indicator class) as the chart argument.
    //
    //  Quick reference:
    //      DrawHelper.HLine(ctx, ChartInfo, font, price, pen, color, x1, x2, "label");
    //      DrawHelper.VLine(ctx, ChartInfo, pen, x, priceTop, priceBot);
    //      DrawHelper.FillZone(ctx, ChartInfo, priceTop, priceBot, color, x1, x2);
    //      DrawHelper.FibBand(ctx, ChartInfo, font, ...);
    //      var pen = DrawHelper.MakePen(Color.Black, 1, LineStyle.Dotted);
    // ═══════════════════════════════════════════════════════════════════════════
    public static class DrawHelper
    {
        // ── Horizontal line + right-side label ───────────────────────────────

        /// <summary>Draw a horizontal line at <paramref name="price"/> from x1 to x2,
        /// with an optional label rendered just past x2.</summary>
        public static void HLine(
            RenderContext ctx,
            IChart        chart,
            RenderFont    font,
            decimal       price,
            RenderPen     pen,
            Color         labelColor,
            int           x1,
            int           x2,
            string?       label = null)
        {
            int y = chart.GetYByPrice(price);
            ctx.DrawLine(pen, x1, y, x2, y);

            if (!string.IsNullOrEmpty(label))
                ctx.DrawString(label, font, labelColor, x2 + 3, y - 7);
        }

        // ── Vertical line between two price levels ────────────────────────────

        /// <summary>Draw a vertical line at bar-column <paramref name="x"/>
        /// spanning from <paramref name="priceTop"/> to <paramref name="priceBot"/>.</summary>
        public static void VLine(
            RenderContext ctx,
            IChart        chart,
            RenderPen     pen,
            int           x,
            decimal       priceTop,
            decimal       priceBot)
        {
            ctx.DrawLine(pen, x, chart.GetYByPrice(priceTop),
                              x, chart.GetYByPrice(priceBot));
        }

        // ── Semi-transparent fill between two price levels ────────────────────

        /// <summary>Fill a rectangle between <paramref name="priceTop"/> and
        /// <paramref name="priceBot"/> using a semi-transparent solid color.</summary>
        public static void FillZone(
            RenderContext ctx,
            IChart        chart,
            decimal       priceTop,
            decimal       priceBot,
            Color         color,
            int           x1,
            int           x2)
        {
            int yT = chart.GetYByPrice(priceTop);
            int yB = chart.GetYByPrice(priceBot);
            if (yT > yB) (yT, yB) = (yB, yT);
            int h = Math.Max(1, yB - yT);
            ctx.FillRectangle(color, new Rectangle(x1, yT, x2 - x1, h));
        }

        // ── Fibonacci band: optional box fill + border lines + labels ─────────

        /// <summary>Draw a Fibonacci extension band consisting of two boundaries
        /// (inner and outer), an optional fill between them, and optional labels.
        /// Works for both the upper and lower leg in a single call pair.</summary>
        public static void FibBand(
            RenderContext   ctx,
            IChart          chart,
            RenderFont      font,
            decimal         inner,        // price closer to range (e.g. +0.33)
            decimal         outer,        // price farther from range (e.g. +0.66)
            FibBandSettings style,
            bool            showLines,
            bool            showBox,
            string          labelInner,
            string          labelOuter,
            int             x1,
            int             x2)
        {
            if (showBox)
                FillZone(ctx, chart, outer, inner, style.BoxColor, x1, x2);

            if (showLines)
            {
                var pen = style.MakePen();
                HLine(ctx, chart, font, inner, pen, style.Color, x1, x2, labelInner);
                HLine(ctx, chart, font, outer, pen, style.Color, x1, x2, labelOuter);
            }
        }

        // ── Volume Profile: POC + Value Area fill + VAH/VAL lines ────────────

        public static void Vpo(
            RenderContext ctx,
            IChart        chart,
            RenderFont    font,
            VolumeProfile vpo,
            LineSettings  pocStyle,
            LineSettings  vaStyle,
            Color         fillColor,
            int           x1,
            int           x2)
        {
            if (!vpo.IsReady) return;
            FillZone(ctx, chart, vpo.VAH, vpo.VAL, fillColor, x1, x2);
            HLine(ctx, chart, font, vpo.VAH, vaStyle.MakePen(),  vaStyle.Color,  x1, x2, "VAH");
            HLine(ctx, chart, font, vpo.VAL, vaStyle.MakePen(),  vaStyle.Color,  x1, x2, "VAL");
            HLine(ctx, chart, font, vpo.POC, pocStyle.MakePen(), pocStyle.Color, x1, x2, "POC");
        }

        // ── Volume Profile Histogram ──────────────────────────────────────────
        // Draws horizontal bars inside the session period (xLeft → xLeft + barWidth).
        // Max bar width fills the full session width (xRight - xLeft).

        public static void VolumeHistogram(
            RenderContext ctx,
            IChart        chart,
            VolumeProfile vpo,
            Color         barColor,     // bars outside value area
            Color         vaColor,      // bars inside value area
            Color         pocColor,     // POC bar
            int           xLeft,        // session start bar x-position
            int           xRight)       // session end bar x-position
        {
            if (!vpo.IsReady || vpo.Distribution == null || vpo.MaxVolume <= 0) return;

            int maxWidth = Math.Max(1, xRight - xLeft);

            foreach (var (price, vol) in vpo.Distribution)
            {
                // Pixel row for this price tick
                int yTop = chart.GetYByPrice(price + vpo.TickSize);
                int yBot = chart.GetYByPrice(price);
                if (yTop > yBot) (yTop, yBot) = (yBot, yTop);
                int h = Math.Max(1, yBot - yTop);

                int barW = (int)(vol / vpo.MaxVolume * maxWidth);
                if (barW <= 0) continue;

                Color c = (price == vpo.POC)
                    ? pocColor
                    : (price >= vpo.VAL && price <= vpo.VAH)
                        ? vaColor
                        : barColor;

                ctx.FillRectangle(c, new Rectangle(xLeft, yTop, barW, h));
            }
        }

        // ── RenderPen factory ─────────────────────────────────────────────────

        /// <summary>Create a <see cref="RenderPen"/> with the given color, width,
        /// and dash style. RenderPen is not IDisposable — no using{} needed.</summary>
        public static RenderPen MakePen(Color color, int width, LineStyle style)
        {
            DashStyle dash = style switch
            {
                LineStyle.Dotted => DashStyle.Dot,
                LineStyle.Dashed => DashStyle.Dash,
                _                => DashStyle.Solid
            };
            return new RenderPen(color, width, dash);
        }
    }
}
