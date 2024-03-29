﻿using System;
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
        private string _defaultInterface;
        private const string CredentialsCacheKey = "unifiCredentials";
        private const string TokenCookieName = "TOKEN";
        private const string CsrfTokenHeaderName = "X-CSRF-Token";
        public UnifiClient(IMemoryCache cache, UnifiClientOptions options)
        {
            _cache = cache;
            _credentials = options.Credentials;
            _baseUrl = options.BaseUrl;
            _defaultInterface = options.DefaultInterface;
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
                    if (options.TimeoutSeconds != null)
                    {
                        cli.Settings.Timeout = TimeSpan.FromSeconds((double) options.TimeoutSeconds);
                    }
                });
        }
        public async Task<Tokens> GetTokens()
        {
            if(_cache.TryGetValue(CredentialsCacheKey, out Tokens tokens))
            {
                return tokens;
            }

            return await Login()
                .ConfigureAwait(false);
        }

        public async Task<Tokens> Login()
        {
            try
            {
                var loginResponse = await $"{_baseUrl}/api/auth/login"
                    .PostJsonAsync(_credentials)
                    .WithTimeoutHandling()
                    .ConfigureAwait(false);
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
        
        public async Task<PortForward> GetPortForwardById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new IdInvalidException();
            }
            var tokens = await GetTokens()
                .ConfigureAwait(false);
            try
            {
                var portForwardResponse = await $"{_baseUrl}/proxy/network/api/s/default/rest/portforward/{id}"
                    .WithCookie(TokenCookieName, tokens.JwtToken)
                    .GetJsonAsync<PortForwardListResponse>()
                    .WithTimeoutHandling()
                    .ConfigureAwait(false);
                return portForwardResponse.Data.SingleOrDefault();
            }
            catch (FlurlHttpException fex) when (fex.StatusCode == 404)
            {
                throw new IdInvalidException(id, fex);
            }
        }

        public async Task<List<PortForward>> GetPortForwardSettings()
        {
            var tokens = await GetTokens()
                .ConfigureAwait(false);
            var portForwardResponse = await $"{_baseUrl}/proxy/network/api/s/default/rest/portforward"
                .WithCookie(TokenCookieName, tokens.JwtToken)
                .GetJsonAsync<PortForwardListResponse>()
                .WithTimeoutHandling()
                .ConfigureAwait(false);
            return portForwardResponse.Data;
        }

        public async Task DeletePortForwardSetting(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new IdInvalidException();
            }
            var tokens = await GetTokens()
                .ConfigureAwait(false);
            try
            {
                var _ = await $"{_baseUrl}/proxy/network/api/s/default/rest/portforward/{id}"
                    .WithCookie(TokenCookieName, tokens.JwtToken)
                    .WithHeader(CsrfTokenHeaderName, tokens.CsrfToken)
                    .DeleteAsync()
                    .WithTimeoutHandling()
                    .ConfigureAwait(false);
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

        private async Task<bool> IsIdInvalidResponse(FlurlHttpException fex)
        {
            if (fex.StatusCode != 400)
            {
                return false;
            }

            var response = await fex.GetResponseJsonAsync()
                .ConfigureAwait(false);
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

        public async Task<PortForward> CreatePortForwardSetting(PortForwardForm portForward)
        {
            if (portForward.PortForwardInterface == null)
            {
                portForward.PortForwardInterface = _defaultInterface;
            }
            var tokens = await GetTokens()
                .ConfigureAwait(false);
            var response = await $"{_baseUrl}/proxy/network/api/s/default/rest/portforward"
                .WithCookie(TokenCookieName, tokens.JwtToken)
                .WithHeader(CsrfTokenHeaderName, tokens.CsrfToken)
                .PostJsonAsync(portForward)
                .WithTimeoutHandling()
                .ConfigureAwait(false);
            var portForwardResponse = await response.GetJsonAsync<PortForwardListResponse>()
                .ConfigureAwait(false); 
            return portForwardResponse.Data.Single();
        }

        public async Task EditPortForwardSetting(string id, PortForwardForm portForward)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new IdInvalidException();
            }
            var tokens = await GetTokens()
                .ConfigureAwait(false);
            try
            {
                var response = await $"{_baseUrl}/proxy/network/api/s/default/rest/portforward/{id}"
                    .WithCookie(TokenCookieName, tokens.JwtToken)
                    .WithHeader(CsrfTokenHeaderName, tokens.CsrfToken)
                    .PutJsonAsync(portForward)
                    .WithTimeoutHandling()
                    .ConfigureAwait(false);
            }catch (FlurlHttpException fex)
            {
                if (await IsIdInvalidResponse(fex))
                {
                    throw new IdInvalidException(id, fex);
                }
                throw;
            }
        }
    }

    public class IdInvalidException : Exception
    {
        public IdInvalidException() : base("Id is missing")
        {
            
        }
        public IdInvalidException(string id, Exception innerException) : base($"Request failed, id: {id} is invalid.", innerException)
        {
        }
    }

    public class LoginException : Exception
    {
        public LoginException(Exception innerException) : base("Login failed. See inner exception for details",innerException)
        {
            
        }
    }
    public class ClientTimoutException : Exception
    {
        public ClientTimoutException(Exception innerException) : base("Client timed out. See inner exception for details",innerException)
        {
            
        }
    }

    public static class FlurlExtensionMethods
    {
        public static async Task<T> WithTimeoutHandling<T>(this Task<T> response)
        {
            try
            {
                return await response
                    .ConfigureAwait(false);
            }
            catch (FlurlHttpTimeoutException fex)
            {
                throw new ClientTimoutException(fex);
            }
        }
    }
}
