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
.\run-identityserver.ps1   # http://localhost:5001  (OIDC issuer — start FIRST)
.\run-api.ps1              # http://localhost:5000
.\run-publisher.ps1        # polls the outbox, publishes to SNS
.\run-consumers.ps1        # polls both SQS queues, invokes the Lambda handlers
```

Each script sets the environment it needs and runs the project with
`dotnet run --no-launch-profile`. To debug instead, launch the same project from
your IDE with the same environment variables (see the script for the list).

> **Start order matters:** the API validates JWT access tokens against the
> IdentityServer's discovery doc, so run `run-identityserver.ps1` before
> `run-api.ps1`.

## Authentication & authorization

The payment endpoints are protected by OAuth2 scopes + OIDC role claims issued by
**Paygate.IdentityServer** (Duende, in-memory config). Two layers are enforced
together: a **scope** (what the client app may do) AND a **role** (what the user may do).

| Endpoint                     | Scope              | Role               |
| ---------------------------- | ------------------ | ------------------ |
| `POST /payments`             | `payments.write`   | `PaymentInitiator` |
| `POST /payments/{id}/approve`| `payments.approve` | `PaymentApprover`  |
| `POST /payments/{id}/reject` | `payments.approve` | `PaymentApprover`  |
| `GET  /payments`, `GET /payments/{id}` | `payments.read` | any of the three roles |

Test users (all password `Pass123$`): **clerk** (PaymentInitiator), **approver**
(PaymentApprover), **auditor** (Auditor, read-only).

**Maker-checker:** the approver of a payment must not be its creator (the token `sub`
is stored as `created_by`). Self-approval returns `403`.

> **Cookies over HTTP (`SameSite=Lax`):** local dev runs on plain HTTP, but Duende's
> session cookie defaults to `SameSite=None`, which browsers **drop** unless it's also
> `Secure`. Over HTTP that silently breaks login — the antiforgery cookie never sticks,
> so the credential POST is rejected before it's even checked (you'll see
> `The cookie 'idsrv' has set 'SameSite=None' and must also set 'Secure'` in the
> IdentityServer log, and no login event). The fix lives in
> `paygate.identity/Paygate.IdentityServer/Program.cs`:
>
> ```csharp
> options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
> options.Authentication.CheckSessionCookieSameSiteMode = SameSiteMode.Lax;
> ```
>
> `Lax` is correct for the redirect-based Authorization Code flow (the cookie is set and
> read entirely on `:5001`; only the final hop to the SPA carries the code in the URL).
> In production over HTTPS you'd leave the defaults and rely on `Secure`. If login ever
> misbehaves, **try a fresh incognito window** — a stale rejected-cookie state lingers.

## Play with the roles (try this)

The three users exist to make **authorization** visible — the same screen behaves
differently depending on who's signed in. Run the two SPAs (`paygate.web` on :3000,
`paygate.admin` on :5173) and log in as each user to see it. All passwords are `Pass123$`.

| Try this | Sign in as | Where | Expected |
| --- | --- | --- | --- |
| Create a payment | **clerk** | web → New Payment | Works (201). The "New Payment" button is visible. |
| Create a payment | **auditor** | web | Button **hidden**; visiting `/payments/new` shows "PaymentInitiator role required". |
| View the payment list | **auditor** | web or admin | Works — read-only roles can still read. |
| Approve a pending payment | **approver** | admin → Backoffice | Approve/Reject buttons present; approving a clerk-created payment → 200. |
| Approve a payment | **auditor** | admin → Backoffice | List is **read-only**; Approve/Reject replaced with `—`. |
| Approve a payment | **clerk** | (token via curl, see §3) | `403` — clerk has `payments.write`, not `payments.approve`. |

**Maker-checker (and why you can't trigger it with these users).** This is itself the
lesson: **clerk** can only create and **approver** can only decide, so a single person can
never both create *and* approve a payment — role separation makes self-approval structurally
impossible, and every pending payment is clerk-created. The `created_by == approver` check
in the service is a **defense-in-depth backstop** behind that role design. To watch the
backstop actually return `403`, you need a principal holding *both* roles, which no test
user has. Two ways to see it:
- Run the integration test `ApprovePayment_BySameUserWhoCreated_Returns403_MakerChecker`
  (it fabricates a dual-role identity) — see `paygate.api/Tests`.
- Or, as an experiment, add a fourth test user with **both** `PaymentInitiator` and
  `PaymentApprover` role claims in `Paygate.IdentityServer/TestUsers.cs`, log in as them,
  create a payment, then try to approve it → `403` maker-checker.

**Things to notice in the browser DevTools → Network tab:**
- every API call carries `Authorization: Bearer …`;
- decode the access token at <https://jwt.ms> to see the `scope` array and the `role` claim
  — the two independent layers the API checks together.

## 3. Exercise the chain

First get an access token. During this backend-first phase the IdentityServer exposes a
Resource-Owner-Password client (`paygate.test`) so you can mint tokens from the shell
without a login UI (this client is verification-only — it goes away once the React
code+PKCE flow lands):

```powershell
function Get-Token($user) {
  (Invoke-RestMethod http://localhost:5001/connect/token -Method Post -Body @{
     grant_type    = "password"
     username      = $user
     password      = "Pass123`$"
     client_id     = "paygate.test"
     client_secret = "test-secret"
     scope         = "payments.read payments.write payments.approve"
   }).access_token
}
$clerk    = Get-Token clerk
$approver = Get-Token approver

$body = @{ amount = 275.50; currency = "EUR"
           customerId = [guid]::NewGuid().ToString()
           merchantId = [guid]::NewGuid().ToString()
           reference = "DEMO" } | ConvertTo-Json
$p = Invoke-RestMethod http://localhost:5000/payments -Method Post -Body $body `
       -ContentType application/json -Headers @{ Authorization = "Bearer $clerk" }

# approve as a DIFFERENT user (maker-checker) — approving as $clerk would 403
Invoke-RestMethod "http://localhost:5000/payments/$($p.paymentId)/approve" -Method Post `
       -Headers @{ Authorization = "Bearer $approver" }

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
