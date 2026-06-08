data "aws_region" "current" {}

# ── CloudWatch log group ──────────────────────────────────────────────────────

resource "aws_cloudwatch_log_group" "this" {
  name              = "/ecs/${var.name}-payment-api"
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
  name               = "${var.name}-payment-api-exec"
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
  name               = "${var.name}-payment-api-task"
  assume_role_policy = data.aws_iam_policy_document.assume_ecs_task.json
  tags               = var.tags
}

# ── Security groups ───────────────────────────────────────────────────────────

resource "aws_security_group" "alb" {
  name        = "${var.name}-payment-api-alb"
  description = "Payment API ALB"
  vpc_id      = var.vpc_id
  tags        = merge(var.tags, { Name = "${var.name}-payment-api-alb" })

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "task" {
  name        = "${var.name}-payment-api-task"
  description = "Payment API ECS task"
  vpc_id      = var.vpc_id
  tags        = merge(var.tags, { Name = "${var.name}-payment-api-task" })

  ingress {
    description     = "HTTP from ALB"
    from_port       = var.container_port
    to_port         = var.container_port
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

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
  name                 = "${var.name}-payment-api"
  image_tag_mutability = "MUTABLE"
  tags                 = var.tags

  image_scanning_configuration {
    scan_on_push = true
  }
}

# ── ALB ───────────────────────────────────────────────────────────────────────

resource "aws_lb" "this" {
  name               = "${var.name}-payment-api"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = var.public_subnet_ids
  tags               = var.tags
}

resource "aws_lb_target_group" "this" {
  name        = "${var.name}-payment-api"
  port        = var.container_port
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip"
  tags        = var.tags

  health_check {
    path                = var.health_check_path
    healthy_threshold   = 2
    unhealthy_threshold = 3
    interval            = 30
  }
}

# HTTP only — HTTPS requires an ACM certificate tied to a real domain name.
# When you add a domain, replace this listener with port 443/HTTPS and add a
# second listener on 80 that redirects to 443.
resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.this.arn
  port              = 80
  protocol          = "HTTP"
  tags              = var.tags

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.this.arn
  }
}

# ── ECS task definition ───────────────────────────────────────────────────────

resource "aws_ecs_task_definition" "this" {
  family                   = "${var.name}-payment-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.cpu
  memory                   = var.memory
  execution_role_arn       = aws_iam_role.task_exec.arn
  task_role_arn            = aws_iam_role.task.arn
  tags                     = var.tags

  container_definitions = jsonencode([{
    name  = "payment-api"
    image = "${aws_ecr_repository.this.repository_url}:${var.image_tag}"

    portMappings = [{
      containerPort = var.container_port
      protocol      = "tcp"
    }]

    environment = [
      { name = "DOTNET_ENVIRONMENT", value = "Production" },
      { name = "ASPNETCORE_URLS", value = "http://+:${var.container_port}" }
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
        "awslogs-stream-prefix" = "payment-api"
      }
    }

    essential    = true
    healthCheck  = {
      command     = ["CMD-SHELL", "curl -f http://localhost:${var.container_port}/health || exit 1"]
      interval    = 30
      timeout     = 5
      retries     = 3
      startPeriod = 10
    }
  }])
}

# ── ECS service ───────────────────────────────────────────────────────────────

resource "aws_ecs_service" "this" {
  name            = "${var.name}-payment-api"
  cluster         = var.ecs_cluster_id
  task_definition = aws_ecs_task_definition.this.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"
  tags            = var.tags

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [aws_security_group.task.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.this.arn
    container_name   = "payment-api"
    container_port   = var.container_port
  }

  lifecycle {
    ignore_changes = [task_definition]
  }
}
