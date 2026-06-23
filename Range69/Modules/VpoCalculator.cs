namespace Atas_Indicators.Modules
{
    public struct VolumeProfile
    {
        public decimal POC;        // Point of Control
        public decimal VAH;        // Value Area High
        public decimal VAL;        // Value Area Low
        public decimal MaxVolume;  // volume at POC — used to scale histogram bars
        public decimal TickSize;   // price step used to build the profile
        public bool    IsReady;

        // Full price → volume map for histogram rendering (null until calculated)
        public Dictionary<decimal, decimal>? Distribution;
    }

    public static class VpoCalculator
    {
        public static VolumeProfile Calculate(
            IEnumerable<(decimal high, decimal low, decimal volume)> bars,
            decimal tickSize,
            decimal valueAreaPct = 0.70m)
        {
            if (tickSize <= 0) return default;

            var profile = new Dictionary<decimal, decimal>();
            decimal total = 0;

            foreach (var (high, low, vol) in bars)
            {
                if (vol <= 0) continue;

                // Round both ends to nearest tick, inclusive [bot, top]
                long bot    = (long)Math.Round(low  / tickSize);
                long top    = (long)Math.Round(high / tickSize);
                long levels = Math.Max(top - bot + 1, 1);  // +1 = inclusive of HIGH tick
                decimal vpl = vol / levels;

                for (long t = bot; t <= top; t++)
                {
                    decimal price = t * tickSize;
                    profile.TryGetValue(price, out var existing);
                    profile[price] = existing + vpl;
                }
                total += vol;
            }

            if (profile.Count == 0 || total <= 0) return default;

            // POC = tick with highest volume
            var poc      = profile.MaxBy(kv => kv.Value);
            decimal pocP = poc.Key;

            // Value Area: expand from POC one tick at a time in both directions,
            // always taking the side with more volume (standard CME method)
            decimal target = total * valueAreaPct;
            decimal acc    = poc.Value;
            var sorted     = profile.Keys.Order().ToArray();
            int upIdx      = Array.IndexOf(sorted, pocP);
            int dnIdx      = upIdx;

            while (acc < target)
            {
                bool canUp = upIdx + 1 < sorted.Length;
                bool canDn = dnIdx - 1 >= 0;
                if (!canUp && !canDn) break;

                decimal upVol = canUp ? profile[sorted[upIdx + 1]] : 0;
                decimal dnVol = canDn ? profile[sorted[dnIdx - 1]] : 0;

                if (upVol >= dnVol && canUp) { upIdx++; acc += upVol; }
                else if (canDn)              { dnIdx--; acc += dnVol; }
            }

            return new VolumeProfile
            {
                POC          = pocP,
                VAH          = sorted[upIdx],
                VAL          = sorted[dnIdx],
                MaxVolume    = poc.Value,
                TickSize     = tickSize,
                IsReady      = true,
                Distribution = profile,
            };
        }
    }
}
