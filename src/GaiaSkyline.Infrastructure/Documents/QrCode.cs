using QRCoder;

namespace GaiaSkyline.Infrastructure.Documents;

/// <summary>Renders a QR code to PNG bytes for embedding in a PDF (Stage 8). Black on white scans best.</summary>
public static class QrCode
{
    public static byte[] Png(string payload, int pixelsPerModule = 8)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }
}
