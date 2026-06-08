locals {
  is_ecs      = var.pipeline_type == "ecs"
  is_lambda   = var.pipeline_type == "lambda"
  is_frontend = var.pipeline_type == "frontend"

  # ── Inline buildspecs ────────────────────────────────────────────────────────
  # Values are passed via CodeBuild environment variables (set below), not
  # interpolated here, so $VARIABLE references survive the heredoc verbatim.

  buildspec_ecs = <<-BUILDSPEC
    version: 0.2
    phases:
      pre_build:
        commands:
          - echo Logging in to ECR...
          - aws ecr get-login-password --region $AWS_DEFAULT_REGION | docker login --username AWS --password-stdin $IMAGE_REPO_URL
          - IMAGE_TAG=$(echo $CODEBUILD_RESOLVED_SOURCE_VERSION | cut -c 1-8)
      build:
        commands:
          - echo Building Docker image...
          - docker build -f $DOCKERFILE_PATH -t $IMAGE_REPO_URL:$IMAGE_TAG $BUILD_CONTEXT
          - docker tag $IMAGE_REPO_URL:$IMAGE_TAG $IMAGE_REPO_URL:latest
      post_build:
        commands:
          - docker push $IMAGE_REPO_URL:$IMAGE_TAG
          - docker push $IMAGE_REPO_URL:latest
          - echo Triggering ECS redeployment...
          - aws ecs update-service --cluster $ECS_CLUSTER_NAME --service $ECS_SERVICE_NAME --force-new-deployment
  BUILDSPEC

  buildspec_lambda = <<-BUILDSPEC
    version: 0.2
    phases:
      install:
        commands:
          - curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
          - chmod +x dotnet-install.sh
          - ./dotnet-install.sh --channel 10.0
      build:
        commands:
          - export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet
          - cd $PROJECT_PATH
          - dotnet publish -c Release -r linux-x64 --self-contained true -o publish
          - cd publish && zip -r ../function.zip . && cd ..
          - aws s3 cp function.zip s3://$S3_BUCKET/$S3_KEY
      post_build:
        commands:
          - aws lambda update-function-code --function-name $FUNCTION_NAME --s3-bucket $S3_BUCKET --s3-key $S3_KEY
  BUILDSPEC

  buildspec_frontend = <<-BUILDSPEC
    version: 0.2
    phases:
      install:
        runtime-versions:
          nodejs: 22
        commands:
          - npm ci
      build:
        commands:
          - npm run build
      post_build:
        commands:
          - aws s3 sync $BUILD_OUTPUT_DIR s3://$FRONTEND_BUCKET --delete
          - aws cloudfront create-invalidation --distribution-id $CF_DISTRIBUTION_ID --paths "/*"
  BUILDSPEC

  buildspec = (
    local.is_ecs ? local.buildspec_ecs :
    local.is_lambda ? local.buildspec_lambda :
    local.buildspec_frontend
  )

  # ── CodeBuild environment variables per type ─────────────────────────────────
  # try() guards attribute access on nullable config objects so Terraform
  # does not error when the config for a different type is null.

  env_vars = concat(
    try([
      { name = "IMAGE_REPO_URL",   value = var.ecs_config.ecr_repository_url, type = "PLAINTEXT" },
      { name = "ECS_CLUSTER_NAME", value = var.ecs_config.ecs_cluster_name,   type = "PLAINTEXT" },
      { name = "ECS_SERVICE_NAME", value = var.ecs_config.ecs_service_name,   type = "PLAINTEXT" },
      { name = "BUILD_CONTEXT",    value = var.ecs_config.build_context,      type = "PLAINTEXT" },
      { name = "DOCKERFILE_PATH",  value = var.ecs_config.dockerfile_path,    type = "PLAINTEXT" },
    ], []),
    try([
      { name = "FUNCTION_NAME", value = var.lambda_config.function_name, type = "PLAINTEXT" },
      { name = "S3_BUCKET",     value = var.lambda_config.s3_bucket,     type = "PLAINTEXT" },
      { name = "S3_KEY",        value = var.lambda_config.s3_key,        type = "PLAINTEXT" },
      { name = "PROJECT_PATH",  value = var.lambda_config.project_path,  type = "PLAINTEXT" },
    ], []),
    try([
      { name = "FRONTEND_BUCKET",    value = var.frontend_config.s3_bucket,                  type = "PLAINTEXT" },
      { name = "CF_DISTRIBUTION_ID", value = var.frontend_config.cloudfront_distribution_id, type = "PLAINTEXT" },
      { name = "BUILD_OUTPUT_DIR",   value = var.frontend_config.build_output_dir,            type = "PLAINTEXT" },
    ], [])
  )
}

# ── IAM role: CodePipeline ────────────────────────────────────────────────────

resource "aws_iam_role" "pipeline" {
  name = "${var.name}-pipeline"
  tags = var.tags

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "codepipeline.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy" "pipeline" {
  name = "policy"
  role = aws_iam_role.pipeline.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:GetObjectVersion",
          "s3:PutObject",
          "s3:GetBucketVersioning"
        ]
        Resource = [
          "arn:aws:s3:::${var.artifact_bucket}",
          "arn:aws:s3:::${var.artifact_bucket}/*"
        ]
      },
      {
        Effect   = "Allow"
        Action   = "codestar-connections:UseConnection"
        Resource = var.codestar_connection_arn
      },
      {
        Effect = "Allow"
        Action = [
          "codebuild:BatchGetBuilds",
          "codebuild:StartBuild"
        ]
        Resource = "*"
      }
    ]
  })
}

