namespace Atas_Indicators.Modules
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SessionSnapshot — MODEL
    //  Immutable-after-lock snapshot of one Initial Balance window.
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

        // ── Derived (set once by Lock) ────────────────────────────────────────
        public decimal Range { get; private set; }

        // Equilibrium (midpoint)
        public decimal EQ { get; private set; }

        // Standard deviations ±0.5 / ±1 / ±1.5 / ±2
        public decimal D05U { get; private set; }
        public decimal D05L { get; private set; }
        public decimal D10U { get; private set; }
        public decimal D10L { get; private set; }
        public decimal D15U { get; private set; }
        public decimal D15L { get; private set; }
        public decimal D20U { get; private set; }
        public decimal D20L { get; private set; }

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

        // Called each bar after session close to detect the first sweep of High or Low
        internal void TrySweep(int bar, decimal high, decimal low)
        {
            if (SweepBar >= 0 || !IsReady) return;
            if (high >= High || low <= Low) SweepBar = bar;
        }

        // Called by SessionTracker when the RTH close bar is found
        internal void SetDayEnd(int bar) { if (DayEndBar < 0) DayEndBar = bar; }

        // Expand H/L while the session is still live. Also advances EndBar and
        // recomputes levels so the session can be rendered progressively (live
        // preview) before it actually closes — Lock() later overwrites EndBar
        // with the authoritative final value once the window truly ends.
        internal void Expand(int bar, decimal high, decimal low)
        {
            if (high > High) { High = high; HighBar = bar; }
            if (low < Low) { Low = low; LowBar = bar; }
            EndBar = bar;
            RecomputeLevels();
        }

        // Called once when the session window closes — freezes EndBar/CloseEstDate
        // at their final values (Expand() no longer runs after this).
        internal void Lock(int endBar, DateTime closeEstDate)
        {
            EndBar = endBar;
            CloseEstDate = closeEstDate;
            RecomputeLevels();
        }

        // Derives all projection levels from the current High/Low/Range.
        // Safe to call repeatedly (live) or once (closed) — same math either way.
        private void RecomputeLevels()
        {
            Range = High - Low;
            if (Range <= 0m) return;

            EQ = Low + Range * 0.50m;

            D05U = High + Range * 0.5m; D05L = Low - Range * 0.5m;
            D10U = High + Range * 1.0m; D10L = Low - Range * 1.0m;
            D15U = High + Range * 1.5m; D15L = Low - Range * 1.5m;
            D20U = High + Range * 2.0m; D20L = Low - Range * 2.0m;
        }
    }
}
