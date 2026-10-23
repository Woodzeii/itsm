namespace itsm.Application.Common.Models;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool RequiresTwoFactor { get; set; }
    public bool RequiresTwoFactorSetup { get; set; }
    public bool CanSkipTwoFactorSetup { get; set; }
    public int UserId { get; set; }
    public string ChallengeId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string OtpAuthUri { get; set; } = string.Empty;
    public string QrCodeImageBase64 { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}