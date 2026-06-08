locals {
  name = "paygate-dev"
}

# ── ECS cluster (shared by both services) ────────────────────────────────────

resource "aws_ecs_cluster" "this" {
  name = local.name

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

resource "aws_ecs_cluster_capacity_providers" "this" {
  cluster_name       = aws_ecs_cluster.this.name
  capacity_providers = ["FARGATE"]

  default_capacity_provider_strategy {
    capacity_provider = "FARGATE"
    weight            = 1
  }
}

# ── Database ─────────────────────────────────────────────────────────────────

module "database" {
  source = "../../modules/database"

  name       = local.name
  vpc_id     = var.vpc_id
  subnet_ids = var.private_subnet_ids

  allowed_security_group_ids = [
    module.outbox_publisher.security_group_id,
    module.payment_api.task_security_group_id
  ]
}

# ── Messaging (SNS + SQS) ─────────────────────────────────────────────────────

module "messaging" {
  source = "../../modules/messaging"
  name   = local.name
}

# ── Outbox publisher (ECS Fargate) ───────────────────────────────────────────

module "outbox_publisher" {
  source = "../../modules/outbox-publisher"

  name                 = local.name
  vpc_id               = var.vpc_id
  subnet_ids           = var.private_subnet_ids
  ecs_cluster_id       = aws_ecs_cluster.this.id
  image_tag            = var.outbox_publisher_image_tag
  db_secret_arn        = module.database.secret_arn
  sns_topic_arn        = module.messaging.topic_arn
  db_security_group_id = module.database.security_group_id
}

# ── Payment API (ECS Fargate + ALB) ──────────────────────────────────────────

module "payment_api" {
  source = "../../modules/payment-api"

  name                 = local.name
  vpc_id               = var.vpc_id
  private_subnet_ids   = var.private_subnet_ids
  public_subnet_ids    = var.public_subnet_ids
  ecs_cluster_id       = aws_ecs_cluster.this.id
  image_tag            = var.payment_api_image_tag
  db_secret_arn        = module.database.secret_arn
  db_security_group_id = module.database.security_group_id
}

# ── S3 bucket for Lambda deployment ZIPs ─────────────────────────────────────
# CI/CD uploads the dotnet publish ZIP here before calling terraform apply.
# The lambda-consumer module pulls the ZIP by key.

resource "aws_s3_bucket" "lambda_artifacts" {
  bucket        = "${local.name}-lambda-artifacts"
  force_destroy = true
}

resource "aws_s3_bucket_versioning" "lambda_artifacts" {
  bucket = aws_s3_bucket.lambda_artifacts.id
  versioning_configuration { status = "Enabled" }
}

# ── Lambda consumers ──────────────────────────────────────────────────────────

module "notification_consumer" {
  source = "../../modules/lambda-consumer"

  name                  = "${local.name}-notification"
  source_queue_arn      = module.messaging.notification_queue_arn
  source_queue_url      = module.messaging.notification_queue_url
  dead_letter_queue_arn = module.messaging.notification_dlq_arn
  s3_bucket             = aws_s3_bucket.lambda_artifacts.bucket
  s3_key                = "notification/latest.zip"
  batch_size            = 10
}

module "audit_consumer" {
  source = "../../modules/lambda-consumer"

  name                  = "${local.name}-audit"
  source_queue_arn      = module.messaging.audit_queue_arn
  source_queue_url      = module.messaging.audit_queue_url
  dead_letter_queue_arn = module.messaging.audit_dlq_arn
  s3_bucket             = aws_s3_bucket.lambda_artifacts.bucket
  s3_key                = "audit/latest.zip"
  db_secret_arn         = module.database.secret_arn
  batch_size            = 10

  environment_variables = {
    DB_CONNECTION_STRING = "Host=${module.database.endpoint};Database=paygate;Username=paygate;Password=REPLACED_BY_INIT"
  }
}

# ── Frontend (S3 + CloudFront) ────────────────────────────────────────────────

module "frontend" {
  source = "../../modules/frontend"
  name   = local.name
}

# ── CI/CD pipelines (one per deployable artifact) ─────────────────────────────
# Each pipeline: Source (CodeStar → GitHub) → Build (CodeBuild does build + deploy).
# The codestar_connection_arn must be set to "Available" via AWS Console before
# any pipeline can pull source. Run: AWS Console → CodePipeline → Connections.

module "outbox_publisher_pipeline" {
  source = "../../modules/codepipeline"

  name                    = "${local.name}-outbox-publisher"
  pipeline_type           = "ecs"
  codestar_connection_arn = var.codestar_connection_arn
  repository_id           = "${var.github_owner}/paygate.message.exchange"
  artifact_bucket         = aws_s3_bucket.lambda_artifacts.bucket

  ecs_config = {
    ecr_repository_url = module.outbox_publisher.ecr_repository_url
    ecs_cluster_name   = aws_ecs_cluster.this.name
    ecs_service_name   = module.outbox_publisher.ecs_service_name
    # Build context is repo root so COPY can reach sibling project folders
    build_context   = "."
    dockerfile_path = "OutboxPublisher/Pay.Message.Exchange.OutboxPublisher/Dockerfile"
  }
}

module "payment_api_pipeline" {
  source = "../../modules/codepipeline"

  name                    = "${local.name}-payment-api"
  pipeline_type           = "ecs"
  codestar_connection_arn = var.codestar_connection_arn
  repository_id           = "${var.github_owner}/paygate.api"
  artifact_bucket         = aws_s3_bucket.lambda_artifacts.bucket

  ecs_config = {
    ecr_repository_url = module.payment_api.ecr_repository_url
    ecs_cluster_name   = aws_ecs_cluster.this.name
    ecs_service_name   = module.payment_api.ecs_service_name
    build_context      = "."
    dockerfile_path    = "Dockerfile"
  }
}

module "notification_consumer_pipeline" {
  source = "../../modules/codepipeline"

  name                    = "${local.name}-notification"
  pipeline_type           = "lambda"
  codestar_connection_arn = var.codestar_connection_arn
  repository_id           = "${var.github_owner}/paygate.consumers"
  artifact_bucket         = aws_s3_bucket.lambda_artifacts.bucket

  lambda_config = {
    function_name = module.notification_consumer.function_name
    s3_bucket     = aws_s3_bucket.lambda_artifacts.bucket
    s3_key        = "notification/latest.zip"
    project_path  = "Paygate.Notification"
  }
}

module "audit_consumer_pipeline" {
  source = "../../modules/codepipeline"

  name                    = "${local.name}-audit"
  pipeline_type           = "lambda"
  codestar_connection_arn = var.codestar_connection_arn
  repository_id           = "${var.github_owner}/paygate.consumers"
  artifact_bucket         = aws_s3_bucket.lambda_artifacts.bucket

  lambda_config = {
    function_name = module.audit_consumer.function_name
    s3_bucket     = aws_s3_bucket.lambda_artifacts.bucket
    s3_key        = "audit/latest.zip"
    project_path  = "Paygate.Audit"
  }
}

module "frontend_pipeline" {
  source = "../../modules/codepipeline"

  name                    = "${local.name}-frontend"
  pipeline_type           = "frontend"
  codestar_connection_arn = var.codestar_connection_arn
  repository_id           = "${var.github_owner}/paygate.web"
  artifact_bucket         = aws_s3_bucket.lambda_artifacts.bucket

  frontend_config = {
    s3_bucket                  = module.frontend.bucket_name
    cloudfront_distribution_id = module.frontend.cloudfront_distribution_id
    # Vite outputs to "dist"; change to "build" for Create React App
    build_output_dir = "dist"
  }
}
