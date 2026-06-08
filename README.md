# Paygate

> **A study project.** Paygate is a deliberately small, fake payment system built
> to learn distributed-systems concepts hands-on — not a production product.
> Clarity is favoured over pragmatism throughout.

## What it's for

Paygate is a sandbox for exploring how independent services coordinate around a
database and a message broker, and how you observe what happens across them:

- **Transactional outbox** — write a payment and its event in one transaction,
  publish asynchronously (at-least-once delivery, no dual-write problem).
- **Messaging** — SNS topic → SQS queues → consumers, with dead-letter queues
  and partial batch failure.
- **Distributed tracing** — a W3C `traceparent` propagated from the API through
  the outbox, SNS, SQS, and into each consumer, surfaced in an admin panel.

## Components

| Folder                     | What it is                                                                    |
| -------------------------- | ----------------------------------------------------------------------------- |
| `paygate.api`              | ASP.NET payment API; writes payments + outbox rows                            |
| `paygate.message.exchange` | Outbox client library + `OutboxPublisher` worker (polls outbox → SNS)         |
| `paygate.consumers`        | SQS consumers (`Paygate.Audit`, `Paygate.Notification`) + a local poller host |
| `paygate.web`              | Customer-facing payments UI (React + RTK Query)                               |
| `paygate.admin`            | "Baby Datadog" — a Signals panel showing live logs + trace correlation        |
| `paygate.localdev`         | LocalStack + Postgres dev environment and run scripts                         |
| `paygate.terraform`        | Infrastructure-as-code for the AWS topology                                   |

## How to run

**Prerequisites:** Docker Desktop, .NET 10 SDK, Node + Yarn.

The infrastructure (Postgres + emulated SNS/SQS) runs in Docker; the .NET
services run on the host so you can attach a debugger. Start them in this order
— each .NET service in its own terminal.

```powershell
# 1. Infrastructure — Postgres + LocalStack (creates topic/queues, seeds the DB)
cd paygate.localdev
docker compose up -d

# 2. Backend (one terminal each)
.\run-api.ps1          # payment API
.\run-publisher.ps1    # outbox → SNS publisher
.\run-consumers.ps1    # SQS → audit/notification consumers

# 3. Frontends (one terminal each)
cd ..\paygate.web   ; yarn install ; yarn dev    # payments UI
cd ..\paygate.admin ; yarn install ; yarn dev    # Signals panel
```

Then create a payment in the web UI (or `POST http://localhost:5000/payments`)
and watch it flow through to both consumers — correlated by a single trace id —
in the admin Signals panel.

### Ports

| Service              | Start from                           | URL                   |
| -------------------- | ------------------------------------ | --------------------- |
| API                  | `paygate.localdev\run-api.ps1`       | http://localhost:5000 |
| OutboxPublisher      | `paygate.localdev\run-publisher.ps1` | — (worker)            |
| Consumers            | `paygate.localdev\run-consumers.ps1` | — (worker)            |
| Web — payments UI    | `yarn dev` in `paygate.web`          | http://localhost:3000 |
| Admin — Signals      | `yarn dev` in `paygate.admin`        | http://localhost:5173 |
| Postgres             | Docker                               | localhost:5432        |
| LocalStack (SNS/SQS) | Docker                               | localhost:4566        |

For the full walkthrough — how the infra is wired, the trace propagation, and
how the Lambdas are run locally — see [`paygate.localdev/README.md`](paygate.localdev/README.md).

## How to debug

Running the .NET services on the **host** (rather than in Docker) is what makes
debugging easy — they're ordinary local processes, so set a breakpoint and go.
The `run-*.ps1` scripts are just "set environment variables, then `dotnet run`",
so for debugging your IDE only needs the **same environment** each script sets.

Two approaches:

1. **Launch from the IDE (F5)** — start the project from your editor with a
   debug config that sets the env vars from the matching `run-*.ps1`. Breakpoints
   work from the first line.
2. **Attach to a running process** — run the `.ps1` as usual, then attach the
   debugger to the `Paygate.Api` / publisher / consumer process.

Infrastructure stays in Docker either way — you don't debug Postgres/LocalStack,
you just connect to them on `localhost`.

> Note: the scripts pass `--no-launch-profile` so `launchSettings.json` can't
> override the port/env. For IDE debugging, use a launch profile that sets the
> right URL + env vars instead.

Useful breakpoints:

- **API** — `PaymentEndpoints.CreatePayment`, `PaymentService.CreateAsync`, `PostgresOutboxWriter`
- **Publisher** — `PostgresOutbox.ReserveAndFetchUnpublishedMessagesAsync`, `SnsMessagePublisher.PublishMessages`
- **Consumers** — `Paygate.Audit/Function.cs` and `Paygate.Notification/Function.cs`. The
  local poller invokes the **real** handler, so these are the exact methods AWS runs.
- **Frontends** — browser DevTools / React DevTools; Vite ships source maps, so you
  breakpoint your `.tsx` directly.

## Where it's going

Next learning increment: running Paygate on **Kubernetes** — containerizing the
services, plain manifests (Deployments, StatefulSet, Ingress), and **KEDA** for
queue-driven autoscaling of the consumers (the Kubernetes analogue of an SQS
Lambda event-source mapping).
