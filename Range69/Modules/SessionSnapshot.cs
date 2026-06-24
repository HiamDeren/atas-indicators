namespace Atas_Indicators.Modules
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SessionSnapshot — MODEL
    //  Immutable-after-lock snapshot of one trading session window.
    //  Holds raw OHLC + all pre-calculated projection levels.
    //  Lock() is called once when the session closes; IsReady guards reads.
    // ═══════════════════════════════════════════════════════════════════════════
    public sealed class SessionSnapshot
    {
        // ── Raw values ────────────────────────────────────────────────────────
        public int StartBar { get; }
        public int EndBar { get; private set; } = -1;
        public int HighBar { get; private set; }   // bar where session High was set
        public int LowBar { get; private set; }   // bar where session Low was set
        public decimal Open { get; }
        public decimal High { get; private set; }
        public decimal Low { get; private set; }

        // Volume Profile — populated after Lock() via SetVPO()
        public VolumeProfile VPO { get; private set; }

        // ── Derived (set once by Lock) ────────────────────────────────────────
        public decimal Range { get; private set; }

        // Internal levels
        public decimal EQ { get; private set; }
        public decimal L75 { get; private set; }
        public decimal L25 { get; private set; }

        // Exhausted zone ±0.1 / 0.2 / 0.3
        public decimal D01U { get; private set; }
        public decimal D01L { get; private set; }
        public decimal D02U { get; private set; }
        public decimal D02L { get; private set; }
        public decimal D03U { get; private set; }
        public decimal D03L { get; private set; }

        // Standard deviations ±1 / ±2
        public decimal D10U { get; private set; }
        public decimal D10L { get; private set; }
        public decimal D20U { get; private set; }
        public decimal D20L { get; private set; }

        // Extended Fib ±0.33 / 0.66
        public decimal F033U { get; private set; }
        public decimal F033L { get; private set; }
        public decimal F066U { get; private set; }
        public decimal F066L { get; private set; }

        // Extended Fib ±1.33 / 1.66
        public decimal F133U { get; private set; }
        public decimal F133L { get; private set; }
        public decimal F166U { get; private set; }
        public decimal F166L { get; private set; }

        // Extended Fib ±2.33 / 2.66
        public decimal F233U { get; private set; }
        public decimal F233L { get; private set; }
        public decimal F266U { get; private set; }
        public decimal F266L { get; private set; }

        // Bar index of the first bar at/after drawEnd time (-1 = not yet reached)
        public int DayEndBar { get; private set; } = -1;
        public DateTime EstDate { get; private set; }
        public DateTime CloseEstDate { get; private set; }

        // First bar after session close where price sweeps (crosses) High or Low (-1 = not yet swept)
        public int SweepBar { get; private set; } = -1;

        // True once Lock() has been called and Range > 0
        public bool IsReady => EndBar >= 0 && Range > 0;

        // ── Construction (internal — only SessionTracker creates these) ───────
        internal SessionSnapshot(int startBar, decimal open, decimal high, decimal low, DateTime estDate)
        {
            StartBar = startBar;
            HighBar = startBar;
            LowBar = startBar;
            Open = open;
            High = high;
            Low = low;
            EstDate = estDate;
        }

        public void SetVPO(VolumeProfile vpo) => VPO = vpo;

        // Called each bar after session close to detect the first sweep of High or Low
        internal void TrySweep(int bar, decimal high, decimal low)
        {
            if (SweepBar >= 0 || !IsReady) return;
            if (high >= High || low <= Low) SweepBar = bar;
        }

        // Called by SessionTracker when the RTH close bar is found
        internal void SetDayEnd(int bar) { if (DayEndBar < 0) DayEndBar = bar; }

        // Expand H/L while the session is still live
        internal void Expand(int bar, decimal high, decimal low)
        {
            if (high > High) { High = high; HighBar = bar; }
            if (low < Low) { Low = low; LowBar = bar; }
        }

        // Compute all levels exactly once when the session window closes
        internal void Lock(int endBar, DateTime closeEstDate)
        {
            EndBar = endBar;
            CloseEstDate = closeEstDate;
            Range = High - Low;
            if (Range <= 0m) return;

            EQ = Low + Range * 0.50m;
            L75 = Low + Range * 0.75m;
            L25 = Low + Range * 0.25m;

            D01U = High + Range * 0.1m; D01L = Low - Range * 0.1m;
            D02U = High + Range * 0.2m; D02L = Low - Range * 0.2m;
            D03U = High + Range * 0.3m; D03L = Low - Range * 0.3m;
            D10U = High + Range * 1.0m; D10L = Low - Range * 1.0m;
            D20U = High + Range * 2.0m; D20L = Low - Range * 2.0m;

            F033U = High + Range * 0.33m; F033L = Low - Range * 0.33m;
            F066U = High + Range * 0.66m; F066L = Low - Range * 0.66m;
            F133U = High + Range * 1.33m; F133L = Low - Range * 1.33m;
            F166U = High + Range * 1.66m; F166L = Low - Range * 1.66m;
            F233U = High + Range * 2.33m; F233L = Low - Range * 2.33m;
            F266U = High + Range * 2.66m; F266L = Low - Range * 2.66m;
        }
    }
}
