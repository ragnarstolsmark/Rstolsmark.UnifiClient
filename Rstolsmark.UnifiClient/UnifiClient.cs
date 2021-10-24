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
        public UnifiClient(IMemoryCache cache)
        {
            _cache = cache;
        }
        public async Task<bool> Login()
        {
            try
            {
                var test = await "https://www.vg.no"
                    .GetAsync();
                var tokenCookie = test.Cookies[0];
                var jwtTokenEncoded = tokenCookie.Value;
                var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(jwtTokenEncoded);
                var csrfToken = jwtToken.Claims.First(x => x.Type == "csrfToken").Value;
                var credentials = new UnifiCredentials(jwtToken: jwtTokenEncoded, csrfToken: csrfToken);
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
