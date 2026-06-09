using Paygate.IdentityServer;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages host the Quickstart login/logout/consent UI (added via `dotnet new isui`),
// which the SPA Authorization Code + PKCE flow redirects to.
builder.Services.AddRazorPages();

// Duende IdentityServer with everything in memory (study setup — no database).
// AddTestUsers also wires up the resource-owner-password validator, which the
// verification-only `paygate.test` client uses to mint tokens from curl.
builder.Services.AddIdentityServer(options =>
    {
        // Surface useful errors in the OIDC response during local study.
        options.Events.RaiseErrorEvents = true;
        options.Events.RaiseInformationEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseSuccessEvents = true;

        // Local dev runs over plain HTTP, so SameSite=None cookies (the default for the
        // session cookie) get rejected by browsers for lacking Secure — which silently
        // breaks login. Lax works for the redirect-based code flow we use.
        options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
        options.Authentication.CheckSessionCookieSameSiteMode = SameSiteMode.Lax;
    })
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryApiResources(Config.ApiResources)
    .AddInMemoryClients(Config.Clients)
    .AddTestUsers(TestUsers.Users);

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseIdentityServer();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
