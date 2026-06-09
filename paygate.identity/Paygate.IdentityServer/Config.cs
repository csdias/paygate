using Duende.IdentityServer.Models;

namespace Paygate.IdentityServer;

// In-memory IdentityServer configuration for the Paygate study project.
// Mirrors the IS4 quickstart style: everything is declared here, no database.
//
// Two layers, deliberately kept distinct so the OAuth lesson is visible:
//   - ApiScopes  = what a CLIENT (a React app) is allowed to request   ("capability")
//   - the role claim = what the USER is allowed to do                  ("permission")
// The API enforces BOTH together (see Paygate.Api authorization policies).
public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        // Custom resource so the user's "role" claim can be requested and lands in tokens.
        new IdentityResource("role", "Your role(s)", ["role"]),
    ];

    // Granular scopes — one per action — so clients ask only for what they need.
    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope("payments.read", "Read payments"),
        new ApiScope("payments.write", "Create payments"),
        new ApiScope("payments.approve", "Approve / reject payments"),
    ];

    // The protected API. Bundling the scopes under a named resource makes the
    // access token's `aud` claim equal to "paygate.api", which the API validates.
    public static IEnumerable<ApiResource> ApiResources =>
    [
        new ApiResource("paygate.api", "Paygate API")
        {
            Scopes = { "payments.read", "payments.write", "payments.approve" },
            UserClaims = { "role" },   // copy the role claim into access tokens for this API
        },
    ];

    public static IEnumerable<Client> Clients =>
    [
        // paygate.web — the payment-initiator SPA (public client, Authorization Code + PKCE).
        new Client
        {
            ClientId = "paygate.web",
            ClientName = "Paygate Web (payments)",
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequireConsent = false,
            RequirePkce = true,
            // Put identity claims (incl. role) in the id token so the SPA's user.profile
            // has them without a separate userinfo round-trip.
            AlwaysIncludeUserClaimsInIdToken = true,
            RedirectUris = { "http://localhost:3000/callback" },
            PostLogoutRedirectUris = { "http://localhost:3000" },
            AllowedCorsOrigins = { "http://localhost:3000" },
            AllowedScopes = { "openid", "profile", "role", "payments.read", "payments.write" },
        },

        // paygate.admin — the backoffice approve/reject SPA (public client, Code + PKCE).
        new Client
        {
            ClientId = "paygate.admin",
            ClientName = "Paygate Admin (backoffice)",
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequireConsent = false,
            RequirePkce = true,
            AlwaysIncludeUserClaimsInIdToken = true,
            RedirectUris = { "http://localhost:5173/callback" },
            PostLogoutRedirectUris = { "http://localhost:5173" },
            AllowedCorsOrigins = { "http://localhost:5173" },
            AllowedScopes = { "openid", "profile", "role", "payments.read", "payments.approve" },
        },

        // VERIFICATION ONLY — Resource Owner Password so we can fetch tokens from curl
        // without a login UI during this backend-first pass. Remove (or disable) once the
        // React code+PKCE flow lands; ROP is not for production.
        new Client
        {
            ClientId = "paygate.test",
            ClientName = "Local token tester (ROP — verification only)",
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            ClientSecrets = { new Secret("test-secret".Sha256()) },
            AllowedScopes = { "openid", "profile", "role", "payments.read", "payments.write", "payments.approve" },
        },
    ];
}
