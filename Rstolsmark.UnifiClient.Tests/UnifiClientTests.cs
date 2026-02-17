using System.Text.Json;
using Flurl.Http;
using Flurl.Http.Configuration;
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
        
        private static void ConfigureHttpTest(HttpTest httpTest)
        {
            // Configure HttpTest to use the same serializer settings as the client
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            httpTest.Settings.JsonSerializer = new DefaultJsonSerializer(jsonOptions);
        }
        
        public UnifiClientTests()
        {
            // Clear the Flurl client cache to ensure HttpTest can intercept calls
            FlurlHttp.Clients.Clear();
            
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
        public void UnifiClient_With_AllowInvalidCertificate_Should_Initialize()
        {
            // Clear the Flurl client cache to ensure a fresh client is created
            FlurlHttp.Clients.Clear();
            
            var cache = new MemoryCache(new MemoryCacheOptions
            {
                Clock = _testClock
            });
            var options = new UnifiClientOptions
            {
                BaseUrl = "https://test-invalid-cert.example.com",
                Credentials = new Credentials
                {
                    Username = "test",
                    Password = "test"
                },
                AllowInvalidCertificate = true
            };
            
            // This should not throw an exception
            var client = new UnifiClient(cache, options);
            Assert.NotNull(client);
        }
        
        [Fact]
        public async Task Login_Should_Throw_Exception_On_Failure()
        {
            using var httpTest = new HttpTest();
            ConfigureHttpTest(httpTest);
            httpTest.RespondWith(status: 400);
            await Assert.ThrowsAsync<LoginException>( _unifiClient.Login);
        }
        
        [Fact]
        public async Task Login_Should_Throw_ClientTimeOutException_On_Timeout()
        {
            using var httpTest = new HttpTest();
            ConfigureHttpTest(httpTest);
            httpTest.SimulateTimeout();
            await Assert.ThrowsAsync<ClientTimoutException>( _unifiClient.Login);
        }
        [Fact]
        public async Task Get_Tokens_Should_Cache_Tokens()
        {
            using var httpTest = new HttpTest();
            ConfigureHttpTest(httpTest);
            AddLoginSuccessCall(httpTest);
            await _unifiClient.GetTokens();
            await _unifiClient.GetTokens();
            //Forward the test clock to two minutes after the token expires to see that login is called again
            _testClock.UtcNow = new DateTimeOffset(2021, 10, 11, 15, 35, 0, 0, TimeSpan.Zero);
            await _unifiClient.GetTokens();
            var expectedRequest = await File.ReadAllTextAsync(Path.Combine(RequestFolder, "Login.json"), TestContext.Current.CancellationToken);
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
            ConfigureHttpTest(httpTest);
            AddLoginSuccessCall(httpTest);
            var portForwardResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "GetCurrentPortForward.json"), TestContext.Current.CancellationToken); 
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
            ConfigureHttpTest(httpTest);
            AddLoginSuccessCall(httpTest);
            var deletePortForwardResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "DeletePortForwardInvalidId.json"), TestContext.Current.CancellationToken); 
            httpTest
                .RespondWith(body: deletePortForwardResponse, status: 400);
            await Assert.ThrowsAsync<IdInvalidException>(()=> _unifiClient.DeletePortForwardSetting("60478d7f8e188e04d2ff3e8a"));
        }
        [Fact]
        public async Task Delete_Port_Forward_With_Valid_Id_Should_Succeed()
        {
            using var httpTest = new HttpTest();
            ConfigureHttpTest(httpTest);
            AddLoginSuccessCall(httpTest);
            var deletePortForwardResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "DeletePortForwardSuccess.json"), TestContext.Current.CancellationToken); 
            httpTest
                .RespondWith(deletePortForwardResponse);
            await _unifiClient.DeletePortForwardSetting("60478d7f8e188e04d2ff3e8e");
        }
        [Fact]
        public async Task Create_PortForward_Should_Return_PortForward()
        {
            using var httpTest = new HttpTest();
            ConfigureHttpTest(httpTest);
            AddLoginSuccessCall(httpTest);
            var createPortForwardResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "CreatePortForward.json"), TestContext.Current.CancellationToken); 
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
            var expectedRequest = await File.ReadAllTextAsync(Path.Combine(RequestFolder, "CreatePortForward.json"), TestContext.Current.CancellationToken);
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
            ConfigureHttpTest(httpTest);
            AddLoginSuccessCall(httpTest);
            var getByIdResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "GetById.json"), TestContext.Current.CancellationToken); 
            httpTest
                .RespondWith(getByIdResponse);
            var id = "6156a2368e188e7795ff6399";
            var portForwardSetting = await _unifiClient.GetPortForwardById(id);
            var tokens = await _unifiClient.GetTokens();
            httpTest.ShouldHaveCalled($"{_options.BaseUrl}/proxy/network/api/s/default/rest/portforward/{id}")
                .WithCookie("TOKEN", tokens.JwtToken);
            Assert.Equal(id, portForwardSetting.Id);
            
        }
        [Fact]
        public async Task Enable_PortForward_Should_Succeed()
        {
            using var httpTest = new HttpTest();
            ConfigureHttpTest(httpTest);
            AddLoginSuccessCall(httpTest);
            var createPortForwardResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "EnablePortForward.json"), TestContext.Current.CancellationToken); 
            httpTest
                .RespondWith(createPortForwardResponse);
            var portForward = new PortForwardForm
            {
                Enabled = true
            };
            var id = "6156a2368e188e7795ff6399";
            await _unifiClient.EditPortForwardSetting(id, portForward);
            var tokens = await _unifiClient.GetTokens();
            var expectedRequest = await File.ReadAllTextAsync(Path.Combine(RequestFolder, "EnablePortForward.json"), TestContext.Current.CancellationToken);
            httpTest.ShouldHaveCalled($"{_options.BaseUrl}/proxy/network/api/s/default/rest/portforward/{id}")
                .WithVerb(HttpMethod.Put)
                .WithContentType("application/json")
                .WithHeader("X-CSRF-Token",tokens.CsrfToken)
                .WithCookie("TOKEN", tokens.JwtToken)
                .WithRequestBody(expectedRequest);
        }
        
        [Fact]
        public async Task Get_Port_Forward_With_Wan_Ip_Should_Parse_All_Fields()
        {
            using var httpTest = new HttpTest();
            ConfigureHttpTest(httpTest);
            AddLoginSuccessCall(httpTest);
            var portForwardResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "GetPortForwardWithWanIp.json"), TestContext.Current.CancellationToken); 
            httpTest
                .RespondWith(portForwardResponse);
            var portForwardSettings = await _unifiClient.GetPortForwardSettings();
            Assert.Single(portForwardSettings);
            Assert.Equal("203.0.113.10", portForwardSettings[0].Source);
            Assert.Equal("198.51.100.50", portForwardSettings[0].DestinationIp);
            Assert.NotNull(portForwardSettings[0].DestinationIps);
            Assert.Empty(portForwardSettings[0].DestinationIps);
            Assert.Equal("ip", portForwardSettings[0].SourceLimitingType);
            Assert.True(portForwardSettings[0].SourceLimitingEnabled);
        }
        
        [Fact]
        public async Task Create_PortForward_With_Wan_Ip_Should_Include_All_Fields()
        {
            using var httpTest = new HttpTest();
            ConfigureHttpTest(httpTest);
            AddLoginSuccessCall(httpTest);
            var createPortForwardResponse =
                await File.ReadAllTextAsync(Path.Combine(ResponseFolder, "GetPortForwardWithWanIp.json"), TestContext.Current.CancellationToken); 
            httpTest
                .RespondWith(createPortForwardResponse);
            var portForward = new PortForwardForm
            {
                Name = "Test User",
                Enabled = true,
                Source = "203.0.113.10",
                DestinationPort = "3391",
                Forward = "192.168.1.100",
                ForwardPort = "3389",
                Protocol = "tcp",
                Log = false,
                DestinationIp = "198.51.100.50",
                DestinationIps = [],
                SourceLimitingType = "ip",
                SourceLimitingEnabled = true
            };
            var portForwardSetting = await _unifiClient.CreatePortForwardSetting(portForward);
            var tokens = await _unifiClient.GetTokens();
            var expectedRequest = await File.ReadAllTextAsync(Path.Combine(RequestFolder, "CreatePortForwardWithWanIp.json"), TestContext.Current.CancellationToken);
            httpTest.ShouldHaveCalled($"{_options.BaseUrl}/proxy/network/api/s/default/rest/portforward")
                .WithContentType("application/json")
                .WithHeader("X-CSRF-Token",tokens.CsrfToken)
                .WithCookie("TOKEN", tokens.JwtToken)
                .WithRequestBody(expectedRequest);
            Assert.Equal("68aeaf5b4abd6665bac3a6f3", portForwardSetting.Id);
            Assert.Equal("198.51.100.50", portForwardSetting.DestinationIp);
        }
    }
}
