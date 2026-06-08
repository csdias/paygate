data "aws_region" "current" {}

# ── CloudWatch log group ──────────────────────────────────────────────────────

resource "aws_cloudwatch_log_group" "this" {
  name              = "/aws/lambda/${var.name}"
  retention_in_days = var.log_retention_days
  tags              = var.tags
}

# ── IAM execution role ────────────────────────────────────────────────────────

data "aws_iam_policy_document" "assume_lambda" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "this" {
  name               = "${var.name}-lambda"
  assume_role_policy = data.aws_iam_policy_document.assume_lambda.json
  tags               = var.tags
}

resource "aws_iam_role_policy_attachment" "basic_execution" {
  role       = aws_iam_role.this.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_iam_role_policy" "sqs_consumer" {
  name = "sqs-consume"
  role = aws_iam_role.this.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = [
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage",
        "sqs:GetQueueAttributes"
      ]
      Resource = [var.source_queue_arn]
    }]
  })
}

resource "aws_iam_role_policy" "secrets" {
  count = var.db_secret_arn != "" ? 1 : 0
  name  = "read-db-secret"
  role  = aws_iam_role.this.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["secretsmanager:GetSecretValue"]
      Resource = [var.db_secret_arn]
    }]
  })
}

# ── Lambda function ───────────────────────────────────────────────────────────

resource "aws_lambda_function" "this" {
  function_name = var.name
  role          = aws_iam_role.this.arn
  handler       = var.handler
  runtime       = var.runtime
  timeout       = var.timeout_seconds
  memory_size   = var.memory_mb

  s3_bucket = var.s3_bucket
  s3_key    = var.s3_key

  # Partial batch failure: Lambda only retries the items that failed,
  # not the whole batch. Requires SQSBatchResponse from the handler.
  dynamic "environment" {
    for_each = length(var.environment_variables) > 0 ? [1] : []
    content {
      variables = var.environment_variables
    }
  }

  dead_letter_config {
    target_arn = var.dead_letter_queue_arn
  }

  depends_on = [aws_cloudwatch_log_group.this]
  tags       = var.tags
}

# ── SQS event source mapping ──────────────────────────────────────────────────

resource "aws_lambda_event_source_mapping" "this" {
  event_source_arn                   = var.source_queue_arn
  function_name                      = aws_lambda_function.this.arn
  batch_size                         = var.batch_size
  maximum_batching_window_in_seconds = var.maximum_batching_window_seconds

  # Tell Lambda to report partial failures back to SQS so only failed
  # messages re-enter the queue rather than the whole batch being retried.
  function_response_types = ["ReportBatchItemFailures"]
}
