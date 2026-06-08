data "aws_region" "current" {}
data "aws_caller_identity" "current" {}

# ── CloudWatch log group ──────────────────────────────────────────────────────

resource "aws_cloudwatch_log_group" "this" {
  name              = "/ecs/${var.name}-outbox-publisher"
  retention_in_days = var.log_retention_days
  tags              = var.tags
}

# ── IAM ───────────────────────────────────────────────────────────────────────

data "aws_iam_policy_document" "assume_ecs_task" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ecs-tasks.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "task_exec" {
  name               = "${var.name}-outbox-publisher-exec"
  assume_role_policy = data.aws_iam_policy_document.assume_ecs_task.json
  tags               = var.tags
}

resource "aws_iam_role_policy_attachment" "task_exec_managed" {
  role       = aws_iam_role.task_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_iam_role_policy" "task_exec_secrets" {
  name = "read-db-secret"
  role = aws_iam_role.task_exec.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["secretsmanager:GetSecretValue"]
      Resource = [var.db_secret_arn]
    }]
  })
}

resource "aws_iam_role" "task" {
  name               = "${var.name}-outbox-publisher-task"
  assume_role_policy = data.aws_iam_policy_document.assume_ecs_task.json
  tags               = var.tags
}

resource "aws_iam_role_policy" "task_sns" {
  name = "sns-publish"
  role = aws_iam_role.task.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["sns:Publish"]
      Resource = [var.sns_topic_arn]
    }]
  })
}

# ── Security group ────────────────────────────────────────────────────────────

resource "aws_security_group" "this" {
  name        = "${var.name}-outbox-publisher"
  description = "Outbox publisher ECS task"
  vpc_id      = var.vpc_id
  tags        = merge(var.tags, { Name = "${var.name}-outbox-publisher" })

  egress {
    description     = "Postgres to RDS"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [var.db_security_group_id]
  }

  egress {
    description = "HTTPS to AWS APIs"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# ── ECR repository ────────────────────────────────────────────────────────────
# Each module owns its own ECR repo so there is no chicken-and-egg problem
# between the image existing and the infrastructure being created. The CI/CD
# pipeline pushes to the repo that Terraform already created.
resource "aws_ecr_repository" "this" {
  name                 = "${var.name}-outbox-publisher"
  image_tag_mutability = "MUTABLE"
  tags                 = var.tags

  image_scanning_configuration {
    scan_on_push = true
  }
}

# ── ECS task definition ───────────────────────────────────────────────────────

resource "aws_ecs_task_definition" "this" {
  family                   = "${var.name}-outbox-publisher"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.cpu
  memory                   = var.memory
  execution_role_arn       = aws_iam_role.task_exec.arn
  task_role_arn            = aws_iam_role.task.arn
  tags                     = var.tags

  container_definitions = jsonencode([{
    name  = "outbox-publisher"
    image = "${aws_ecr_repository.this.repository_url}:${var.image_tag}"


    environment = [
      { name = "DOTNET_ENVIRONMENT", value = "Production" },
      { name = "SecretsManagerRdsUserCredentials", value = var.db_secret_arn }
    ]

    secrets = [{
      name      = "ConnectionStrings__local"
      valueFrom = "${var.db_secret_arn}:connection_string::"
    }]

    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.this.name
        "awslogs-region"        = data.aws_region.current.name
        "awslogs-stream-prefix" = "outbox-publisher"
      }
    }

    essential = true
  }])
}

# ── ECS service ───────────────────────────────────────────────────────────────

resource "aws_ecs_service" "this" {
  name            = "${var.name}-outbox-publisher"
  cluster         = var.ecs_cluster_id
  task_definition = aws_ecs_task_definition.this.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"
  tags            = var.tags

  network_configuration {
    subnets          = var.subnet_ids
    security_groups  = [aws_security_group.this.id]
    assign_public_ip = false
  }

  lifecycle {
    ignore_changes = [task_definition]
  }
}
