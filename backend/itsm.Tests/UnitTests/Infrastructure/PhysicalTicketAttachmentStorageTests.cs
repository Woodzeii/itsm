using itsm.Infrastructure.Persistence.Services;
using Microsoft.Extensions.Configuration;

namespace itsm.Tests.UnitTests.Infrastructure;

public sealed class PhysicalTicketAttachmentStorageTests
{
    [Fact]
    public async Task SaveOpenAndDeleteUsesOpaqueStorageKey()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"itsm-attachments-{Guid.NewGuid():N}");
        var storage = CreateStorage(rootPath);

        try
        {
            var bytes = "ticket attachment"u8.ToArray();
            await using var source = new MemoryStream(bytes);
            var key = await storage.SaveAsync(source);

            Assert.True(Guid.TryParseExact(key, "N", out _));

            await using (var saved = await storage.OpenReadAsync(key))
            {
                Assert.NotNull(saved);
                using var result = new MemoryStream();
                await saved.CopyToAsync(result);
                Assert.Equal(bytes, result.ToArray());
            }

            await storage.DeleteAsync(key);
            Assert.Null(await storage.OpenReadAsync(key));
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task OpenReadRejectsPathTraversal()
    {
        var storage = CreateStorage(Path.Combine(Path.GetTempPath(), $"itsm-attachments-{Guid.NewGuid():N}"));

        await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync("..\\outside"));
    }

    private static PhysicalTicketAttachmentStorage CreateStorage(string rootPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:TicketAttachmentsPath"] = rootPath
            })
            .Build();

        return new PhysicalTicketAttachmentStorage(configuration);
    }
}
