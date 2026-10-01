namespace itsm.Application.Common.Models;

public enum ConfirmEmailStatus
{
    Success,
    InvalidToken,
    AlreadyVerified,
    Expired
}

public class ConfirmEmailResult
{
    public ConfirmEmailStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}