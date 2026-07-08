namespace Atas_Indicators.Modules
{
    public enum ExtendMode
    {
        ToTime,          // stop at user-defined EST time (default = end of AM session)
        ToCurrentBar,    // follow current bar; freeze at market close (16:14)
    }
}
