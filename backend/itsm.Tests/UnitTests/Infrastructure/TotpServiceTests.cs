using FluentAssertions;
using itsm.Infrastructure.Identity;

namespace itsm.Tests.UnitTests.Infrastructure;

public sealed class TotpServiceTests
{
    private readonly TotpService _service = new();

    [Fact]
    public void GenerateQrCodePngBase64_ReturnsValidPngBytes()
    {
        const string uri = "otpauth://totp/ITSM%20NC:admin?secret=JBSWY3DPEHPK3PXP&issuer=ITSM%20NC&digits=6&period=30";

        var base64 = _service.GenerateQrCodePngBase64(uri);

        base64.Should().NotBeNullOrWhiteSpace();
        var bytes = Convert.FromBase64String(base64);
        bytes.Should().HaveCountGreaterThan(100);
        bytes[0].Should().Be(0x89);
        bytes[1].Should().Be((byte)'P');
        bytes[2].Should().Be((byte)'N');
        bytes[3].Should().Be((byte)'G');
    }
}
