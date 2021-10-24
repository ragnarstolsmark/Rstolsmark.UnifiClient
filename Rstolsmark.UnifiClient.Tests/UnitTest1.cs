using System;
using System.Threading.Tasks;
using Xunit;
using Flurl.Http;
using Flurl.Http.Testing;
using System.IO;
using Microsoft.Extensions.Caching.Memory;

namespace Rstolsmark.UnifiClient.Tests
{
    public class UnitTest1
    {
        UnifiClient _unifiClient;
        TestClock _testClock;

        public UnitTest1()
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
            var options = new UnifiClientOptions
            {
                BaseUrl = "https://example.com",
                Credentials = new Credentials
                {
                    Username = "foo",
                    Password = "bar"
                }
            };
            _unifiClient = new UnifiClient(cache, options);
        }
        [Fact]
        public async Task Login_Should_Return_False_On_Failure()
        {
            using var httpTest = new HttpTest();
            httpTest.RespondWith(status: 400);
            Assert.False(await _unifiClient.Login());
        }
        [Fact]
        public async Task Login_Should_Return_True_On_Success()
        {
            using var httpTest = new HttpTest();
            //jwttokenet er valid to 11.10.2021 15:33:28 UTC
            //inneholder også et claim som er csrfToken som er gyldig like lenge
            var jwtToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJjc3JmVG9rZW4iOiJkN2ExNmNkOS1mNzljLTQwYjgtYjllMi1lNTA0MGI5ZmQ4YjEiLCJ1c2VySWQiOiIyZTJiMTNjNC05YjZkLTRkYWMtYmVhMS04OTA4OGU3ODgwMjciLCJpYXQiOjE2MzM5NjI4MDgsImV4cCI6MTYzMzk2NjQwOH0.uOQhY_VONOCjFvTIggagoPE4uL6Rbk0ByqAMDKzdJGw";
            httpTest
                .RespondWith(status: 200, cookies: new { TOKEN = jwtToken }); //the cookie is called TOKEN
            Assert.True(await _unifiClient.Login());
        }
    }
}
