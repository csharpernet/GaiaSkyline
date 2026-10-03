namespace GaiaSkyline.Domain.Pricing;

/// <summary>The kind of fee applied to a booking.</summary>
public enum FeeType
{
    /// <summary>A one-off cleaning fee charged per stay.</summary>
    Cleaning,

    /// <summary>Municipal tourist tax, charged per paying guest per night up to a capped number of nights.</summary>
    TouristTax,
}
