namespace itsm.Application.Common.Models;

public class VerifyTwoFactorRequest
{
    public string ChallengeId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class SkipTwoFactorSetupRequest
{
    public string ChallengeId { get; set; } = string.Empty;
}

public class VerifyTwoFactorResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
}