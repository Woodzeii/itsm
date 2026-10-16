using itsm.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace itsm.Infrastructure.Persistence.Services;

public sealed class PhysicalTicketAttachmentStorage : ITicketAttachmentStorage
{
    private readonly string _rootPath;

    public PhysicalTicketAttachmentStorage(IConfiguration configuration)
    {
        var configuredPath = configuration["FileStorage:TicketAttachmentsPath"];
        var resolvedPath = configuredPath is null
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "ticket-attachments")
            : Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppContext.BaseDirectory, configuredPath);
        _rootPath = Path.GetFullPath(resolvedPath);
    }

    public async Task<string> SaveAsync(Stream content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_rootPath);
        var storageKey = $"{Guid.NewGuid():N}";
        var path = ResolvePath(storageKey);
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await content.CopyToAsync(file, ct);
        }
        catch
        {
            if (File.Exists(path))
                File.Delete(path);
            throw;
        }
        return storageKey;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        if (!Guid.TryParseExact(storageKey, "N", out _))
            throw new ArgumentException("Некорректный идентификатор файла.", nameof(storageKey));

        return Path.Combine(_rootPath, storageKey);
    }
}
