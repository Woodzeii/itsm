using itsm.Application.Common.Interfaces;
using OtpNet;
using QRCoder;

namespace itsm.Infrastructure.Identity;

public class TotpService : ITotpService
{
    public string GenerateSecret()
    {
        var bytes = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(bytes);
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            return false;

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);
            return totp.VerifyTotp(
                code.Trim(),
                out _,
                VerificationWindow.RfcSpecifiedNetworkDelay);
        }
        catch
        {
            return false;
        }
    }

    public string BuildOtpAuthUri(string issuer, string accountName, string secret)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        var issuerEscaped = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={secret}&issuer={issuerEscaped}&digits=6&period=30";
    }

    public string GenerateQrCodePngBase64(string payload, int pixelsPerModule = 8)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload для QR-кода не может быть пустым.", nameof(payload));
        if (pixelsPerModule is < 1 or > 40)
            throw new ArgumentOutOfRangeException(nameof(pixelsPerModule));

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(pixelsPerModule);
        return Convert.ToBase64String(bytes);
    }
}