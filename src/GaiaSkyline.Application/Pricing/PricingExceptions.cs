namespace GaiaSkyline.Application.Pricing;

/// <summary>Base type for pricing/quote validation failures (surfaced as 422 by the API).</summary>
public abstract class PricingException : Exception
{
    protected PricingException(string message)
        : base(message)
    {
    }
}

/// <summary>No pricing rule covers a requested night.</summary>
public sealed class NoPriceForDateException : PricingException
{
    public NoPriceForDateException(DateOnly date)
        : base($"No nightly rate is configured for {date:yyyy-MM-dd}.")
    {
        Date = date;
    }

    public DateOnly Date { get; }
}

/// <summary>The stay is shorter than the minimum nights that apply to it.</summary>
public sealed class BelowMinimumNightsException : PricingException
{
    public BelowMinimumNightsException(int minimumNights)
        : base($"This stay requires at least {minimumNights} night(s).")
    {
        MinimumNights = minimumNights;
    }

    public int MinimumNights { get; }
}