# ── IAM role: CodeBuild ────────────────────────────────────────────────────────

resource "aws_iam_role" "build" {
  name = "${var.name}-build"
  tags = var.tags

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "codebuild.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

# Base policy: CloudWatch Logs + S3 artifact bucket access (all types need this)
resource "aws_iam_role_policy" "build_base" {
  name = "base"
  role = aws_iam_role.build.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "*"
      },
      {
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:GetObjectVersion",
          "s3:PutObject",
          "s3:GetBucketVersioning"
        ]
        Resource = [
          "arn:aws:s3:::${var.artifact_bucket}",
          "arn:aws:s3:::${var.artifact_bucket}/*"
        ]
      }
    ]
  })
}

# ECS policy: ECR push + ECS force-new-deployment
resource "aws_iam_role_policy" "build_ecs" {
  count = local.is_ecs ? 1 : 0
  name  = "ecs-ecr"
  role  = aws_iam_role.build.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        # GetAuthorizationToken must always target *
        Effect   = "Allow"
        Action   = ["ecr:GetAuthorizationToken"]
        Resource = ["*"]
      },
      {
        Effect = "Allow"
        Action = [
          "ecr:BatchCheckLayerAvailability",
          "ecr:PutImage",
          "ecr:InitiateLayerUpload",
          "ecr:UploadLayerPart",
          "ecr:CompleteLayerUpload"
        ]
        Resource = ["arn:aws:ecr:*:*:repository/*"]
      },
      {
        Effect   = "Allow"
        Action   = ["ecs:UpdateService", "ecs:DescribeServices"]
        Resource = ["*"]
      }
    ]
  })
}

# Lambda policy: S3 ZIP upload + Lambda update-function-code
resource "aws_iam_role_policy" "build_lambda" {
  count = local.is_lambda ? 1 : 0
  name  = "lambda-deploy"
  role  = aws_iam_role.build.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = ["s3:PutObject", "s3:GetObject"]
        # The lambda ZIPs may go to a different bucket than the artifact bucket
        Resource = ["arn:aws:s3:::*"]
      },
      {
        Effect   = "Allow"
        Action   = ["lambda:UpdateFunctionCode", "lambda:GetFunction"]
        Resource = ["*"]
      }
    ]
  })
}

# Frontend policy: S3 sync + CloudFront cache invalidation
resource "aws_iam_role_policy" "build_frontend" {
  count = local.is_frontend ? 1 : 0
  name  = "frontend-deploy"
  role  = aws_iam_role.build.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "s3:PutObject",
          "s3:GetObject",
          "s3:DeleteObject",
          "s3:ListBucket"
        ]
        Resource = ["arn:aws:s3:::*"]
      },
      {
        Effect   = "Allow"
        Action   = ["cloudfront:CreateInvalidation"]
        Resource = ["*"]
      }
    ]
  })
}

# ── CodeBuild project ─────────────────────────────────────────────────────────

resource "aws_codebuild_project" "this" {
  name          = var.name
  build_timeout = 30
  service_role  = aws_iam_role.build.arn
  tags          = var.tags

  source {
    type      = "CODEPIPELINE"
    buildspec = local.buildspec
  }

  artifacts {
    type = "CODEPIPELINE"
  }

  environment {
    compute_type = "BUILD_GENERAL1_SMALL"
    image        = "aws/codebuild/standard:7.0"
    type         = "LINUX_CONTAINER"

    # docker build requires access to the Docker daemon
    privileged_mode = local.is_ecs

    dynamic "environment_variable" {
      for_each = local.env_vars
      content {
        name  = environment_variable.value.name
        value = environment_variable.value.value
        type  = environment_variable.value.type
      }
    }
  }

  logs_config {
    cloudwatch_logs {
      group_name  = "/codebuild/${var.name}"
      stream_name = "build"
    }
  }
}

# ── CodePipeline ──────────────────────────────────────────────────────────────

resource "aws_codepipeline" "this" {
  name     = var.name
  role_arn = aws_iam_role.pipeline.arn
  tags     = var.tags

  artifact_store {
    location = var.artifact_bucket
    type     = "S3"
  }

  stage {
    name = "Source"

    action {
      name             = "Source"
      category         = "Source"
      owner            = "AWS"
      provider         = "CodeStarSourceConnection"
      version          = "1"
      output_artifacts = ["source"]

      configuration = {
        ConnectionArn        = var.codestar_connection_arn
        FullRepositoryId     = var.repository_id
        BranchName           = var.branch
        # Detect changes automatically — no polling
        DetectChanges        = "true"
        OutputArtifactFormat = "CODE_ZIP"
      }
    }
  }

  stage {
    name = "Build"

    action {
      name            = "Build"
      category        = "Build"
      owner           = "AWS"
      provider        = "CodeBuild"
      version         = "1"
      input_artifacts = ["source"]

      configuration = {
        ProjectName = aws_codebuild_project.this.name
      }
    }
  }
}
