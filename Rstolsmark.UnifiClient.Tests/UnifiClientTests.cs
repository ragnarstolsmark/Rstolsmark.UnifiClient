using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Flurl.Http.Testing;
using Microsoft.Extensions.Caching.Memory;

namespace Rstolsmark.UnifiClient.Tests
{
    public class UnifiClientTests
    {
        private readonly UnifiClient _unifiClient;
        private readonly TestClock _testClock;
        private readonly string _jwtToken;
        private readonly UnifiClientOptions _options;
        private const string ResponseFolder = "responses";
        private const string RequestFolder = "requests";
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
                },
                DefaultInterface = "wan"
            };
            _unifiClient = new UnifiClient(cache, _options);
            //The jwt token is valid to 11.10.2021 15:33:28 UTC
            //It also contains a clam named csrfToken that is valid in the same time span
            _jwtToken = File.ReadAllText(Path.Combine(ResponseFolder,"JwtToken.txt"));
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
            var expectedRequest = await File.ReadAllTextAsync(Path.Combine(RequestFolder, "Login.json"));
            httpTest.ShouldHaveCalled($"{_options.BaseUrl}/api/auth/login")
                .WithContentType("application/json")
                .WithRequestBody(expectedRequest)
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

        [Fact]
        public async Task Delete_Port_Forward_With_Invalid_Id_Should_Throw_IdInvalid_Exception()
        {
            using var httpTest = new HttpTest();
            AddLoginSuccessCall(httpTest);
            var deletePortForwardResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "DeletePortForwardInvalidId.json")); 
            httpTest
                .RespondWith(body: deletePortForwardResponse, status: 400);
            await Assert.ThrowsAsync<IdInvalidException>(()=> _unifiClient.DeletePortForwardSetting("60478d7f8e188e04d2ff3e8a"));
        }
        [Fact]
        public async Task Delete_Port_Forward_With_Valid_Id_Should_Succeed()
        {
            using var httpTest = new HttpTest();
            AddLoginSuccessCall(httpTest);
            var deletePortForwardResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "DeletePortForwardSuccess.json")); 
            httpTest
                .RespondWith(deletePortForwardResponse);
            await _unifiClient.DeletePortForwardSetting("60478d7f8e188e04d2ff3e8e");
        }
        [Fact]
        public async Task Create_PortForward_Should_Return_PortForward()
        {
            using var httpTest = new HttpTest();
            AddLoginSuccessCall(httpTest);
            var createPortForwardResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "CreatePortForward.json")); 
            httpTest
                .RespondWith(createPortForwardResponse);
            var portForward = new PortForwardForm
            {
                Name = "Some external port",
                Enabled = true,
                Source = "242.151.234.222",
                DestinationPort = "3391",
                Forward = "192.168.5.93",
                ForwardPort = "3389",
                Protocol = "tcp",
                Log = false
            };
            var portForwardSetting = await _unifiClient.CreatePortForwardSetting(portForward);
            var tokens = await _unifiClient.GetTokens();
            var expectedRequest = await File.ReadAllTextAsync(Path.Combine(RequestFolder, "CreatePortForward.json"));
            httpTest.ShouldHaveCalled($"{_options.BaseUrl}/proxy/network/api/s/default/rest/portforward")
                .WithContentType("application/json")
                .WithHeader("X-CSRF-Token",tokens.CsrfToken)
                .WithCookie("TOKEN", tokens.JwtToken)
                .WithRequestBody(expectedRequest);
            Assert.Equal("6156a2368e188e7795ff6399", portForwardSetting.Id);
        }
        [Fact]
        public async Task Get_PortForwardById_Should_Return_PortForward()
        {
            using var httpTest = new HttpTest();
            AddLoginSuccessCall(httpTest);
            var GetByIdResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "GetById.json")); 
            httpTest
                .RespondWith(GetByIdResponse);
            var id = "6156a2368e188e7795ff6399";
            var portForwardSetting = await _unifiClient.GetPortForwardById(id);
            var tokens = await _unifiClient.GetTokens();
            httpTest.ShouldHaveCalled($"{_options.BaseUrl}/proxy/network/api/s/default/rest/portforward/{id}")
                .WithCookie("TOKEN", tokens.JwtToken);
            Assert.Equal(id, portForwardSetting.Id);
            
        }
    }
}
