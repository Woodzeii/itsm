namespace itsm.Application.Common.Interfaces;

public interface ITotpService
{
    string GenerateSecret();
    bool VerifyCode(string secret, string code);
    string BuildOtpAuthUri(string issuer, string accountName, string secret);
}