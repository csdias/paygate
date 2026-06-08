output "endpoint" {
  description = "RDS instance endpoint (host:port)"
  value       = "${aws_db_instance.this.address}:${aws_db_instance.this.port}"
}

output "security_group_id" {
  description = "Security group attached to the RDS instance"
  value       = aws_security_group.db.id
}

output "secret_arn" {
  description = "ARN of the Secrets Manager secret containing DB credentials"
  value       = aws_secretsmanager_secret.db_credentials.arn
}

output "secret_name" {
  description = "Name of the Secrets Manager secret"
  value       = aws_secretsmanager_secret.db_credentials.name
}
