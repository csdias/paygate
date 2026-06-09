# Runs the Paygate IdentityServer (Duende, in-memory config + test users) on the host.
#
# Start this BEFORE the API: the API validates JWT access tokens against this server's
# OIDC discovery document (http://localhost:5001/.well-known/openid-configuration).
#
# Test users (all password "Pass123$"):
#   clerk    -> PaymentInitiator  (create payments)
#   approver -> PaymentApprover   (approve/reject — must not be the creator)
#   auditor  -> Auditor           (read-only)
#
# --no-launch-profile so Properties/launchSettings.json doesn't override ASPNETCORE_URLS.
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5001"   # issuer the API trusts

dotnet run --project "$repo\paygate.identity\Paygate.IdentityServer" --no-launch-profile
