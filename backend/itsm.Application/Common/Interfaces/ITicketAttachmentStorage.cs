namespace itsm.Application.Common.Interfaces;

public interface ITicketAttachmentStorage
{
    Task<string> SaveAsync(Stream content, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
