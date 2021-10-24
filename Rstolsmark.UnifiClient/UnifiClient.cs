using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Flurl.Http;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace Rstolsmark.UnifiClient
{
    public class UnifiClient
    {
        private IMemoryCache _cache;
        private Credentials _credentials;
        private string _baseUrl;

        public UnifiClient(IMemoryCache cache, UnifiClientOptions options)
        {
            _cache = cache;
            _credentials = options.Credentials;
            _baseUrl = options.BaseUrl;
            if (options.AllowInvalidCertificate)
            {
                FlurlHttp.ConfigureClient(_baseUrl, cli =>
                {
                    cli.Settings.HttpClientFactory = new UntrustedCertClientFactory();
                });
            }
        }
        public async Task<bool> Login()
        {
            try
            {
                var test = await _baseUrl
                    .GetAsync();
                var tokenCookie = test.Cookies[0];
                var jwtTokenEncoded = tokenCookie.Value;
                var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(jwtTokenEncoded);
                var csrfToken = jwtToken.Claims.First(x => x.Type == "csrfToken").Value;
                var credentials = new Tokens(jwtToken: jwtTokenEncoded, csrfToken: csrfToken);
                //subtract 10 minutes to allow for clock skew
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(jwtToken.ValidTo.AddMinutes(-10));
                _cache.Set("credentials", credentials, cacheEntryOptions);
                //jwtToken.ValidTo
                return true;
            }
            catch (FlurlHttpException)
            {
                return false;
            }
        }
    }
}
