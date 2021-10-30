using System;
using System.Threading.Tasks;
using Xunit;
using Flurl.Http.Testing;
using Microsoft.Extensions.Caching.Memory;

namespace Rstolsmark.UnifiClient.Tests
{
    public class UnifiClientTests
    {
        UnifiClient _unifiClient;
        TestClock _testClock;
        private string _jwtToken;
        private UnifiClientOptions _options;
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
            _jwtToken =
                "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJjc3JmVG9rZW4iOiJkN2ExNmNkOS1mNzljLTQwYjgtYjllMi1lNTA0MGI5ZmQ4YjEiLCJ1c2VySWQiOiIyZTJiMTNjNC05YjZkLTRkYWMtYmVhMS04OTA4OGU3ODgwMjciLCJpYXQiOjE2MzM5NjI4MDgsImV4cCI6MTYzMzk2NjQwOH0.uOQhY_VONOCjFvTIggagoPE4uL6Rbk0ByqAMDKzdJGw";
        }
        [Fact]
        public async Task Login_Should_Throw_Exception_On_Failure()
        {
            using var httpTest = new HttpTest();
            httpTest.RespondWith(status: 400);
            await Assert.ThrowsAsync<LoginException>( _unifiClient.Login);
        }
        [Fact]
        public async Task GetTokens_Should_Cache_Tokens()
        {
            using var httpTest = new HttpTest();
            //the cookie is called TOKEN
            httpTest
                .RespondWith(status: 200, cookies: new { TOKEN = _jwtToken }); 
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
    }
}
