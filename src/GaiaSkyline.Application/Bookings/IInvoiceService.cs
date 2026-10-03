namespace GaiaSkyline.Application.Bookings;

/// <summary>Generates the branded PDF invoice for a confirmed booking.</summary>
public interface IInvoiceService
{
    /// <summary>The PDF bytes for the booking, or null if it isn't found or isn't payable yet.</summary>
    Task<byte[]?> GenerateAsync(string referenceCode, CancellationToken cancellationToken);
}
