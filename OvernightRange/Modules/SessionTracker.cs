using Atas_Indicators.Modules;

namespace Atas_Indicators.Modules
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SessionTracker — CONTROLLER (reusable state machine)
    //  Detects open/update/close transitions for any fixed EST time window.
    //  Feed one bar at a time via Process(); read Last for the completed session.
    //
    //  Usage in any indicator:
    //      _tracker = new SessionTracker(new TimeSpan(6,0,0), new TimeSpan(9,0,0));
    //      // in OnCalculate:
    //      _tracker.Process(bar, candle.Time, candle.Open, candle.High, candle.Low);
    //      var session = _tracker.Last;  // non-null once first session closes
    // ═══════════════════════════════════════════════════════════════════════════
    public sealed class SessionTracker
    {
        // ── Eastern timezone (Windows + IANA fallback) ────────────────────────
        private static readonly TimeZoneInfo EasternTZ = ResolveEasternTZ();

        private static TimeZoneInfo ResolveEasternTZ()
        {
            try   { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        }

        // ── Config ────────────────────────────────────────────────────────────
        private readonly TimeSpan _start;
        private readonly TimeSpan _end;

        // Settable so indicators can change it at runtime (e.g. user edits DrawUntil parameter)
        public TimeSpan DrawEnd { get; set; }

        // ── State ─────────────────────────────────────────────────────────────
        private SessionSnapshot? _active;
        private bool             _prevIn;
        private int              _lastInBar = -1; // last bar index that was inside the window

        // ── Public output ─────────────────────────────────────────────────────

        /// <summary>Most recently completed (locked) session. Null before first close.</summary>
        public SessionSnapshot? Last { get; private set; }

        /// <summary>Session currently accumulating. Null outside the time window.</summary>
        public SessionSnapshot? Active => _active;

        // ── Constructor ───────────────────────────────────────────────────────

        /// <param name="start">Session open time in Eastern (e.g. 06:00).</param>
        /// <param name="end">Session close time in Eastern (e.g. 09:00). Supports overnight if end &lt; start.</param>
        /// <param name="drawEnd">EST time when drawn lines stop extending. Defaults to 16:15 (RTH close).</param>
        public SessionTracker(TimeSpan start, TimeSpan end, TimeSpan? drawEnd = null)
        {
            _start  = start;
            _end    = end;
            DrawEnd = drawEnd ?? new TimeSpan(16, 15, 0);
        }

        // ── API ───────────────────────────────────────────────────────────────

        /// <summary>Call on bar 0 to wipe all state before a history reload.</summary>
        public void Reset()
        {
            _active = null;
            Last    = null;
            _prevIn = false;
            _lastInBar = -1;
        }

        /// <summary>
        /// Feed one bar. Returns true when a session just completed (Last is updated).
        /// </summary>
        public bool Process(int bar, DateTime utcTime, decimal open, decimal high, decimal low)
        {
            bool inSession = IsInWindow(utcTime);

            bool sessionStarted = inSession  && !_prevIn;
            bool sessionEnded   = !inSession && _prevIn;

            if (inSession) _lastInBar = bar;

            if (sessionStarted)
                OpenSession(bar, open, high, low, utcTime);
            else if (inSession && _active != null)
                _active.Expand(bar, high, low);

            bool completed = false;
            if (sessionEnded && _active != null)
            {
                CloseSession(bar, utcTime);
                completed = true;
            }

            TrySetDayEnd(bar, utcTime);
            Last?.TrySweep(bar, high, low);

            _prevIn = inSession;
            return completed;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void OpenSession(int bar, decimal open, decimal high, decimal low, DateTime utcTime)
        {
            var estDate = ToEastern(utcTime).Date;
            _active = new SessionSnapshot(bar, open, high, low, estDate);
        }

        private void CloseSession(int bar, DateTime utcTime)
        {
            // Use _lastInBar (the actual last bar inside the window) instead of bar-1.
            // bar-1 fails when bars are non-contiguous (gaps, renko, range bars, etc.).
            int endBar = _lastInBar >= 0 ? _lastInBar : bar - 1;
            _active!.Lock(endBar, ToEastern(utcTime).Date);
            Last    = _active;
            _active = null;
            _lastInBar = -1;
        }

        // Set DayEndBar on the most recent completed session once we see 16:15 EST
        // on the same calendar day. If the day rolls over without hitting 16:15
        // (holiday early-close), we use the last bar we saw on that date.
        private void TrySetDayEnd(int bar, DateTime utcTime)
        {
            if (Last == null || Last.DayEndBar >= 0) return;

            var est = ToEastern(utcTime);

            if (est.Date == Last.CloseEstDate && est.TimeOfDay >= DrawEnd)
            {
                Last.SetDayEnd(bar);
            }
            else if (est.Date > Last.CloseEstDate)
            {
                Last.SetDayEnd(bar - 1);
            }
        }

        // Supports both day sessions (start < end) and overnight (start > end)
        private bool IsInWindow(DateTime utcTime)
        {
            var t = TimeZoneInfo.ConvertTimeFromUtc(utcTime, EasternTZ).TimeOfDay;
            return _start < _end
                ? t >= _start && t <= _end        // e.g. 06:00–09:00, end bar included
                : t >= _start || t <= _end;        // e.g. 18:00–09:30, end bar included
        }

        // ── Static utilities (usable without an instance) ─────────────────────

        /// <summary>Convert a UTC DateTime to Eastern local time.</summary>
        public static DateTime ToEastern(DateTime utc)
            => TimeZoneInfo.ConvertTimeFromUtc(utc, EasternTZ);
    }
}
