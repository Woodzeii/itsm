namespace itsm.Application.Common.Models;


public class TwoFactorSetupResponse
{

    public string Secret { get; set; } = string.Empty;

    public string OtpAuthUri { get; set; } = string.Empty;

    public string Issuer { get; set; } = "ITSM NC";
}

public class VerifyTwoFactorSetupRequest
{

    public string Code { get; set; } = string.Empty;
}

public class TwoFactorStatusResponse
{
    public bool IsEnabled { get; set; }
    public DateTimeOffset? EnabledAt { get; set; }
}



public class DisableTwoFactorRequest
{

    public string Password { get; set; } = string.Empty;
}