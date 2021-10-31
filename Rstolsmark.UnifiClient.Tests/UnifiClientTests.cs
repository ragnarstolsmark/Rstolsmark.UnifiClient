using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Flurl.Http.Testing;
using Microsoft.Extensions.Caching.Memory;
using NuGet.Frameworks;

namespace Rstolsmark.UnifiClient.Tests
{
    public class UnifiClientTests
    {
        private readonly UnifiClient _unifiClient;
        private readonly TestClock _testClock;
        private readonly string _jwtToken;
        private readonly UnifiClientOptions _options;
        private const string ResponseFolder = "responses";
        public UnifiClientTests()
        {
            var loginDate = new DateTimeOffset(2021, 10, 11, 14, 33, 0, 0, TimeSpan.Zero);
            _testClock = new TestClock()
            {
                UtcNow = loginDate
            };

            var cache = new MemoryCache(new MemoryCacheOptions
            {
                Clock = _testClock
            });
            _options = new UnifiClientOptions
            {
                BaseUrl = "https://example.com",
                Credentials = new Credentials
                {
                    Username = "foo",
                    Password = "bar"
                }
            };
            _unifiClient = new UnifiClient(cache, _options);
            //The jwt token is valid to 11.10.2021 15:33:28 UTC
            //It also contains a clam named csrfToken that is valid in the same time span
            _jwtToken = File.ReadAllText(Path.Combine(ResponseFolder,"jwtToken.txt"));
        }
        [Fact]
        public async Task Login_Should_Throw_Exception_On_Failure()
        {
            using var httpTest = new HttpTest();
            httpTest.RespondWith(status: 400);
            await Assert.ThrowsAsync<LoginException>( _unifiClient.Login);
        }
        [Fact]
        public async Task Get_Tokens_Should_Cache_Tokens()
        {
            using var httpTest = new HttpTest();
            AddLoginSuccessCall(httpTest);
            await _unifiClient.GetTokens();
            await _unifiClient.GetTokens();
            //Forward the test clock to two minutes after the token expires to see that login is called again
            _testClock.UtcNow = new DateTimeOffset(2021, 10, 11, 15, 35, 0, 0, TimeSpan.Zero);
            await _unifiClient.GetTokens();
            httpTest.ShouldHaveCalled($"{_options.BaseUrl}/api/auth/login")
                .WithContentType("application/json")
                .WithRequestBody($@"{{""username"":""{_options.Credentials.Username}"",""password"":""{_options.Credentials.Password}""}}")
                .Times(2);
        }

        private void AddLoginSuccessCall(HttpTest httpTest)
        {
            //the cookie is called TOKEN
            httpTest
                .RespondWith(cookies: new {TOKEN = _jwtToken});
        }

        [Fact]
        public async Task Get_Port_Forward_Settings_Should_Return_List()
        {
            using var httpTest = new HttpTest();
            AddLoginSuccessCall(httpTest);
            var portForwardResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "GetCurrentPortForward.json")); 
            httpTest
                .RespondWith(portForwardResponse);
            var portForwardSettings = await _unifiClient.GetPortForwardSettings();
            Assert.Single(portForwardSettings);
            Assert.Equal("57.173.50.35",portForwardSettings[0].Source);
        }
    }
}
