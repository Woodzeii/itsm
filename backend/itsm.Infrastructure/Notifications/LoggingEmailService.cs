using itsm.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace itsm.Infrastructure.Notifications;

public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendVerificationEmailAsync(string to, string? fullName, string token, CancellationToken ct = default)
    {
        var greeting = string.IsNullOrWhiteSpace(fullName) ? "Здравствуйте" : $"Здравствуйте, {fullName}";
        _logger.LogWarning(
            "DEV EMAIL -> {To}\n{Greeting}!\nПодтвердите email. Токен: {Token}",
            to, greeting, token);
        return Task.CompletedTask;
    }
}