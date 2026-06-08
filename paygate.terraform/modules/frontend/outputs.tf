output "bucket_name" {
  description = "S3 bucket name to sync the React build into"
  value       = aws_s3_bucket.this.bucket
}

output "cloudfront_domain" {
  description = "CloudFront distribution domain (use this as the app URL)"
  value       = aws_cloudfront_distribution.this.domain_name
}

output "cloudfront_distribution_id" {
  description = "Distribution ID — needed to invalidate cache after deploy"
  value       = aws_cloudfront_distribution.this.id
}
