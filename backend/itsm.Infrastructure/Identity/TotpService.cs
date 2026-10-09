using itsm.Application.Common.Interfaces;
using OtpNet;

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
}