# Runs the OutboxPublisher on the host. It polls the outbox table, publishes
# unpublished rows to the SNS topic (in LocalStack), and marks them published.
#
# AWS__ServiceURL / AWS__Region populate the "AWS" config section that the SDK's
# GetAWSOptions() reads (wired via AddDefaultAWSOptions in Program.cs) — this is
# what points the SNS client at LocalStack. Region is us-east-1 locally because the
# SDK signs as us-east-1 whenever a ServiceURL is set (see localstack-init).
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

$env:DOTNET_ENVIRONMENT = "Development"
$env:ConnectionStrings__local = "Host=localhost;Port=5432;Database=paygate;Username=paygate;Password=paygate"
$env:OutboxDatabase__TableName = "outbox"
$env:OutboxDatabase__BatchSize = "10"
$env:OutboxDatabase__InactiveDelay = "00:00:30"

# Forward publisher logs to the Signals panel (paygate.admin). Remove to disable.
$env:TELEMETRY_INGEST_URL = "http://localhost:5000/telemetry/ingest"

$env:AWS__ServiceURL = "http://localhost:4566"
$env:AWS__Region = "us-east-1"
$env:AWS_ACCESS_KEY_ID = "test"
$env:AWS_SECRET_ACCESS_KEY = "test"

dotnet run --project "$repo\paygate.message.exchange\OutboxPublisher\Pay.Message.Exchange.OutboxPublisher" --no-launch-profile
