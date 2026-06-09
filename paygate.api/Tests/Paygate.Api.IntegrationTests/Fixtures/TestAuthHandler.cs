using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Paygate.Api.IntegrationTests.Fixtures;

// Replaces JWT bearer in integration tests. Builds a principal from request headers so each
// test can act as a chosen persona without standing up the real IdentityServer:
//   X-Test-Sub    → the `sub` actor id (drives created_by / maker-checker)
//   X-Test-Role   → comma-separated role claims
//   X-Test-Scope  → space-separated scope claims
// No X-Test-Sub header → NoResult → the request is treated as unauthenticated (401),
// which lets us test the anonymous case too.
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Sub", out var sub) || string.IsNullOrEmpty(sub))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new("sub", sub!) };

        if (Request.Headers.TryGetValue("X-Test-Role", out var roles))
            claims.AddRange(roles.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(r => new Claim("role", r)));

        if (Request.Headers.TryGetValue("X-Test-Scope", out var scopes))
            claims.AddRange(scopes.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => new Claim("scope", s)));

        // roleType "role" so RequireRole matches; nameType "name" mirrors the real config.
        var identity = new ClaimsIdentity(claims, SchemeName, nameType: "name", roleType: "role");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
