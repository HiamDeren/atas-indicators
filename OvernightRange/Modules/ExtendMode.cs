namespace Atas_Indicators.Modules
{
    public enum ExtendMode
    {
        ToTime,    // stop at user-defined EST time (default)
        ToAxis,    // extend to right edge of chart (live bar)
        ToSweep,   // extend until price sweeps (crosses) the session High or Low
    }
}
