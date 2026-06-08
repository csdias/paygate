locals {
  queues = ["notification", "audit"]
}

# ── SNS topic ────────────────────────────────────────────────────────────────

resource "aws_sns_topic" "payment_events" {
  name = "${var.name}-payment-events"
  tags = var.tags
}

# ── SQS queues (one per consumer) + DLQs ────────────────────────────────────

resource "aws_sqs_queue" "dlq" {
  for_each = toset(local.queues)

  name                       = "${var.name}-${each.key}-dlq"
  message_retention_seconds  = var.message_retention_seconds
  tags                       = var.tags
}

resource "aws_sqs_queue" "consumer" {
  for_each = toset(local.queues)

  name                       = "${var.name}-${each.key}"
  visibility_timeout_seconds = var.visibility_timeout_seconds
  message_retention_seconds  = var.message_retention_seconds
  tags                       = var.tags

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dlq[each.key].arn
    maxReceiveCount     = var.dlq_max_receive_count
  })
}

# ── SNS → SQS subscriptions ──────────────────────────────────────────────────

resource "aws_sns_topic_subscription" "consumer" {
  for_each = toset(local.queues)

  topic_arn = aws_sns_topic.payment_events.arn
  protocol  = "sqs"
  endpoint  = aws_sqs_queue.consumer[each.key].arn

  raw_message_delivery = false
}

# ── SQS queue policies (allow SNS to send) ───────────────────────────────────

data "aws_iam_policy_document" "sqs_allow_sns" {
  for_each = toset(local.queues)

  statement {
    sid    = "AllowSNSPublish"
    effect = "Allow"

    principals {
      type        = "Service"
      identifiers = ["sns.amazonaws.com"]
    }

    actions   = ["sqs:SendMessage"]
    resources = [aws_sqs_queue.consumer[each.key].arn]

    condition {
      test     = "ArnEquals"
      variable = "aws:SourceArn"
      values   = [aws_sns_topic.payment_events.arn]
    }
  }
}

resource "aws_sqs_queue_policy" "consumer" {
  for_each  = toset(local.queues)
  queue_url = aws_sqs_queue.consumer[each.key].url
  policy    = data.aws_iam_policy_document.sqs_allow_sns[each.key].json
}
