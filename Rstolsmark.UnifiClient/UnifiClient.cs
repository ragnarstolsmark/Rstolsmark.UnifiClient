using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Flurl.Http;
using System.Linq;
using Flurl.Http.Configuration;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Rstolsmark.UnifiClient
{
    public class UnifiClient
    {
        private IMemoryCache _cache;
        private Credentials _credentials;
        private string _baseUrl;
        private const string CredentialsCacheKey = "unifiCredentials";
        private const string TokenCookieName = "TOKEN";
        private const string CsrfTokenHeaderName = "X-CSRF-Token";
        public UnifiClient(IMemoryCache cache, UnifiClientOptions options)
        {
            _cache = cache;
            _credentials = options.Credentials;
            _baseUrl = options.BaseUrl;
                FlurlHttp.ConfigureClient(_baseUrl, cli =>
                {
                    if (options.AllowInvalidCertificate)
                    {
                        cli.Settings.HttpClientFactory = new UntrustedCertClientFactory();
                    }

                    var jsonSettings = new JsonSerializerSettings()
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                        NullValueHandling = NullValueHandling.Ignore
                    };
                    cli.Settings.JsonSerializer = new NewtonsoftJsonSerializer(jsonSettings);
                });
        }
        public async Task<Tokens> GetTokens()
        {
            if(_cache.TryGetValue(CredentialsCacheKey, out Tokens tokens))
            {
                return tokens;
            }

            return await Login();
        }

        public async Task<Tokens> Login()
        {
            try
            {
                var loginResponse = await $"{_baseUrl}/api/auth/login"
                    .PostJsonAsync(_credentials);
                var tokenCookie = loginResponse.Cookies[0];
                var jwtTokenEncoded = tokenCookie.Value;
                var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(jwtTokenEncoded);
                var csrfToken = jwtToken.Claims.First(x => x.Type == "csrfToken").Value;
                var credentials = new Tokens(jwtToken: jwtTokenEncoded, csrfToken: csrfToken);
                //subtract 10 minutes to allow for clock skew
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(jwtToken.ValidTo.AddMinutes(-10));
                _cache.Set(CredentialsCacheKey, credentials, cacheEntryOptions);
                //jwtToken.ValidTo
                return credentials;
            }
            catch (FlurlHttpException flurlException)
            {
                throw new LoginException(flurlException);
            }
        }

        public async Task<List<PortForward>> GetPortForwardSettings()
        {
            var tokens = await GetTokens();
            var portForwardResponse = await $"{_baseUrl}/proxy/network/api/s/default/rest/portforward"
                .WithCookie(TokenCookieName, tokens.JwtToken)
                .GetJsonAsync<PortForwardResponse>();
            return portForwardResponse.Data;
        }

        public async Task DeletePortForwardSetting(string id)
        {
            async Task<bool> IsIdInvalidResponse(FlurlHttpException fex)
            {
                if (fex.StatusCode != 400)
                {
                    return false;
                }

                var response = await fex.GetResponseJsonAsync();
                try
                {
                    bool isIdInvalid = response.meta.msg.Equals("api.err.IdInvalid");
                    return isIdInvalid;
                }
                catch (RuntimeBinderException)
                {
                    return false;
                }
            }
            var tokens = await GetTokens();
            try
            {
                var _ = await $"{_baseUrl}/proxy/network/api/s/default/rest/portforward/{id}"
                    .WithCookie(TokenCookieName, tokens.JwtToken)
                    .WithHeader(CsrfTokenHeaderName, tokens.CsrfToken)
                    .DeleteAsync();
            }
            catch (FlurlHttpException fex)
            {
                if (await IsIdInvalidResponse(fex))
                {
                    throw new IdInvalidException(id, fex);
                }
                throw;
            }
        }

        public async Task<PortForward> CreatePortForwardSetting(PortForward portForward)
        {
            var tokens = await GetTokens();
            var response = await $"{_baseUrl}/proxy/network/api/s/default/rest/portforward"
                .WithCookie(TokenCookieName, tokens.JwtToken)
                .WithHeader(CsrfTokenHeaderName, tokens.CsrfToken)
                .PostJsonAsync(portForward);
            var portForwardResponse = await response.GetJsonAsync<PortForwardResponse>(); 
            return portForwardResponse.Data.Single();
        }
    }

    public class IdInvalidException : Exception
    {
        public IdInvalidException(string id, Exception innerException) : base($"Deletion failed, id: {id} is invalid.")
        {
        }
    }

    public class LoginException : Exception
    {
        public LoginException(Exception innerException) : base("Login failed. See inner exception for details",innerException)
        {
            
        }
    }
}
