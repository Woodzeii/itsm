namespace itsm.Application.Common.Models;

public class ConfirmEmailRequest
{
    public string Token { get; set; } = string.Empty;
}

public class ConfirmEmailResponse
{
    public string Message { get; set; } = string.Empty;
}