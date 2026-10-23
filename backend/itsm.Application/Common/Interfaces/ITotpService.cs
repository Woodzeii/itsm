namespace itsm.Application.Common.Interfaces;

public interface ITotpService
{
    string GenerateSecret();
    bool VerifyCode(string secret, string code);
    string BuildOtpAuthUri(string issuer, string accountName, string secret);
    string GenerateQrCodePngBase64(string payload, int pixelsPerModule = 8);
}