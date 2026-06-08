# Runs the Paygate API on the host against the local Postgres (port 5432).
# The API only touches the database — it writes payments + outbox rows in one
# transaction. Publishing to SNS is the OutboxPublisher's job.
#
# --no-launch-profile so Properties/launchSettings.json doesn't override ASPNETCORE_URLS.
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5000"   # matches the paygate.admin Vite proxy

dotnet run --project "$repo\paygate.api\Paygate.Api" --no-launch-profile
