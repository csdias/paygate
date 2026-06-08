# Runs the consumer poller on the host. It long-polls both SQS queues and invokes
# the real Lambda handlers (Paygate.Notification, Paygate.Audit) — the same code AWS
# would run, but in a process you can attach a debugger to.
#
# DB_CONNECTION_STRING is what the Audit Lambda reads to write payment_audit rows.
# Region is us-east-1 to match the queues created in LocalStack.
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

$env:DB_CONNECTION_STRING = "Host=localhost;Port=5432;Database=paygate;Username=paygate;Password=paygate"

# Forward consumer logs to the Signals panel (paygate.admin). Remove to disable.
$env:TELEMETRY_INGEST_URL = "http://localhost:5000/telemetry/ingest"

$env:AWS_ENDPOINT_URL = "http://localhost:4566"
$env:AWS_ACCESS_KEY_ID = "test"
$env:AWS_SECRET_ACCESS_KEY = "test"
$env:AWS_REGION = "us-east-1"
$env:AWS_DEFAULT_REGION = "us-east-1"

dotnet run --project "$repo\paygate.consumers\Paygate.Consumers.LocalRunner" --no-launch-profile
