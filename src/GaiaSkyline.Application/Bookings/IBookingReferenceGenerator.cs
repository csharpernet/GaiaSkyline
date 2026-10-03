namespace GaiaSkyline.Application.Bookings;

/// <summary>Generates human-friendly booking reference codes (e.g. "GS-8K3M").</summary>
public interface IBookingReferenceGenerator
{
    string Next();
}
