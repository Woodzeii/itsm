using itsm.Application.Common.Interfaces;

namespace itsm.Tests.Mocks;

public class RecordingEmailService : IEmailService
{
    public record SentEmail(string To, string? FullName, string Token);

    private readonly List<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> Sent => _sent;

    public Task SendVerificationEmailAsync(string to, string? fullName, string token, CancellationToken ct = default)
    {
        _sent.Add(new SentEmail(to, fullName, token));
        return Task.CompletedTask;
    }

    public void Clear() => _sent.Clear();
}