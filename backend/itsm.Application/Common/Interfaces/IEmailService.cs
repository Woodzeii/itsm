namespace itsm.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string to, string? fullName, string token, CancellationToken ct = default);
}