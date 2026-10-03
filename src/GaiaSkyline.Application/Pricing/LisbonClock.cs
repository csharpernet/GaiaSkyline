namespace GaiaSkyline.Application.Pricing;

/// <summary>Today's date in the apartment's timezone (Europe/Lisbon), from an injectable clock.</summary>
public static class LisbonClock
{
    private static readonly TimeZoneInfo Lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    public static DateOnly Today(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var local = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Lisbon);
        return DateOnly.FromDateTime(local.DateTime);
    }
}
