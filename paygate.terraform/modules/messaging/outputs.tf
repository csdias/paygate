output "topic_arn" {
  description = "ARN of the payment-events SNS topic"
  value       = aws_sns_topic.payment_events.arn
}

output "notification_queue_arn" {
  value = aws_sqs_queue.consumer["notification"].arn
}

output "notification_queue_url" {
  value = aws_sqs_queue.consumer["notification"].url
}

output "notification_dlq_arn" {
  value = aws_sqs_queue.dlq["notification"].arn
}

output "audit_queue_arn" {
  value = aws_sqs_queue.consumer["audit"].arn
}

output "audit_queue_url" {
  value = aws_sqs_queue.consumer["audit"].url
}

output "audit_dlq_arn" {
  value = aws_sqs_queue.dlq["audit"].arn
}
