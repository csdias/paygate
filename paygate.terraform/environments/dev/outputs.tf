output "payment_api_url" {
  description = "Public URL for the payment API"
  value       = "http://${module.payment_api.alb_dns_name}"
}

output "frontend_url" {
  description = "CloudFront URL for the React frontend"
  value       = "https://${module.frontend.cloudfront_domain}"
}

output "outbox_publisher_ecr" {
  description = "ECR repository to push the outbox-publisher image to"
  value       = module.outbox_publisher.ecr_repository_url
}

output "payment_api_ecr" {
  description = "ECR repository to push the payment-api image to"
  value       = module.payment_api.ecr_repository_url
}

output "frontend_bucket" {
  description = "S3 bucket to sync the React build into"
  value       = module.frontend.bucket_name
}

output "cloudfront_distribution_id" {
  description = "CloudFront distribution ID — pass to cache invalidation in CI/CD"
  value       = module.frontend.cloudfront_distribution_id
}

output "lambda_artifacts_bucket" {
  description = "S3 bucket to upload Lambda deployment ZIPs to"
  value       = aws_s3_bucket.lambda_artifacts.bucket
}

output "notification_lambda_arn" {
  value = module.notification_consumer.function_arn
}

output "audit_lambda_arn" {
  value = module.audit_consumer.function_arn
}

output "db_secret_arn" {
  description = "Secrets Manager ARN for RDS credentials"
  value       = module.database.secret_arn
  sensitive   = true
}

output "sns_topic_arn" {
  description = "Payment events SNS topic ARN"
  value       = module.messaging.topic_arn
}
