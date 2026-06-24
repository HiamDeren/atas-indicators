using System;
using System.Collections.Generic;
using System.Linq;
using ATAS.Indicators;

namespace Atas_Indicators.Modules
{
    // ─────────────────────────────────────────────────────────────────────────
    // VolumeProfile result struct — unchanged, fully backward-compatible
    // ─────────────────────────────────────────────────────────────────────────
    public struct VolumeProfile
    {
        public decimal POC;          // Point of Control (price with highest volume)
        public decimal VAH;          // Value Area High
        public decimal VAL;          // Value Area Low
        public decimal MaxVolume;    // Volume at POC — used to scale histogram bars
        public decimal TotalVolume;  // Sum of all volume in profile
        public decimal TickSize;     // Price step used to build the profile
        public bool IsReady;

        // Full price → volume map for histogram rendering (null until calculated)
        public Dictionary<decimal, decimal>? Distribution;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VpoCalculator
    //
    // PRIMARY (ATAS real cluster data):
    //   BuildCluster()  — accumulate tick-accurate volume per price level
    //   Calculate()     — compute POC / VAH / VAL from a pre-built cluster dict
    //
    // LEGACY FALLBACK (equal-distribution, use when no cluster data available):
    //   CalculateFromBars() — same as old Calculate(), kept for compatibility
    // ─────────────────────────────────────────────────────────────────────────
    public static class VpoCalculator
    {
        // ═════════════════════════════════════════════════════════════════════
        // STEP 1 — Build cluster dict from ATAS real tick data
        //
        // Call this inside your indicator's OnCalculate loop (or from a helper),
        // passing the GetCandle(bar) reference and the instrument tick size.
        //
        // Usage:
        //   var cluster = new Dictionary<decimal, decimal>();
        //   for (int i = sessionStart; i <= currentBar; i++)
        //       VpoCalculator.BuildCluster(GetCandle(i), tickSize, cluster);
        //   var vpo = VpoCalculator.Calculate(cluster, tickSize);
        // ═════════════════════════════════════════════════════════════════════
        public static void BuildCluster(
            IndicatorCandle candle,
            decimal tickSize,
            Dictionary<decimal, decimal> cluster)
        {
            if (tickSize <= 0 || candle.High < candle.Low) return;

            // Snap Low/High to nearest tick boundary
            decimal low = Math.Round(candle.Low / tickSize) * tickSize;
            decimal high = Math.Round(candle.High / tickSize) * tickSize;

            for (decimal price = low; price <= high; price += tickSize)
            {
                // GetPriceVolumeInfo returns the REAL traded volume at this exact
                // tick level inside this bar — accurate footprint / cluster data.
                // Returns null if no trades occurred at this price level.
                var pv = candle.GetPriceVolumeInfo(price);
                if (pv == null || pv.Volume <= 0) continue;

                cluster.TryGetValue(price, out decimal existing);
                cluster[price] = existing + pv.Volume;
            }
        }

        // Convenience overload: build from a range of bars in one call
        public static Dictionary<decimal, decimal> BuildCluster(
            Func<int, IndicatorCandle> getCandle,
            int startBar,
            int endBar,
            decimal tickSize)
        {
            var cluster = new Dictionary<decimal, decimal>(capacity: 256);
            for (int i = startBar; i <= endBar; i++)
                BuildCluster(getCandle(i), tickSize, cluster);
            return cluster;
        }

        // ═════════════════════════════════════════════════════════════════════
        // STEP 2 — Compute POC / VAH / VAL from cluster dict
        //
        // This is the single source of truth for all VA math.
        // Both the real-data path and the legacy path converge here.
        // ═════════════════════════════════════════════════════════════════════
        public static VolumeProfile Calculate(
            Dictionary<decimal, decimal> cluster,
            decimal tickSize,
            decimal valueAreaPct = 0.70m)
        {
            if (cluster == null || cluster.Count == 0 || tickSize <= 0)
                return default;

            decimal total = 0m;
            foreach (var vol in cluster.Values) total += vol;
            if (total <= 0) return default;

            // ── POC ──────────────────────────────────────────────────────────
            var pocEntry = cluster.MaxBy(kv => kv.Value);
            decimal pocPrice = pocEntry.Key;
            decimal pocVol = pocEntry.Value;

            // ── Value Area expansion (CME standard method) ────────────────────
            // Expand outward from POC, always consuming the side with more volume.
            // Stop when accumulated volume >= target.
            decimal target = total * valueAreaPct;
            decimal acc = pocVol;

            // Early exit: POC alone already covers the value area
            // (happens with very few price levels or very high valueAreaPct)
            decimal[] sorted = cluster.Keys.Order().ToArray();
            int upIdx = Array.IndexOf(sorted, pocPrice);
            int dnIdx = upIdx;

            while (acc < target)
            {
                bool canUp = upIdx + 1 < sorted.Length;
                bool canDn = dnIdx - 1 >= 0;
                if (!canUp && !canDn) break;

                decimal upVol = canUp ? cluster[sorted[upIdx + 1]] : 0m;
                decimal dnVol = canDn ? cluster[sorted[dnIdx - 1]] : 0m;

                // Tie goes to upside (standard rule)
                if (upVol >= dnVol && canUp) { upIdx++; acc += upVol; }
                else if (canDn) { dnIdx--; acc += dnVol; }
            }

            return new VolumeProfile
            {
                POC = pocPrice,
                VAH = sorted[upIdx],
                VAL = sorted[dnIdx],
                MaxVolume = pocVol,
                TotalVolume = total,
                TickSize = tickSize,
                IsReady = true,
                Distribution = cluster,
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        // LEGACY FALLBACK — equal-distribution (no cluster data)
        //
        // Kept for backward compatibility / non-ATAS environments.
        // Volume is spread evenly across all ticks in [low, high].
        // Less accurate than real cluster data — POC/VA will differ from
        // ATAS built-in Market Profile when price traded unevenly in the bar.
        // ═════════════════════════════════════════════════════════════════════
        public static VolumeProfile CalculateFromBars(
            IEnumerable<(decimal high, decimal low, decimal volume)> bars,
            decimal tickSize,
            decimal valueAreaPct = 0.70m)
        {
            if (tickSize <= 0) return default;

            var cluster = new Dictionary<decimal, decimal>(capacity: 256);
            decimal total = 0m;

            foreach (var (high, low, vol) in bars)
            {
                if (vol <= 0 || high < low) continue;

                long bot = (long)Math.Round(low / tickSize);
                long top = (long)Math.Round(high / tickSize);
                long levels = Math.Max(top - bot + 1, 1);
                decimal vpl = vol / levels;

                for (long t = bot; t <= top; t++)
                {
                    decimal price = t * tickSize;
                    cluster.TryGetValue(price, out decimal existing);
                    cluster[price] = existing + vpl;
                }
                total += vol;
            }

            // Reuse the same VA math — single source of truth
            return Calculate(cluster, tickSize, valueAreaPct);
        }
    }
}
