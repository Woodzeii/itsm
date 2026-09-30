namespace itsm.Application.Common.Models;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool RequiresTwoFactor { get; set; }
    public int UserId { get; set; }
    public string Message { get; set; } = string.Empty;
}