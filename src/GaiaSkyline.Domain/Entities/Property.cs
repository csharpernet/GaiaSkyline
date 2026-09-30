using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Entities;

/// <summary>
/// The single short-term rental this site books. Modelled as an aggregate root even though
/// exactly one row is expected (a "singleton" row) — later stages add related aggregates.
/// </summary>
public sealed class Property : Entity<PropertyId>
{
    // Required by EF Core's materialization; not for application use.
    private Property()
    {
    }

    public Property(
        PropertyId id,
        string name,
        string registrationCode,
        string address,
        double lat,
        double lng,
        string defaultCurrency,
        string timezone,
        TimeOnly checkInFromLocal,
        TimeOnly checkOutByLocal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(timezone);

        if (lat is < -90d or > 90d)
        {
            throw new ArgumentOutOfRangeException(nameof(lat), lat, "Latitude must be between -90 and 90.");
        }

        if (lng is < -180d or > 180d)
        {
            throw new ArgumentOutOfRangeException(nameof(lng), lng, "Longitude must be between -180 and 180.");
        }

        var currency = defaultCurrency.Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException(
                $"'{defaultCurrency}' is not a valid ISO 4217 alphabetic currency code.", nameof(defaultCurrency));
        }

        Id = id;
        Name = name.Trim();
        RegistrationCode = registrationCode.Trim();
        Address = address.Trim();
        Lat = lat;
        Lng = lng;
        DefaultCurrency = currency;
        Timezone = timezone.Trim();
        CheckInFromLocal = checkInFromLocal;
        CheckOutByLocal = checkOutByLocal;
    }

    /// <summary>Public-facing name of the listing.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Legal short-term-rental registration / licence code (Portugal "AL" number).</summary>
    public string RegistrationCode { get; private set; } = null!;

    /// <summary>Human-readable street address.</summary>
    public string Address { get; private set; } = null!;

    /// <summary>Latitude in decimal degrees (-90..90).</summary>
    public double Lat { get; private set; }

    /// <summary>Longitude in decimal degrees (-180..180).</summary>
    public double Lng { get; private set; }

    /// <summary>Default ISO 4217 currency for pricing (e.g. "EUR").</summary>
    public string DefaultCurrency { get; private set; } = null!;

    /// <summary>IANA time-zone id for the property's local time (e.g. "Europe/Lisbon").</summary>
    public string Timezone { get; private set; } = null!;

    /// <summary>Earliest local check-in time.</summary>
    public TimeOnly CheckInFromLocal { get; private set; }

    /// <summary>Latest local check-out time.</summary>
    public TimeOnly CheckOutByLocal { get; private set; }
}
