# paygate.terraform

Terraform modules and environments for the Paygate payment system.

## Structure

```
modules/
  database/           RDS Postgres + Secrets Manager secret + security group
  messaging/          SNS topic → SQS fan-out (notification + audit) + DLQs
  outbox-publisher/   ECS Fargate service for paygate.message.exchange
  payment-api/        ECS Fargate service + ALB for paygate.api
  frontend/           S3 + CloudFront for the React SPA

environments/
  dev/                Root module — wires all modules together for the dev account
```

## Prerequisites

- Terraform >= 1.9
- AWS credentials with sufficient permissions
- An existing VPC with private and public subnets

## Usage

```bash
cd environments/dev
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your vpc_id and subnet IDs
terraform init
terraform plan
terraform apply
```

## Design decisions

### Modules own their own ECR repositories

Each ECS module (`outbox-publisher`, `payment-api`) creates its own ECR repository
internally rather than accepting a pre-existing URL as a variable. This avoids a
chicken-and-egg problem: the repository must exist before a CI/CD pipeline can push
an image to it, but the pipeline only runs after the infrastructure is up. By letting
Terraform create the repo on first `apply`, you can push images immediately and the
ECS task definition simply references `aws_ecr_repository.this.repository_url`.

### ALB is HTTP-only

The `payment-api` module creates an HTTP-only ALB listener. HTTPS requires an ACM
certificate bound to a real domain name, which is environment-specific and outside
the scope of the module. When you add a domain:

1. Create an ACM certificate (or import one) in `eu-west-1`.
2. Replace the port-80 `aws_lb_listener` in the module with a port-443 HTTPS listener
   referencing the certificate ARN.
3. Add a second port-80 listener with a `redirect` default action pointing to HTTPS.

### SPA routing fallback in CloudFront

The `frontend` module configures CloudFront to return `index.html` with a `200` status
for both `403` and `404` responses from S3. Without this, React Router deep-links
(e.g. `/payments/abc-123`) would return a 403 from S3 because the object does not
exist at that key. The 403 (not 404) is what S3 returns when the bucket has no public
access and the key is missing.

### SNS → SQS fan-out

The `messaging` module creates two independent SQS queues (`notification`, `audit`)
both subscribed to the same SNS topic. This is the fan-out pattern: a single
`PaymentInitiatedEvent` published to SNS is delivered to both queues independently.
Each queue has its own DLQ. If the notification Lambda fails after `maxReceiveCount`
retries, the message lands in `notification-dlq` — the audit queue is completely
unaffected.

### Lambda custom runtime (`provided.al2023`)

.NET 10 has no managed Lambda runtime. Both consumer Lambdas use `provided.al2023`
with `Amazon.Lambda.RuntimeSupport` — the compiled `bootstrap` executable _is_ the
runtime. The `handler` field in Terraform is set to `"bootstrap"` (the default for
custom runtimes). CI/CD publishes with `dotnet publish -r linux-x64 -c Release` and
uploads the ZIP to S3; Terraform references the ZIP by bucket + key.

### Partial batch failure (`ReportBatchItemFailures`)

Both Lambda functions return `SQSBatchResponse` with a `BatchItemFailures` list.
The SQS event source mapping is configured with `function_response_types = ["ReportBatchItemFailures"]`.
This means if 2 out of 10 messages fail, only those 2 go back on the queue (and
eventually to the DLQ after `maxReceiveCount` retries). Without this, a single bad
message would cause all 10 to retry — the classic SQS poison-pill problem.

### One pipeline per service, not one monorepo pipeline

Each deployable artifact gets its own CodePipeline: `outbox-publisher`, `payment-api`,
`notification-consumer`, `audit-consumer`, and `frontend`. A commit in `paygate.api`
only triggers the payment-api pipeline — it does not rebuild the Lambda consumers.
This keeps pipelines fast and failures isolated. The trade-off is five separate
CodeStar connections (but each can share one GitHub App installation).

### CodeStar connection must be activated before first run

A CodeStar connection is created by `terraform apply` in a `PENDING` state. It becomes
`AVAILABLE` only after you complete the GitHub OAuth handshake in the AWS Console
(CodePipeline → Settings → Connections → Complete handshake). Pipelines silently wait
until the connection is active. One connection ARN is shared across all five pipelines.

### Two-stage pipeline: Source → Build (Build also deploys)

Each pipeline has exactly two stages. The Build stage (CodeBuild) both compiles/packages
and deploys in the same project:

- **ECS**: `docker build` → ECR push → `aws ecs update-service --force-new-deployment`
- **Lambda**: `dotnet publish -r linux-x64` → ZIP → S3 upload → `aws lambda update-function-code`
- **Frontend**: `npm ci && npm run build` → `aws s3 sync` → CloudFront invalidation

A separate Deploy stage (using the ECS or Lambda deploy action provider) would give
cleaner rollback semantics but adds complexity. This two-stage approach is sufficient
for a dev environment. Add an approval action between stages when promoting to prod.

### .NET 10 is not a managed CodeBuild runtime

The Lambda build installs .NET 10 inline via the `dotnet-install.sh` script during
the `install` phase. There is no `dotnet: 10` in `runtime-versions` — that block only
covers AWS-managed runtimes (up to .NET 8 as of mid-2025). The install adds ~60s to
each Lambda build.

### Shared artifact bucket for CodePipeline and Lambda ZIPs

The `lambda_artifacts` S3 bucket (created in `environments/dev/main.tf`) serves double
duty: it stores CodePipeline inter-stage artifacts AND the Lambda deployment ZIPs. This
avoids creating a separate CodePipeline artifact bucket. Versioning is enabled on the
bucket so CodePipeline can retrieve specific artifact versions.

### FOR UPDATE SKIP LOCKED in the outbox publisher

The outbox publisher (`paygate.message.exchange`) uses PostgreSQL's
`SELECT ... FOR UPDATE SKIP LOCKED` to allow multiple ECS tasks to poll the outbox
concurrently without blocking each other. Each task locks a distinct batch of rows,
publishes them to SNS, then marks them published. This is why PostgreSQL was chosen
over a general-purpose relational database — MSSQL supports the same syntax since
2005 but the UCAS team validated Postgres first.
