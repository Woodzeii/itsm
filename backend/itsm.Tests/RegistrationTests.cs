using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace itsm.Tests;

[Collection(IntegrationTestCollection.Name)]
public class RegistrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    static RegistrationTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", CustomWebApplicationFactory.TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "itsm-api");
        Environment.SetEnvironmentVariable("Jwt__Audience", "itsm-client");
        Environment.SetEnvironmentVariable("Jwt__ExpiresMinutes", "60");

        Environment.SetEnvironmentVariable("POSTGRES_DB", "itsm_tests");
        Environment.SetEnvironmentVariable("POSTGRES_USER", "test");
        Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", "test");
        Environment.SetEnvironmentVariable("POSTGRES_HOST", "localhost");
        Environment.SetEnvironmentVariable("POSTGRES_PORT", "5432");
    }

    public RegistrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EmailService.Clear();
    }

    private static RegisterRequest ValidRequest(string username) => new()
    {
        Username = username,
        Email = $"{username}@itsm.local",
        Password = "Test1234",
        FullName = "Test User"
    };

    // ============ AUTH-QA.7 ============
    [Fact]
    public async Task Register_WithValidData_Returns200_CreatesUnverifiedUser_SendsEmail()
    {
        var client = _factory.CreateClient();
        var request = ValidRequest($"qa7_{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        body.Should().NotBeNull();
        body!.UserId.Should().BeGreaterThan(0);
        body.Status.Should().Be(UserStatuses.Unverified);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        user.Should().NotBeNull();
        user!.Status.Should().Be(UserStatuses.Unverified);
        user.PasswordHash.Should().NotBeNullOrEmpty();
        user.VerificationTokenHash.Should().NotBeNullOrEmpty();

        _factory.EmailService.Sent.Should().ContainSingle();
        var email = _factory.EmailService.Sent[0];
        email.To.Should().Be(request.Email);
        email.FullName.Should().Be(request.FullName);
        email.Token.Should().NotBeNullOrWhiteSpace();
    }

    // ============ AUTH-QA.8 ============
    [Fact]
    public async Task Register_WithoutFullName_CreatesUser_WithEmptyFullName()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest
        {
            Username = $"qa8_{Guid.NewGuid():N}",
            Email = $"qa8_{Guid.NewGuid():N}@itsm.local",
            Password = "Test1234"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        user.Should().NotBeNull();
        user!.FullName.Should().Be(string.Empty);
    }

    // ============ AUTH-QA.9 ============
    [Fact]
    public async Task Register_WithDuplicateUsername_Returns409()
    {
        var client = _factory.CreateClient();
        var first = ValidRequest($"qa9a_{Guid.NewGuid():N}");
        await client.PostAsJsonAsync("/api/auth/register", first);

        var second = new RegisterRequest
        {
            Username = first.Username,
            Email = $"other_{Guid.NewGuid():N}@itsm.local",
            Password = "Test1234"
        };
        var response = await client.PostAsJsonAsync("/api/auth/register", second);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var first = ValidRequest($"qa9b_{Guid.NewGuid():N}");
        await client.PostAsJsonAsync("/api/auth/register", first);

        var second = new RegisterRequest
        {
            Username = $"other_{Guid.NewGuid():N}",
            Email = first.Email,
            Password = "Test1234"
        };
        var response = await client.PostAsJsonAsync("/api/auth/register", second);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ============ AUTH-QA.10 ============
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("no@")]
    [InlineData("@nodomain")]
    [InlineData("spaces in@email.com")]
    public async Task Register_WithInvalidEmail_Returns400(string invalidEmail)
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest
        {
            Username = $"qa10_{Guid.NewGuid():N}",
            Email = invalidEmail,
            Password = "Test1234"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ============ AUTH-QA.11 ============
    [Theory]
    [InlineData("123")]
    [InlineData("abcdefgh")]
    [InlineData("12345678")]
    [InlineData("")]
    public async Task Register_WithWeakPassword_Returns400(string weakPassword)
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest
        {
            Username = $"qa11_{Guid.NewGuid():N}",
            Email = $"qa11_{Guid.NewGuid():N}@itsm.local",
            Password = weakPassword
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}