using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Paygate.Api.Endpoints;
using Paygate.Api.Telemetry;
using Paygate.Data;
using Paygate.Data.Postgres;
using Pay.Message.Exchange.OutboxClient;
using Pay.Message.Exchange.OutboxClient.Postgres;
using Pay.Message.Exchange.OutboxPublisher.Db;

// Payment repo maps snake_case columns (payment_id) onto a PascalCase entity via
// SELECT */RETURNING *. Without this, multi-word columns silently map to default
// (e.g. PaymentId => Guid.Empty). Outbox queries are unaffected — they use AS aliases.
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// Created before the builder so Serilog and DI share the same instance
var telemetryStore = new TelemetryStore();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.Sink(new TelemetrySink(telemetryStore, "Paygate.Api"))
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddOpenApi();
    builder.Services.AddSingleton(telemetryStore);
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                  "http://localhost:5173",   // paygate.admin (Signals panel)
                  "http://localhost:3000")   // paygate.web (payments UI)
              .AllowAnyHeader()
              .AllowAnyMethod()));

    // ── Authentication: validate JWT access tokens issued by Paygate.IdentityServer ──
    var authority = builder.Configuration["IdentityServer:Authority"] ?? "http://localhost:5001";
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authority;          // OIDC metadata + signing keys fetched from here
            options.RequireHttpsMetadata = false;   // local issuer is plain http
            options.MapInboundClaims = false;       // keep raw "sub"/"role"/"scope" claim types
            options.TokenValidationParameters.ValidAudience = "paygate.api";
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = "role";
        });

    // ── Authorization: every policy requires a SCOPE (what the client app may do) AND
    //    a ROLE (what the user may do). Demonstrates the two OAuth layers enforced together. ──
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("CanCreatePayment", p => p
            .RequireAuthenticatedUser().RequireClaim("scope", "payments.write").RequireRole("PaymentInitiator"));
        options.AddPolicy("CanDecidePayment", p => p
            .RequireAuthenticatedUser().RequireClaim("scope", "payments.approve").RequireRole("PaymentApprover"));
        options.AddPolicy("CanReadPayment", p => p
            .RequireAuthenticatedUser().RequireClaim("scope", "payments.read")
            .RequireRole("Auditor", "PaymentInitiator", "PaymentApprover"));
    });

    builder.Services.UsePostgresPayment();
    builder.Services.AddPaymentServices();

    // Binds OutboxDatabase:TableName — drives IOutboxTableNames (e.g. outbox_message_registry).
    // Without this the base name is empty and queries hit "_message_registry".
    builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("OutboxDatabase"));

    builder.Services.UsePostgresOutboxClient();
    builder.Services.AddOutboxClient();

    var app = builder.Build();

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.MapPaymentEndpoints();
    app.MapCustomerEndpoints();
    app.MapMerchantEndpoints();
    app.MapTelemetryEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
