using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Moq;

namespace itsm.Tests;

public class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        ResetTwoFactorMock();
    }

    /// <summary>
    /// Сбрасывает мок ITwoFactorService к дефолтному поведению.
    /// Нужно, потому что тест QA.5 перезаписывает setup, и без сброса
    /// это сломает последующие тесты (общая фабрика).
    /// </summary>
    private void ResetTwoFactorMock()
    {
        _factory.TwoFactorMock.Reset();

        _factory.TwoFactorMock
            .Setup(x => x.GenerateCode(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(CustomWebApplicationFactory.ValidCode);

        _factory.TwoFactorMock
            .Setup(x => x.ValidateCode(It.IsAny<int>(), CustomWebApplicationFactory.ValidCode))
            .Returns((int userId, string _) => new TwoFactorUserData
            {
                UserId = userId,
                Login = "admin",
                Email = "admin@itsm.local"
            });

        _factory.TwoFactorMock
            .Setup(x => x.ValidateCode(It.IsAny<int>(), It.Is<string>(c => c != CustomWebApplicationFactory.ValidCode)))
            .Returns((TwoFactorUserData?)null);

        _factory.TwoFactorMock
            .Setup(x => x.InvalidateCode(It.IsAny<int>()));
    }

    // ============ AUTH-QA.2: успешный сценарий ============
    [Fact]
    public async Task Login_Then_Verify2Fa_ReturnsJwt()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();
        login!.RequiresTwoFactor.Should().BeTrue();
        login.UserId.Should().BeGreaterThan(0);

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest
            {
                UserId = login.UserId,
                Code = CustomWebApplicationFactory.ValidCode
            });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var verify = await verifyResponse.Content.ReadFromJsonAsync<VerifyTwoFactorResponse>();
        verify.Should().NotBeNull();
        verify!.AccessToken.Should().NotBeNullOrWhiteSpace();
        verify.TokenType.Should().Be("Bearer");
        verify.ExpiresIn.Should().BeGreaterThan(0);
    }

    // ============ AUTH-QA.3: блокировка неверного пароля ============
    [Fact]
    public async Task Login_WithWrongPassword_Returns401_AndNoSession()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "wrong-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadAsStringAsync() ?? string.Empty;
        body.Should().NotContain("eyJ");
    }

    // ============ AUTH-QA.4: неверный 2FA-код ============
    [Fact]
    public async Task Verify2Fa_WithWrongCode_Returns401_AndNoJwt()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest { UserId = login!.UserId, Code = "000000" });

        verifyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await verifyResponse.Content.ReadAsStringAsync() ?? string.Empty;
        body.Should().NotContain("accessToken");
        body.Should().NotContain("eyJ");
    }

    // ============ AUTH-QA.5: сгорание кода ============
    [Fact]
    public async Task Verify2Fa_WithExpiredCode_Returns401()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();

        // Эмулируем сгорание: мок начинает возвращать null для этого userId.
        _factory.TwoFactorMock
            .Setup(x => x.ValidateCode(login!.UserId, It.IsAny<string>()))
            .Returns((TwoFactorUserData?)null);

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest
            {
                UserId = login.UserId,
                Code = CustomWebApplicationFactory.ValidCode
            });

        verifyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ============ AUTH-QA.6: структура JWT ============
    [Fact]
    public async Task Verify2Fa_ReturnsJwtWithRequiredClaims()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest
            {
                UserId = login!.UserId,
                Code = CustomWebApplicationFactory.ValidCode
            });
        var verify = await verifyResponse.Content.ReadFromJsonAsync<VerifyTwoFactorResponse>();
        verify.Should().NotBeNull();

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(verify!.AccessToken);

        token.Claims.Should().Contain(c => c.Type == "Id" && c.Value == login.UserId.ToString());
        token.Claims.Should().Contain(c => c.Type == "Login" && c.Value == "admin");
        token.Claims.Should().Contain(c => c.Type == "Email" && c.Value == "admin@itsm.local");
        token.Claims.Should().Contain(c => c.Type == "iss" && c.Value == "itsm-api");
        token.Claims.Should().Contain(c => c.Type == "aud" && c.Value == "itsm-client");
    }
}