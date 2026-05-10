using Microsoft.Extensions.Caching.Memory;
using WireMock.Server;
using WireMock.Settings;

namespace Rstolsmark.UnifiClient.Tests;

public class SslValidationTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly MemoryCache _cache;
    private readonly Credentials _credentials;
    private readonly string _jwtToken;
    private const string ResponseFolder = "responses";

    public SslValidationTests()
    {
        var invalidDomain = "invalid.com";
        var cert = WireMockSslHelper.GenerateCrossPlatformCert(invalidDomain);
        // Starts a WireMock server with SSL and an invalid certificate.
        _server = WireMockServer.Start(new WireMockServerSettings
        {
            UseSSL = true,
            CertificateSettings = new WireMockCertificateSettings()
            {
                X509Certificate = cert
            }
        });
        var loginDate = new DateTimeOffset(2021, 10, 11, 14, 33, 0, 0, TimeSpan.Zero);
        var testClock = new TestClock()
        {
            UtcNow = loginDate
        };

        _cache = new MemoryCache(new MemoryCacheOptions
        {
            Clock = testClock
        });
        _credentials = new Credentials
        {
            Username = "foo",
            Password = "bar"
        };
        _jwtToken = File.ReadAllText(Path.Combine(ResponseFolder,"JwtToken.txt"));
    }

    [Fact]
    public async Task Should_Fail_When_Handler_Check_Is_Incorrect()
    {
        var options = new UnifiClientOptions
        {
            BaseUrl = _server.Url,
            Credentials = _credentials,
            DefaultInterface = "wan",
            AllowInvalidCertificate = false
        };
        using var unifiClient = new UnifiClient(_cache, options);
        AddLoginResponse();
        await Assert.ThrowsAsync<LoginException>(async () =>
        {
            await unifiClient.Login();
        });
    }
    
    [Fact]
    public async Task Should_Not_Fail_When_Handler_Check_Is_Incorrect_And_AllowsInvalidCertifiacte()
    {
        var options = new UnifiClientOptions
        {
            BaseUrl = _server.Url,
            Credentials = _credentials,
            DefaultInterface = "wan",
            AllowInvalidCertificate = true
        };
        using var unifiClient = new UnifiClient(_cache, options);
        AddLoginResponse();
        await unifiClient.Login();
    }

    private void AddLoginResponse()
    {
        _server.Given(WireMock.RequestBuilders.Request.Create().WithPath("/api/auth/login").UsingPost())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(200)
                .WithHeader("Set-Cookie", $"TOKEN={_jwtToken}"));
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }
}