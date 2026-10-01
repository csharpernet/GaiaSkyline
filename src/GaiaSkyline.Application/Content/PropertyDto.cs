namespace GaiaSkyline.Application.Content;

/// <summary>Read model for the singleton property (used by structured data and the location section).</summary>
public sealed record PropertyDto(
    string Name,
    string RegistrationCode,
    string Address,
    double Lat,
    double Lng,
    string DefaultCurrency,
    string Timezone,
    TimeOnly CheckInFromLocal,
    TimeOnly CheckOutByLocal,
    int Sleeps,
    int Bedrooms,
    int Beds,
    int Bathrooms,
    string BedsBreakdown);
