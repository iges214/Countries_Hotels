using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NoName_Hotel.api.Constants;
using NoName_Hotel.api.Services.AuthServices;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace NoName_Hotel.api.Handlers
{
    public class ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApiKeyValidatorService apiKeyValidatorService
        ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string apikey = string.Empty;
            if (Request.Headers.TryGetValue(AuthenticationDefault.ApiKeyHeaderName, out var headervalues))
            {
                apikey = headervalues.ToString();
            }

            if (string.IsNullOrWhiteSpace(apikey))
            {
                return AuthenticateResult.NoResult();
            }

            var valid = await apiKeyValidatorService.IaValidAsync(apikey, Context.RequestAborted);
            if (!valid)
            {
                return AuthenticateResult.Fail("Invlid Api key");
            }
            var claims = new List<Claim>
                {
                new(ClaimTypes.NameIdentifier, "apiKey"),
                new(ClaimTypes.Name, "ApiKeyClient")
                };


            var Identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(Identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }
}
