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
                ctx.DrawString(label, font, labelColor, x2 + 3, y - (int)(font.Size * 0.7f));
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
