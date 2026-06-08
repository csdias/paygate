# Paygate — local development environment

The AWS analogue of Lernova's `docker-compose`. It runs the two pieces of
infrastructure the system needs — **Postgres** and **LocalStack** (emulated
SNS + SQS) — in containers, while you run the .NET services on the **host** so
you can attach a debugger and set breakpoints.

```
  POST /payments
        │
        ▼
   Paygate.Api ──(payment + outbox row, one transaction)──▶  Postgres
                                                              │
                          OutboxPublisher ──(polls outbox)────┘
                              │  publishes to
                              ▼
                       SNS  paygate-local-payment-events          (LocalStack)
                              │
                ┌─────────────┴─────────────┐
                ▼                           ▼
        SQS paygate-local-audit       SQS paygate-local-notification
                │                           │
        Audit handler                Notification handler        (Paygate.Consumers.LocalRunner)
        → payment_audit              → logs the dispatch
```

The W3C `traceparent` rides the whole way (DB column → SNS message attribute →
restored in each handler), so one trace id ties the API, publisher, and both
consumers together.

## Prerequisites

- Docker Desktop running
- .NET 10 SDK

## 1. Start the infrastructure

```powershell
cd paygate.localdev
docker compose up -d
```

This starts:

| Container  | Purpose                         | Port |
| ---------- | ------------------------------- | ---- |
| postgres   | payment / outbox / audit tables | 5432 |
| localstack | SNS + SQS                       | 4566 |

On first start, Postgres runs the **real** schema files from each project
(bind-mounted — see `docker-compose.yml`) plus `seed-registry.sql`, and
LocalStack runs `localstack-init/01-bootstrap.sh` to create the topic, the two
queues + DLQs, and the subscriptions — mirroring
`paygate.terraform/modules/messaging`.

Verify:

```powershell
docker exec paygatelocaldev-localstack-1 awslocal sqs list-queues --region us-east-1
docker exec paygatelocaldev-postgres-1 psql -U paygate -d paygate -c "\dt"
```

> **Region note:** locally everything uses **us-east-1**. When the AWS SDK is
> pointed at a `ServiceURL` (LocalStack) it signs as us-east-1 regardless of
> region config, so the emulated topology lives there too. Production uses
> eu-west-1 (the Terraform default); only the region label differs.

## 2. Run the services (each in its own terminal)

```powershell
.\run-api.ps1          # http://localhost:5000
.\run-publisher.ps1    # polls the outbox, publishes to SNS
.\run-consumers.ps1    # polls both SQS queues, invokes the Lambda handlers
```

Each script sets the environment it needs and runs the project with
`dotnet run --no-launch-profile`. To debug instead, launch the same project from
your IDE with the same environment variables (see the script for the list).

## 3. Exercise the chain

```powershell
$body = @{ amount = 275.50; currency = "EUR"
           customerId = [guid]::NewGuid().ToString()
           merchantId = [guid]::NewGuid().ToString()
           reference = "DEMO" } | ConvertTo-Json
$p = Invoke-RestMethod http://localhost:5000/payments -Method Post -Body $body -ContentType application/json

# outbox row published?
docker exec paygatelocaldev-postgres-1 psql -U paygate -d paygate -c `
  "SELECT published_at IS NOT NULL AS published FROM outbox WHERE context_id='$($p.paymentId)';"

# audit Lambda wrote the row?
docker exec paygatelocaldev-postgres-1 psql -U paygate -d paygate -c `
  "SELECT payment_id, event_type FROM payment_audit WHERE payment_id='$($p.paymentId)';"
```

The notification handler's output (including the trace id) appears in the
`run-consumers.ps1` terminal.

## Watch the trace flow in the Signals panel

All three .NET services forward their logs to the API's `/telemetry/ingest`
endpoint, so they show up together in the **paygate.admin** Signals panel,
correlated by trace id:

- the API logs on its own pipeline (the request `Activity`),
- the OutboxPublisher via a Serilog `IngestSink`,
- the consumers via `TelemetryForwarder` (wrapping the Lambda logger).

Forwarding is enabled by the `TELEMETRY_INGEST_URL` env var, already set in
`run-publisher.ps1` and `run-consumers.ps1` (remove it to disable). It's
best-effort and batched — if the API isn't running, entries are dropped and
processing is unaffected.

Start the panel:

```powershell
cd ..\paygate.admin
yarn dev            # http://localhost:5173 (Vite proxies /telemetry → :5000)
```

POST a payment, then open **Signals** and click the trace id on any row. One
trace ties the whole flow together, oldest-first:

```
Paygate.Api           Payment <id> initiated: 777.00 EUR
OutboxPublisher     Received message …, publishing
OutboxPublisher     Published message …, marking as published
Paygate.Notification  Dispatching / [NOTIFY] / dispatched
Paygate.Audit         Audit record written
```

This is the same `traceparent` propagated from the API request → outbox column
→ SNS message attribute → restored in each consumer.

## How the Lambdas run locally

In AWS, an SQS **event source mapping** invokes each Lambda. Locally,
`Paygate.Consumers.LocalRunner` plays that role: it long-polls each queue, builds
the same `SQSEvent`, and calls the **real** `Function.Handler` — so you debug the
exact code AWS runs. Partial batch failure is honoured: a record the handler
reports as failed is left on the queue (redelivered after the 30s visibility
timeout, then DLQ'd after 3 attempts), just like AWS.

## Tear down

```powershell
docker compose down        # stop containers (data is ephemeral anyway)
```

Stop the three .NET processes with Ctrl+C in their terminals.

## Notes / things this surfaced

Getting the chain to run end-to-end required fixing several latent issues where
the messaging seam had never actually been exercised (the integration tests
mock the outbox). They're committed alongside this environment:

- outbox message column reconciled to `message` (API writer vs. publisher reader)
- canonical `outbox_*`-prefixed schema (matches `IOutboxTableNames`)
- `IMessageRegistryRepository` DI registration; `DatabaseOptions` binding in the API
- Dapper `MatchNamesWithUnderscores` (snake_case columns → PascalCase entities)
- AutoMapper `Metadata` resolver made instantiable
- AWS SDK pointed at LocalStack via `AddDefaultAWSOptions` + `AWS__ServiceURL`
- `Unwrap<T>` reads the `Payload` of the published wrapper
